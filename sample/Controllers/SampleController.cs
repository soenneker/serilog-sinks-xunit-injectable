using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleApi.Utils;

namespace SampleApi.Controllers;

[ApiController]
[Route("")]
public class SampleController
{
    private readonly SampleUtil _sampleUtil;

    public SampleController(SampleUtil sampleUtil)
    {
        _sampleUtil = sampleUtil;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        _sampleUtil.DoWork();
        return new OkObjectResult(null);
    }
}