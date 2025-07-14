using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SampleApi;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Integration;

public class ApiFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> ApiFactory { get; set; } = null!;

    private InjectableTestOutputSink? _sink;

    public ValueTask InitializeAsync()
    {
        ApiFactory = new WebApplicationFactory<Program>();
        ApiFactory = ApiFactory.WithWebHostBuilder(builder =>
        {
            _sink = new InjectableTestOutputSink();

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IInjectableTestOutputSink>(_sink);
                services.AddSerilog((_, loggerConfiguration) =>
                {
                    loggerConfiguration.MinimumLevel.Verbose();
                    loggerConfiguration.WriteTo.InjectableTestOutput(_sink);
                    loggerConfiguration.Enrich.FromLogContext();
                });
            });
        });

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_sink != null)
            await _sink.DisposeAsync().ConfigureAwait(false);

        await ApiFactory.DisposeAsync().ConfigureAwait(false);
    }
}