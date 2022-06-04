using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SampleApi.Utils;
using Serilog.Sinks.XUnit.Injectable.Sinks;
using Serilog.Sinks.XUnit.Injectable.Sinks.Abstract;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Unit;

public class UnitFixture : IAsyncLifetime
{
    public ServiceProvider ServiceProvider { get; set; } = default!;

    protected IServiceCollection Services { get; set; }

    public UnitFixture()
    {
        var injectableTestOutputSink = new InjectableTestOutputSink();

        Services = new ServiceCollection();

        Services.AddSingleton<IInjectableTestOutputSink>(injectableTestOutputSink);
        Services.AddSingleton<SampleUtil>();

        ILogger serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.InjectableTestOutput(injectableTestOutputSink)
            .Enrich.FromLogContext()
            .CreateLogger();

        Log.Logger = serilogLogger;

        Services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });
    }

    public virtual Task InitializeAsync()
    {
        ServiceProvider = Services.BuildServiceProvider();

        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
    }
}