using SampleApi.Utils;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using System.Threading.Tasks;
using AwesomeAssertions;
using Soenneker.Tests.HostedUnit;
using Serilog.Sinks.XUnit.Injectable.Tests.Utils;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Unit;

[ClassDataSource<UnitHost>(Shared = SharedType.PerTestSession)]
public sealed class SampleUtilTests : HostedUnitTest
{
    private readonly SampleUtil _util;

    public SampleUtilTests(UnitHost host) : base(host)
    {
        var outputSink = (IInjectableTestOutputSink)host.ServiceProvider.GetService(typeof(IInjectableTestOutputSink))!;
        outputSink.Inject(new TUnitTestOutputHelper());

        _util = (SampleUtil)host.ServiceProvider.GetService(typeof(SampleUtil))!;
    }

    [Test]
    public void DoWork_should_result_with_log_messages()
    {
        _util.DoWork();
    }

    [Test]
    public void DoWork_loop_result_with_log_messages()
    {
        for (var i = 0; i < 10; i++)
        {
            _util.DoWork();
        }
    }

    [Test]
    public void DoWork_loop_with_inject_with_log_messages()
    {
        for (var i = 0; i < 10; i++)
        {
            var outputSink = (IInjectableTestOutputSink)Host.ServiceProvider.GetService(typeof(IInjectableTestOutputSink))!;
            outputSink.Inject(new TUnitTestOutputHelper());

            _util.DoWork();
        }
    }

    [Test]
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
