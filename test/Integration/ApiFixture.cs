using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SampleApi;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Integration;

public class ApiFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> ApiFactory { get; set; } = null!;

    public ValueTask InitializeAsync()
    {
        ApiFactory = new WebApplicationFactory<Program>();
        ApiFactory = ApiFactory.WithWebHostBuilder(builder =>
        {
            var injectableTestOutputSink = new InjectableTestOutputSink();

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IInjectableTestOutputSink>(injectableTestOutputSink);
                services.AddSerilog((_, loggerConfiguration) =>
                {
                    loggerConfiguration.MinimumLevel.Verbose();
                    loggerConfiguration.WriteTo.InjectableTestOutput(injectableTestOutputSink);
                    loggerConfiguration.Enrich.FromLogContext();
                });
            });
        });

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await ApiFactory.DisposeAsync();
    }
}