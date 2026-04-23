using System.Net.Http;
using System.Threading.Tasks;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Tests.Utils;
using Soenneker.Tests.HostedUnit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Integration;

[ClassDataSource<ApiHost>(Shared = SharedType.PerTestSession)]
public sealed class ApiTests : HostedUnitTest
{
    private readonly HttpClient _client;

    public ApiTests(ApiHost host) : base(host)
    {
        var outputSink = (IInjectableTestOutputSink)host.ApiFactory.Services.GetService(typeof(IInjectableTestOutputSink))!;
        outputSink.Inject(new TUnitTestOutputHelper());

        _client = host.ApiFactory.CreateClient();
    }

    [Test]
    public async Task Get_should_have_log_messages_and_be_successful()
    {
        HttpResponseMessage response = await _client.GetAsync("/", System.Threading.CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task Get_concurrent()
    {
        Task<HttpResponseMessage> task1 = _client.GetAsync("/", System.Threading.CancellationToken.None);
        Task<HttpResponseMessage> task2 = _client.GetAsync("/", System.Threading.CancellationToken.None);
        Task<HttpResponseMessage> task3 = _client.GetAsync("/", System.Threading.CancellationToken.None);

        await Task.WhenAll(task1, task2, task3);
    }
}
