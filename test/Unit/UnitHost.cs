using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using SampleApi.Utils;
using Soenneker.TestHosts.Unit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Unit;

public sealed class UnitHost : UnitTestHost
{
    public override Task InitializeAsync()
    {
        SetupIoC(Services);

        return base.InitializeAsync();
    }

    private static void SetupIoC(IServiceCollection services)
    {
        InjectableTestOutputSink sink = new();

        services.AddSingleton<IInjectableTestOutputSink>(sink);
        services.AddSingleton<SampleUtil>();

        ILogger serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.InjectableTestOutput(sink)
            .Enrich.FromLogContext()
            .CreateLogger();

        Log.Logger = serilogLogger;

        services.AddLogging(builder => { builder.AddSerilog(dispose: false); });
    }
}
