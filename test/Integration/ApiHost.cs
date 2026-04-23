using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SampleApi;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using Soenneker.TestHosts.Unit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Integration;

public sealed class ApiHost : UnitTestHost
{
    public WebApplicationFactory<Program> ApiFactory { get; private set; } = null!;

    public override Task InitializeAsync()
    {
        InjectableTestOutputSink sink = new();

        ApiFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IInjectableTestOutputSink>(sink);
                services.AddSerilog((_, loggerConfiguration) =>
                {
                    loggerConfiguration.MinimumLevel.Verbose();
                    loggerConfiguration.WriteTo.InjectableTestOutput(sink);
                    loggerConfiguration.Enrich.FromLogContext();
                });
            });
        });

        return base.InitializeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await ApiFactory.DisposeAsync().ConfigureAwait(false);

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
