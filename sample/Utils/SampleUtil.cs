using Microsoft.Extensions.Logging;
using Serilog;

namespace SampleApi.Utils;

public class SampleUtil
{
    private readonly ILogger<SampleUtil> _logger;

    public SampleUtil(ILogger<SampleUtil> logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        Log.Logger.Information("----- Did some work (Serilog logger) -----");
        _logger.LogInformation("----- Did some work (Microsoft logger) -----");
    }
}