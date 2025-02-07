using System;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Xunit;
using Xunit.Sdk;

namespace Serilog.Sinks.XUnit.Injectable.Extensions;

/// <summary>
///     Adds the WriteTo.InjectableTestOutput() extension method to <see cref="LoggerConfiguration" />.
/// </summary>
public static class InjectableTestOutputExtension
{
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

        return sinkConfiguration.Sink(sink, restrictedToMinimumLevel, levelSwitch);
    }
}