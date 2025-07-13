using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Utils;

internal sealed class MockTestOutputHelper : ITestOutputHelper
{
    private readonly StringWriter _writer = new();

    public string Output => _writer.ToString();

    public void Write(string message)
    {
        _writer.Write(message);
    }

    public void Write(string format, params object[] args)
    {
        _writer.Write(format, args);
    }

    public void WriteLine(string message)
    {
        _writer.WriteLine(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        _writer.WriteLine(format, args);
    }

    public static LogEvent CreateEvent(string message)
    {
        var sw = new StringWriter();

        var formatter = new MessageTemplateTextFormatter("[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}", null);

        var parser = new MessageTemplateParser();
        MessageTemplate template = parser.Parse(message);

        var evt = new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            null,
            template,
            new List<LogEventProperty>());

        formatter.Format(evt, sw);

        return evt;
    }
}