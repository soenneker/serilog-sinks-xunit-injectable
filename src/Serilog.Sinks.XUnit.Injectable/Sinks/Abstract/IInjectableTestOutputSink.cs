using Serilog.Core;
using Xunit.Abstractions;

namespace Serilog.Sinks.XUnit.Injectable.Sinks.Abstract;

/// <summary>
/// Use as a Singleton
/// </summary>
public interface IInjectableTestOutputSink : ILogEventSink
{
    /// <summary>
    ///     Call this as soon as you have a new instance of the testOutputHelper (usually at the beginning of a xUnit test
    ///     class)
    /// </summary>
    /// <param name="testOutputHelper">The <see cref="ITestOutputHelper" /> that will be written to.</param>
    /// <param name="messageSink"> The xUnit message sink for diagnostic messages.</param>
    void Inject(ITestOutputHelper testOutputHelper, IMessageSink? messageSink = null);
}