using Serilog.Core;
using Serilog.Events;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Serilog.Sinks.XUnit.Injectable.Abstract;

/// <summary>
/// A sink to direct Serilog output to the XUnit test output via dependency injection <para/>
/// Use as a Singleton
/// </summary>
public interface IInjectableTestOutputSink : ILogEventSink, IAsyncDisposable
{
    /// <summary>
    ///     Call this as soon as you have a new instance of the testOutputHelper (usually at the beginning of a xUnit test
    ///     class)
    /// </summary>
    /// <param name="testOutputHelper">The <see cref="ITestOutputHelper" /> that will be written to.</param>
    /// <param name="messageSink"> The xUnit message sink for diagnostic messages.</param>
    void Inject(ITestOutputHelper testOutputHelper, IMessageSink? messageSink = null);

    /// <summary>
    ///     Emits the event unless testOutputHelper is null. In that case, it caches it for later (and then emits them all when
    ///     it's not) <para/>
    ///     Will NOT cache IMessageSink log events.
    /// </summary>
    /// <param name="logEvent">The event being logged</param>
    new void Emit(LogEvent logEvent);

    /// <summary>
    /// This is idempotent... but you should avoid calling it explicitly because it'll get disposed from Serilog if it's been registered.  
    /// </summary>
    /// <returns></returns>
    new ValueTask DisposeAsync();
}