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
using Soenneker.Atomics.Bools;
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

    // Bounded channel prevents infinite growth if tests end or helper is missing.
    private readonly Channel<LogEvent> _ch = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(_channelCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropWrite,
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

    private readonly AtomicBool _disposed = new();

    public InjectableTestOutputSink(string outputTemplate = _defaultTemplate, IFormatProvider? formatProvider = null)
    {
        _fmt = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        _readerTask = Task.Run(() => ReadLoop(_cts.Token));
    }

    /// <summary>Inject the current test's output helper (call at test start).</summary>
    public void Inject(ITestOutputHelper helper, IMessageSink? diagnosticSink = null)
    {
        ArgumentNullException.ThrowIfNull(helper);
        _helper = helper; // publish to reader
        _sink = diagnosticSink;
    }

    /// <summary>Serilog pipeline entry point.</summary>
    public void Emit(LogEvent logEvent)
    {
        if (logEvent is null || _disposed.IsTrue)
            return;

        _ch.Writer.TryWrite(logEvent); // non-blocking; may drop when full
    }

    public void Complete()
    {
        if (_disposed.IsTrue)
            return;

        _helper = null; // stop xUnit writes
        _ch.Writer.TryComplete(); // prefer graceful drain
        // optional: don't cancel here; let Dispose handle fallback cancel on timeout
    }

    private async Task ReadLoop(CancellationToken ct)
    {
        try
        {
            await foreach (LogEvent evt in _ch.Reader.ReadAllAsync(ct)
                               .ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested) break;

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
        if (!_disposed.TrySetTrue()) return;

        _helper = null; // stop xUnit calls after this point
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

        _cts.Dispose();
    }

    public void Dispose()
    {
        if (!_disposed.TrySetTrue()) return;

        _helper = null;
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

        _cts.Dispose();
    }
}