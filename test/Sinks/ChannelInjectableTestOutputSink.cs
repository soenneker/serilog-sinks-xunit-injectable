using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Soenneker.Utils.ReusableStringWriter;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Sinks;

/// <inheritdoc cref="IInjectableTestOutputSink"/>
public sealed class ChannelInjectableTestOutputSink : IInjectableTestOutputSink
{
    private const string _defaultTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{Exception}";

    private readonly MessageTemplateTextFormatter _fmt;
    private readonly Channel<LogEvent> _ch = Channel.CreateUnbounded<LogEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });
    private readonly Task _readerTask;
    private readonly CancellationTokenSource _cts = new();

    private readonly ReusableStringWriter _sw = new();

    // Shared with producers/reader – volatile for safe publication
    private volatile ITestOutputHelper? _helper;
    private volatile IMessageSink? _sink;

    // Only the reader touches this; safe without locks.
    private readonly Queue<LogEvent> _pending = new();

    private int _disposed;

    public ChannelInjectableTestOutputSink(string outputTemplate = _defaultTemplate, IFormatProvider? formatProvider = null)
    {
        _fmt = new MessageTemplateTextFormatter(outputTemplate, formatProvider);

        _readerTask = Task.Run(ReadLoop);
    }

    public void Inject(ITestOutputHelper helper, IMessageSink? sink = null)
    {
        ArgumentNullException.ThrowIfNull(helper);

        _helper = helper;
        _sink = sink;
    }

    public void Emit(LogEvent logEvent)
    {
        if (Volatile.Read(ref _disposed) == 0)
            _ch.Writer.TryWrite(logEvent);
    }

    private async Task ReadLoop()
    {
        await foreach (LogEvent evt in _ch.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
        {
            ITestOutputHelper? helper = _helper; // volatile read

            if (helper is null)
            {
                _pending.Enqueue(evt); // buffer until helper arrives
                continue;
            }

            // first, flush any backlog that accumulated pre‑inject
            while (_pending.Count > 0)
            {
                Write(_pending.Dequeue(), helper);
            }

            Write(evt, helper);
        }
    }

    private void Write(LogEvent evt, ITestOutputHelper helper)
    {
        _sw.Reset();
        _fmt.Format(evt, _sw);
        string message = _sw.Finish();

        _sink?.OnMessage(new DiagnosticMessage(message));

        try
        {
            helper.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
            // Helper became invalid (test finished) – cache the event
            _helper = null;

            _pending.Enqueue(evt);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // 1) Tell the reader no more items are coming
        _ch.Writer.TryComplete();

        try
        {
            // 2) Let the reader finish formatting & flushing
            await _readerTask.ConfigureAwait(false);
        }
        finally
        {
            // 3) Now it’s safe to cancel / dispose
            await _cts.CancelAsync().ConfigureAwait(false);     // optional – keeps teardown symmetric
            _cts.Dispose();
            await _sw.DisposeAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _readerTask.Dispose();
        _cts.Dispose();
        _sw.Dispose();
    }
}