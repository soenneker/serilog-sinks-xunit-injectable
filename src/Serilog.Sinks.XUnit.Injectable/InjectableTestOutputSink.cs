using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Soenneker.Utils.ReusableStringWriter;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Serilog.Sinks.XUnit.Injectable;

public sealed class InjectableTestOutputSink : IInjectableTestOutputSink
{
    private const string _defaultTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{Exception}";
    private const int _backlogCap = 2048;
    private readonly object _gate = new();
    private readonly MessageTemplateTextFormatter _fmt;
    private readonly ReusableStringWriter _sw = new();
    // TryWrite drops overflowing events; WriteAsync lets flush barriers wait for capacity.
    private readonly Channel<WorkItem> _ch = Channel.CreateBounded<WorkItem>(new BoundedChannelOptions(4096)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });
    private readonly Queue<LogEvent> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readerTask;
    private Target? _target;
    private bool _completed;
    private Task? _disposeTask;

    private sealed record Target(ITestOutputHelper Helper, IMessageSink? Sink);
    private readonly record struct WorkItem(LogEvent? Event, Target? Target, TaskCompletionSource? Barrier = null);

    public InjectableTestOutputSink(string outputTemplate = _defaultTemplate, IFormatProvider? formatProvider = null)
    {
        _fmt = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        _readerTask = Task.Run(ReadLoop);
    }

    public void Inject(ITestOutputHelper helper, IMessageSink? diagnosticSink = null)
    {
        ArgumentNullException.ThrowIfNull(helper);
        lock (_gate)
        {
            if (_completed)
                return;

            _target = new Target(helper, diagnosticSink);
            // Only startup events are unassigned. A failed helper never creates a new backlog.
            while (_pending.TryDequeue(out LogEvent? evt))
                _ch.Writer.TryWrite(new WorkItem(evt, _target));
        }
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent is null)
            return;

        lock (_gate)
        {
            if (_completed)
                return;

            if (_target is null)
            {
                if (_pending.Count < _backlogCap)
                    _pending.Enqueue(logEvent);
                return;
            }

            _ch.Writer.TryWrite(new WorkItem(logEvent, _target));
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            _completed = true;
            _target = null;
            _pending.Clear();
            _ch.Writer.TryComplete();
        }
    }

    public async ValueTask FlushAsync()
    {
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await _ch.Writer.WriteAsync(new WorkItem(null, null, barrier)).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            await _readerTask.ConfigureAwait(false);
            return;
        }

        // Completion/cancellation of the reader must also release waiting flush callers.
        await Task.WhenAny(barrier.Task, _readerTask).ConfigureAwait(false);
        if (!barrier.Task.IsCompletedSuccessfully)
            await _readerTask.ConfigureAwait(false);
    }

    private async Task ReadLoop()
    {
        try
        {
            await foreach (WorkItem item in _ch.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                if (_cts.IsCancellationRequested)
                    break;
                if (item.Barrier is not null)
                    item.Barrier.TrySetResult();
                else
                    Write(item.Event!, item.Target!);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _ch.Writer.TryComplete();
            while (_ch.Reader.TryRead(out _)) { }
            await _sw.DisposeAsync().ConfigureAwait(false);
            _cts.Dispose();
        }
    }

    private void Write(LogEvent evt, Target target)
    {
        try
        {
            _sw.Reset();
            _fmt.Format(evt, _sw);
            string message = _sw.Finish();
            try
            {
                target.Sink?.OnMessage(new DiagnosticMessage(message));
            }
            catch
            {
            }
            try
            {
                target.Helper.WriteLine(message);
            }
            catch
            {
                // A finished test's output cannot be retried against another test's helper.
            }
        }
        catch
        {
            // Logging must not fail a test.
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            Complete();
            return new ValueTask(_disposeTask ??= DrainAsync());
        }
    }

    private async Task DrainAsync()
    {
        try
        {
            await _readerTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            try
            {
                await _cts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // The reader finished between the timeout and cancellation.
            }
            try
            {
                await _readerTask.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // A blocked helper cannot be interrupted. The reader owns resource cleanup.
            }
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
