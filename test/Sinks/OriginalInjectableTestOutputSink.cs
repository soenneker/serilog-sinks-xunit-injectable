using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;
namespace Serilog.Sinks.XUnit.Injectable.Tests.Sinks;
/// <inheritdoc cref="IInjectableTestOutputSink"/>
public sealed class OriginalInjectableTestOutputSink : IInjectableTestOutputSink
{
    private readonly Stack<LogEvent> _cachedLogEvents;
    private readonly MessageTemplateTextFormatter _textFormatter;
    private IMessageSink? _messageSink;
    private ITestOutputHelper? _testOutputHelper;
    private const string _defaultConsoleOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
    public OriginalInjectableTestOutputSink(string outputTemplate = _defaultConsoleOutputTemplate, IFormatProvider? formatProvider = null)
    {
        _cachedLogEvents = new Stack<LogEvent>();
        _textFormatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
    }
    public void Complete()
    {
        throw new NotImplementedException();
    }
    public void Inject(ITestOutputHelper testOutputHelper, IMessageSink? messageSink = null)
    {
        _testOutputHelper = testOutputHelper;
        _messageSink = messageSink;
    }
    public void Emit(LogEvent logEvent)
    {
        if (_testOutputHelper == null)
        {
            _cachedLogEvents.Push(logEvent);
            return;
        }
        FlushCachedLogEvents();
        Write(logEvent);
    }
    private void FlushCachedLogEvents()
    {
        while (_cachedLogEvents.Count > 0)
        {
            Write(_cachedLogEvents.Pop());
        }
    }
    private void Write(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        using var renderSpace = new StringWriter();
        _textFormatter.Format(logEvent, renderSpace);
        string message = renderSpace.ToString().Trim();
        _messageSink?.OnMessage(new DiagnosticMessage(message));
        try
        {
            _testOutputHelper?.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
        }
    }
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
    public void Dispose()
    {
    }
}