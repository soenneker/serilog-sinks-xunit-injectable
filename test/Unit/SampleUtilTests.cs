using SampleApi.Utils;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Xunit;
using Xunit.Abstractions;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Unit;

[Collection("UnitCollection")]
public class SampleUtilTests
{
    private readonly SampleUtil _util;

    public SampleUtilTests(UnitFixture fixture, ITestOutputHelper testOutputHelper)
    {
        var outputSink = (IInjectableTestOutputSink)fixture.ServiceProvider.GetService(typeof(IInjectableTestOutputSink))!;
        outputSink.Inject(testOutputHelper);

        _util = (SampleUtil)fixture.ServiceProvider.GetService(typeof(SampleUtil))!;
    }

    [Fact]
    public void DoWork_should_result_with_log_messages()
    {
        _util.DoWork();
    }
}