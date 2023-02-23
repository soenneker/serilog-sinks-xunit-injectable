using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Serilog.Sinks.XUnit.Injectable;

/// <inheritdoc cref="IInjectableTestOutputSink"/>
public class InjectableTestOutputSink : IInjectableTestOutputSink
{
    private readonly Stack<LogEvent> _cachedLogEvents;
    private readonly ITextFormatter _textFormatter;
    private IMessageSink? _messageSink;
    private ITestOutputHelper? _testOutputHelper;

    /// <summary>
    ///     Use this ctor for injecting into the DI container
    /// </summary>
    /// <param name="formatProvider">
    ///     Controls the rendering of log events into text, for example to log JSON. To
    ///     control plain text formatting, use the overload that accepts an output template.
    /// </param>
    public InjectableTestOutputSink(IFormatProvider? formatProvider = null)
    {
        _cachedLogEvents = new Stack<LogEvent>();

        _textFormatter = new MessageTemplateTextFormatter(InjectableTestOutputExtension.DefaultConsoleOutputTemplate, formatProvider);
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
        }
        else
        {
            if (_cachedLogEvents.Any())
            {
                while (_cachedLogEvents.Count > 0)
                {
                    LogEvent oldEvent = _cachedLogEvents.Pop();

                    Write(oldEvent);
                }
            }

            Write(logEvent);
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
        _testOutputHelper?.WriteLine(message);
    }
}