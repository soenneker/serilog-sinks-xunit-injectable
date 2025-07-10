using System.Net.Http;
using System.Threading.Tasks;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Integration;

[Collection("ApiCollection")]
public class ApiTests
{
    private readonly HttpClient _client;

    public ApiTests(ApiFixture fixture, ITestOutputHelper testOutputHelper)
    {
        var outputSink = (IInjectableTestOutputSink)fixture.ApiFactory.Services.GetService(typeof(IInjectableTestOutputSink))!;
        outputSink.Inject(testOutputHelper);

        _client = fixture.ApiFactory.CreateClient();
    }

    [Fact]
    public async Task Get_should_have_log_messages_and_be_successful()
    {
        HttpResponseMessage response = await _client.GetAsync("/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Get_concurrent()
    {
        Task<HttpResponseMessage> task1= _client.GetAsync("/", TestContext.Current.CancellationToken);
        Task<HttpResponseMessage> task2 = _client.GetAsync("/", TestContext.Current.CancellationToken);
        Task<HttpResponseMessage> task3 = _client.GetAsync("/", TestContext.Current.CancellationToken);

        await Task.WhenAll(task1, task2, task3);
    }
}