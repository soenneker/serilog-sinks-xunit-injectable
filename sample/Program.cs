using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace SampleApi;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }
        catch (Exception e)
        {
            Log.Error(e, "Stopped program because of exception");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Used for WebApplicationFactory, cannot delete, cannot change access, cannot change number of parameters.
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args);

        hostBuilder.UseSerilog((_, services, loggerConfig) =>
        {
            loggerConfig.MinimumLevel.Is(LogEventLevel.Verbose);
            loggerConfig.Enrich.FromLogContext();
        });

        hostBuilder.ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });

        return hostBuilder;
    }
}