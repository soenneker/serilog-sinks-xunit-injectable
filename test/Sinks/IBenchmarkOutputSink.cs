using System;
using Serilog.Core;
using Xunit;
using Xunit.Sdk;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Sinks;

public interface IBenchmarkOutputSink : ILogEventSink, IAsyncDisposable, IDisposable
{
    void Inject(ITestOutputHelper helper, IMessageSink? sink = null);
}
