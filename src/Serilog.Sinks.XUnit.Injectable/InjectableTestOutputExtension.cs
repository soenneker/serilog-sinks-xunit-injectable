using System;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.XUnit.Injectable.Sinks;
using Serilog.Sinks.XUnit.Injectable.Sinks.Abstract;
using Xunit.Abstractions;

namespace Serilog.Sinks.XUnit.Injectable;

/// <summary>
///     Adds the WriteTo.InjectableTestOutput() extension method to <see cref="LoggerConfiguration" />.
/// </summary>
public static class InjectableTestOutputExtension
{
    public const string DefaultConsoleOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    ///     Writes log events to <see cref="ITestOutputHelper" /> (and optionally, <see cref="IMessageSink" />) after it's been
    ///     injected.
    /// </summary>
    /// <param name="sink">The sink registered in DI</param>
    /// <param name="sinkConfiguration">Logger sink configuration.</param>
    /// <param name="restrictedToMinimumLevel">
    ///     The minimum level for
    ///     events passed through the sink. Ignored when <paramref name="levelSwitch" /> is specified.
    /// </param>
    /// <param name="levelSwitch">
    ///     A switch allowing the pass-through minimum level
    ///     to be changed at runtime.
    /// </param>
    /// <returns>Configuration object allowing method chaining.</returns>
    public static LoggerConfiguration InjectableTestOutput(this LoggerSinkConfiguration sinkConfiguration, IInjectableTestOutputSink sink,
        LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum, LoggingLevelSwitch? levelSwitch = null)
    {
        if (sinkConfiguration == null)
            throw new ArgumentNullException(nameof(sinkConfiguration));

        if (sink == null)
            throw new ArgumentNullException(nameof(sink));

        var config = sinkConfiguration.Sink(sink, restrictedToMinimumLevel, levelSwitch);
        return config;
    }
}