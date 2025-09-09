using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Utils.ReusableStringWriter;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Sinks;

/// <inheritdoc cref="IInjectableTestOutputSink"/>
public sealed class QueueInjectableTestOutputSink : IInjectableTestOutputSink
{
    private readonly Lock _lock = new();
    private readonly Queue<LogEvent> _cache = new();
    private readonly MessageTemplateTextFormatter _fmt;

    private volatile ITestOutputHelper? _helper;
    private volatile IMessageSink? _sink;

    private const string _defaultTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{Exception}";

    [ThreadStatic] private static ReusableStringWriter? _sw;

    public QueueInjectableTestOutputSink(string outputTemplate = _defaultTemplate, IFormatProvider? formatProvider = null)
    {
        _fmt = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
    }

    public void Inject(ITestOutputHelper helper, IMessageSink? sink = null)
    {
        ArgumentNullException.ThrowIfNull(helper);

        using Lock.Scope _ = _lock.EnterScope();

        _helper = helper;
        _sink = sink;

        FlushLocked(helper);
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        using Lock.Scope _ = _lock.EnterScope();

        // FAST-PATH: helper not yet available – enqueue and return
        if (_helper is null)
        {
            _cache.Enqueue(logEvent);
            return;
        }

        FlushLocked(_helper);
        WriteLocked(logEvent, _helper);
    }

    private void FlushLocked(ITestOutputHelper? outputHelper)
    {
        while (_cache.Count > 0)
            WriteLocked(_cache.Dequeue(), outputHelper);
    }

    private void WriteLocked(LogEvent evt, ITestOutputHelper? outputHelper)
    {
        // rent / reset thread-local buffers
        ReusableStringWriter sw = _sw ??= new ReusableStringWriter();
        sw.Reset();

        _fmt.Format(evt, sw);
        string message = sw.Finish();

        _sink?.OnMessage(new DiagnosticMessage(message));

        try
        {
            outputHelper?.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
            // test already finished – swallow
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}