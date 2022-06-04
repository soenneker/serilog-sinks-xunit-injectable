using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SampleApi;
using Serilog.Sinks.XUnit.Injectable.Sinks;
using Serilog.Sinks.XUnit.Injectable.Sinks.Abstract;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Integration;

public class ApiFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> ApiFactory { get; set; } = default!;

    public Task InitializeAsync()
    {
        ApiFactory = new WebApplicationFactory<Program>();
        ApiFactory = ApiFactory.WithWebHostBuilder(builder =>
        {
            var injectableTestOutputSink = new InjectableTestOutputSink();

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IInjectableTestOutputSink>(injectableTestOutputSink);
            });

            builder.UseSerilog((_, loggerConfiguration) =>
            {
                loggerConfiguration.MinimumLevel.Verbose();
                loggerConfiguration.WriteTo.InjectableTestOutput(injectableTestOutputSink);
                loggerConfiguration.Enrich.FromLogContext();
            });
        });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await ApiFactory.DisposeAsync();
    }
}