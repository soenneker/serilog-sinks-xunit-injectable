using Microsoft.Extensions.DependencyInjection;
using SampleApi.Utils;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Unit;

public class UnitFixture : IAsyncLifetime
{
    public ServiceProvider ServiceProvider { get; set; } = null!;

    protected IServiceCollection Services { get; set; }

    private readonly InjectableTestOutputSink _sink;

    public UnitFixture()
    {
        Services = new ServiceCollection();

        _sink = new InjectableTestOutputSink();

        Services.AddSingleton<IInjectableTestOutputSink>(_sink);
        Services.AddSingleton<SampleUtil>();

        ILogger serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.InjectableTestOutput(_sink)
            .Enrich.FromLogContext()
            .CreateLogger();

        Log.Logger = serilogLogger;

        Services.AddLogging(builder => { builder.AddSerilog(dispose: true); });
    }

    public virtual ValueTask InitializeAsync()
    {
        ServiceProvider = Services.BuildServiceProvider();

        return ValueTask.CompletedTask;
    }

    public virtual async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await _sink.DisposeAsync().ConfigureAwait(false);

        await ServiceProvider.DisposeAsync().ConfigureAwait(false);
    }
}