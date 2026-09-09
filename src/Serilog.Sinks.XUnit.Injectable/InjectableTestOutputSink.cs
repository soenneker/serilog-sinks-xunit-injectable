using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.ReusableStringWriter;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Soenneker.Atomics.ValueBools;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Serilog.Sinks.XUnit.Injectable;

///<inheritdoc cref="IInjectableTestOutputSink"/>
public sealed class InjectableTestOutputSink : IInjectableTestOutputSink
{
    private const string _defaultTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{Exception}";
    private const int _backlogCap = 2048; // limit in-memory backlog when helper isn't available
    private const int _channelCapacity = 4096; // apply backpressure under heavy logging

    private static readonly TimeSpan _drainWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _cancelWait = TimeSpan.FromSeconds(3);

    private readonly MessageTemplateTextFormatter _fmt;

    /// <summary>
    /// Bounded channel prevents infinite growth if tests end or helper is missing.
    /// Items carry either a log event or a drain barrier (used to deterministically wait for
    /// the reader to have written everything enqueued before it).
    /// </summary>
    /// <remarks>
    /// <see cref="BoundedChannelFullMode.Wait"/> (not DropWrite) is used so the drain barrier,
    /// written via WriteAsync, is never dropped when the channel is full: it blocks until the
    /// reader frees space and is therefore guaranteed to be enqueued. Normal log events keep
    /// using TryWrite, which is best-effort and drops when full (see <see cref="Emit"/>).
    /// </remarks>
    private readonly Channel<SinkItem> _ch = Channel.CreateBounded<SinkItem>(new BoundedChannelOptions(_channelCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readerTask;

    private readonly ReusableStringWriter _sw = new();

    // Volatile so producers/reader see latest references without locks
    private volatile ITestOutputHelper? _helper;
    private volatile IMessageSink? _sink;

    // Only the reader loop touches this queue
    private readonly Queue<LogEvent> _pending = new();

    private ValueAtomicBool _disposed;

    public InjectableTestOutputSink(string outputTemplate = _defaultTemplate, IFormatProvider? formatProvider = null)
    {
        _fmt = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        _readerTask = Task.Run(() => ReadLoop(_cts.Token));
    }

    /// <summary>
    /// Replaces the active output helper after draining any output still queued for the previous one.
    /// Intended to be called at a test boundary (when switching from one test to the next); it is not
    /// designed to run concurrently with <see cref="Emit"/> from multiple tests at the same time.
    /// </summary>
    public void Inject(ITestOutputHelper helper, IMessageSink? diagnosticSink = null)
    {
        ArgumentNullException.ThrowIfNull(helper);

        // Drain the previously-injected helper first so any output still queued for it is
        // written there before we re-point. Otherwise the reader (which reads _helper per
        // event) would write the previous test's leftover lines to the newly injected helper.
        DrainCurrentHelper();

        _helper = helper; // publish to reader
        _sink = diagnosticSink;
    }

    /// <summary>
    /// Waits until the reader has written every event enqueued so far to the currently-injected helper.
    /// </summary>
    public async ValueTask FlushAsync()
    {
        if (_disposed.Value || _helper is null)
            return;

        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            await _ch.Writer.WriteAsync(new SinkItem(null, barrier)).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return;
        }

        await barrier.Task.ConfigureAwait(false);
    }

    private void DrainCurrentHelper()
    {
        if (_disposed.Value || _helper is null)
            return;

        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            // AsTask() yields a real Task so GetResult() blocks until the channel has space under
            // Wait mode; observing the pending async ValueTask directly would throw "not completed".
            _ch.Writer.WriteAsync(new SinkItem(null, barrier)).AsTask().GetAwaiter().GetResult();
        }
        catch (ChannelClosedException)
        {
            return;
        }

        barrier.Task.GetAwaiter().GetResult();
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent is null || _disposed.Value)
            return;

        _ch.Writer.TryWrite(new SinkItem(logEvent, null)); // non-blocking; may drop when full
    }

    public void Complete()
    {
        if (_disposed.Value)
            return;

        _helper = null; // stop xUnit writes
        _ch.Writer.TryComplete(); // prefer graceful drain
        // optional: don't cancel here; let Dispose handle fallback cancel on timeout
    }

    private async Task ReadLoop(CancellationToken ct)
    {
        try
        {
            await foreach (var item in _ch.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested)
                    break;

                if (item.Barrier is not null)
                {
                    // Everything enqueued before this barrier has been processed.
                    item.Barrier.TrySetResult();
                    continue;
                }

                var evt = item.Event!;

                ITestOutputHelper? helper = _helper; // volatile read
                if (helper is null)
                {
                    if (_pending.Count < _backlogCap)
                        _pending.Enqueue(evt);
                    continue;
                }

                // Flush any backlog that accumulated before helper arrived
                while (!ct.IsCancellationRequested && _pending.Count > 0)
                    Write(_pending.Dequeue(), helper);

                if (!ct.IsCancellationRequested)
                    Write(evt, helper);
            }
        }
        catch (OperationCanceledException)
        {
            // expected during teardown
        }
        catch
        {
            // never let logging crash tests
        }
    }

    private void Write(LogEvent evt, ITestOutputHelper helper)
    {
        try
        {
            _sw.Reset();
            _fmt.Format(evt, _sw);
            string message = _sw.Finish();

            try
            {
                _sink?.OnMessage(new DiagnosticMessage(message));
            }
            catch
            {
                /* ignore */
            }

            try
            {
                helper.WriteLine(message);
            }
            catch (InvalidOperationException)
            {
                // test finished; helper invalid
                _helper = null;

                if (_pending.Count < _backlogCap)
                    _pending.Enqueue(evt);
            }
            catch
            {
                _helper = null;
            }
        }
        catch
        {
            // swallow formatting/writing failures
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed.TrySetTrue())
            return;

        _ch.Writer.TryComplete(); // 1) tell reader: no more items

        try
        {
            // 2) give the reader a short window to drain cleanly
            await _readerTask.WaitAsync(_drainWait)
                             .NoSync();
        }
        catch (TimeoutException)
        {
            // 3) fallback: force-break the loop if it didn’t finish
            await _cts.CancelAsync()
                      .NoSync();
            try
            {
                await _readerTask.WaitAsync(_cancelWait)
                                 .NoSync();
            }
            catch
            {
                /* swallow during teardown */
            }
        }
        catch (OperationCanceledException)
        {
            /* ok */
        }

        try
        {
            await _sw.DisposeAsync()
                     .NoSync();
        }
        catch
        {
        }

        _helper = null; // stop xUnit calls after drain
        _cts.Dispose();
    }

    /// <summary>
    /// Releases resources used by the current instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed.TrySetTrue())
            return;

        _ch.Writer.TryComplete();

        try
        {
            _readerTask.GetAwaiter()
                       .GetResult();
        }
        catch
        {
            _cts.Cancel();
            try
            {
                _readerTask.GetAwaiter()
                           .GetResult();
            }
            catch
            {
            }
        }

        try
        {
            _sw.Dispose();
        }
        catch
        {
        }

        _helper = null; // stop xUnit calls after drain
        _cts.Dispose();
    }

    private readonly record struct SinkItem(LogEvent? Event, TaskCompletionSource? Barrier);
}