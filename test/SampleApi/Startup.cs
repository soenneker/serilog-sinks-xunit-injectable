using SampleApi.Utils;

namespace SampleApi;

public class Startup
{
    public Startup()
    {
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SampleUtil>();

        services.AddControllers(options =>
        {
        });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, ILogger<Startup> logger)
    {
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}