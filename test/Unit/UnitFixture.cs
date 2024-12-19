using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SampleApi.Utils;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Unit;

public class UnitFixture : IAsyncLifetime
{
    public ServiceProvider ServiceProvider { get; set; } = default!;

    protected IServiceCollection Services { get; set; }

    public UnitFixture()
    {
        Services = new ServiceCollection();

        var injectableTestOutputSink = new InjectableTestOutputSink();

        Services.AddSingleton<IInjectableTestOutputSink>(injectableTestOutputSink);
        Services.AddSingleton<SampleUtil>();

        ILogger serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.InjectableTestOutput(injectableTestOutputSink)
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

    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return ServiceProvider.DisposeAsync();
    }
}