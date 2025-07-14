using SampleApi.Utils;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using System.Threading.Tasks;
using AwesomeAssertions;
using Serilog.Sinks.XUnit.Injectable.Tests.Utils;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Unit;

[Collection("UnitCollection")]
public class SampleUtilTests
{
    private readonly SampleUtil _util;

    private readonly UnitFixture _fixture;
    private readonly ITestOutputHelper _testOutputHelper;

    public SampleUtilTests(UnitFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
        var outputSink = (IInjectableTestOutputSink) fixture.ServiceProvider.GetService(typeof(IInjectableTestOutputSink))!;
        outputSink.Inject(testOutputHelper);

        _util = (SampleUtil) fixture.ServiceProvider.GetService(typeof(SampleUtil))!;
    }

    [Fact]
    public void DoWork_should_result_with_log_messages()
    {
        _util.DoWork();
    }

    [Fact]
    public void DoWork_loop_result_with_log_messages()
    {
        for (int i = 0; i < 10; i++)
        {
            _util.DoWork();
        }
    }

    [Fact]
    public void DoWork_loop_with_inject_with_log_messages()
    {
        for (int i = 0; i < 10; i++)
        {
            var outputSink = (IInjectableTestOutputSink) _fixture.ServiceProvider.GetService(typeof(IInjectableTestOutputSink))!;
            outputSink.Inject(_testOutputHelper);

            _util.DoWork();
        }
    }

    [Fact]
    public async Task Logs_Are_Isolated_Per_Test()
    {
        var output1 = new MockTestOutputHelper();
        var output2 = new MockTestOutputHelper();

        var sink1 = new InjectableTestOutputSink();
        sink1.Inject(output1);
        sink1.Emit(MockTestOutputHelper.CreateEvent("test1"));
        await sink1.DisposeAsync();

        var sink2 = new InjectableTestOutputSink();
        sink2.Inject(output2);
        sink2.Emit(MockTestOutputHelper.CreateEvent("test2"));
        await sink2.DisposeAsync();

        output1.Output.Should().Contain("test1");
        output1.Output.Should().NotContain("test2");
        output2.Output.Should().NotContain("test1");
        output2.Output.Should().Contain("test2");
    }
}