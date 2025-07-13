using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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

    /// <summary>
    ///     Use this ctor for injecting into the DI container
    /// </summary>
    /// <param name="outputTemplate">A message template describing the format used to write to the sink.
    /// the default is <code>"[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"</code>.</param>
    /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    public OriginalInjectableTestOutputSink(string outputTemplate = _defaultConsoleOutputTemplate, IFormatProvider? formatProvider = null)
    {
        _cachedLogEvents = new Stack<LogEvent>();

        _textFormatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
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

    /// <summary>
    ///     Emits the provided log event from a sink
    /// </summary>
    /// <param name="logEvent">The event being logged</param>
    private void Write(LogEvent logEvent)
    {
        if (logEvent == null)
            throw new ArgumentNullException(nameof(logEvent));

        var renderSpace = new StringWriter();
        _textFormatter.Format(logEvent, renderSpace);

        string message = renderSpace.ToString().Trim();

        _messageSink?.OnMessage(new DiagnosticMessage(message));

        try
        {
            _testOutputHelper?.WriteLine(message);
        }
        catch (InvalidOperationException ex)
        {
            // Typically no test is active
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}