using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Serilog.Sinks.XUnit.Injectable;

///<inheritdoc cref="IInjectableTestOutputSink"/>
public sealed class InjectableTestOutputSink : IInjectableTestOutputSink
{
    private readonly Lock _lock = new(); // guards flush+write
    private readonly ConcurrentQueue<LogEvent> _cache = new();
    private readonly MessageTemplateTextFormatter _fmt;

    private volatile ITestOutputHelper? _helper; // set once, then read
    private volatile IMessageSink? _sink;

    private const string _defaultTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{Exception}";

    [ThreadStatic] private static StringBuilder? _sb;

    [ThreadStatic] private static ReusableStringWriter? _sw;

    public InjectableTestOutputSink(string outputTemplate = _defaultTemplate, IFormatProvider? formatProvider = null)
    {
        _fmt = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
    }

    public void Inject(ITestOutputHelper helper, IMessageSink? sink = null)
    {
        ArgumentNullException.ThrowIfNull(helper);

        // one short, guaranteed exit scope
        using Lock.Scope _ = _lock.EnterScope();

        _helper = helper;
        _sink = sink;
        FlushLocked();
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        // FAST-PATH: helper not yet available – enqueue without locking
        ITestOutputHelper? helperSnapshot = _helper; // direct volatile read

        if (helperSnapshot is null)
        {
            _cache.Enqueue(logEvent);
            return;
        }


        using Lock.Scope _ = _lock.EnterScope();

        // Flush anything another thread buffered before we obtained the scope
        FlushLocked();
        WriteLocked(logEvent);
    }

    private void FlushLocked()
    {
        while (_cache.TryDequeue(out LogEvent? queued))
            WriteLocked(queued);
    }

    private void WriteLocked(LogEvent evt)
    {
        // rent / reset thread-local buffers
        StringBuilder sb = _sb ??= new StringBuilder(256);
        sb.Clear();

        ReusableStringWriter sw = _sw ??= new ReusableStringWriter(sb);
        sw.Reset();

        _fmt.Format(evt, sw);
        var message = sb.ToString(); // single unavoidable alloc

        _sink?.OnMessage(new DiagnosticMessage(message));

        try
        {
            _helper?.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
            /* test already finished – swallow */
        }
    }
}