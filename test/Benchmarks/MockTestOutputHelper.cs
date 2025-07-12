using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Benchmarks;

internal sealed class MockTestOutputHelper : ITestOutputHelper
{
    public void Write(string message)
    {
    }

    public void Write(string format, params object[] args)
    {
    }

    public void WriteLine(string message)
    {
    }

    public void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));

    public string Output { get; }
}