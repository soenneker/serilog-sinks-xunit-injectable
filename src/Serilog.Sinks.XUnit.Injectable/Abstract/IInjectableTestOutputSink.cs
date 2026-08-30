using Serilog.Core;
using Serilog.Events;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Serilog.Sinks.XUnit.Injectable.Abstract;

/// <summary>
/// A non-blocking Serilog sink whose active xUnit <see cref="ITestOutputHelper"/> can be replaced while a test fixture remains shared.
/// Use one sink per shared logger and avoid injecting it concurrently from multiple tests.
/// </summary>
public interface IInjectableTestOutputSink : ILogEventSink, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Stops accepting new events and allows the queued reader to complete. Disposal already performs this step.
    /// </summary>
    void Complete();

    /// <summary>
    /// Replaces the output helper and optional diagnostic sink used for subsequent queued writes.
    /// </summary>
    /// <param name="testOutputHelper">The <see cref="ITestOutputHelper" /> that will be written to.</param>
    /// <param name="messageSink">The optional xUnit message sink that receives the same formatted events.</param>
    void Inject(ITestOutputHelper testOutputHelper, IMessageSink? messageSink = null);

    /// <summary>
    /// Enqueues an event without blocking. Events are buffered while no helper is available and may be dropped when bounded capacity is exhausted.
    /// </summary>
    /// <param name="logEvent">The event being logged</param>
    new void Emit(LogEvent logEvent);

    /// <summary>
    /// Completes the queue, drains it when possible, and releases the sink. This operation is idempotent.
    /// </summary>
    /// <returns></returns>
    new ValueTask DisposeAsync();
}
