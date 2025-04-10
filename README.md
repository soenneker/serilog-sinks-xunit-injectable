[![](https://img.shields.io/nuget/v/Serilog.Sinks.XUnit.Injectable.svg?style=for-the-badge)](https://www.nuget.org/packages/Serilog.Sinks.XUnit.Injectable/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/serilog-sinks-xunit-injectable/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/serilog-sinks-xunit-injectable/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Serilog.Sinks.XUnit.Injectable.svg?style=for-the-badge)](https://www.nuget.org/packages/Serilog.Sinks.XUnit.Injectable/)

# Serilog.Sinks.XUnit.Injectable
### The injectable, Serilog xUnit test output sink

Leverage xUnit's [`TestOutputHelper`](https://xunit.net/docs/capturing-output) across tests that share state

### Common use cases
- Integration tests (i.e. [`WebApplicationFactory`](https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-6.0))
- Unit tests that utilize Dependency Injection in a fixture

## Why?
When running a suite of tests, it can be expensive to build a new DI `ServiceProvider`, and even more so, a `WebApplicationFactory`. Hence, these can be stored in a xUnit fixture and reused across tests. 

xUnit provides a new `TestOutputHelper` per test, and so even if you register it as a sink initially, the next test will not capture/output messages from the services inside the provider.

This library addresses that issue by allowing for the `TestOutputHelper` from each test to be "injected" into the fixture as it's running. It also maintains the context of each test in the appropriate test runner window.

Examples are provided for both Unit and Integration tests. For brevity, the actual injection is shown in the constructor of the test class in the examples below, but you'll probably want to leverage a base class.

### xUnit Compatibility

- Version 3.x: Supports xUnit 2.9.
- Latest Version: Fully supports xUnit 3.

## Installation

```
dotnet add package Serilog.Sinks.XUnit.Injectable
```

---
### Example: `WebApplicationFactory` "integration tests"
---
```csharp
public class ApiFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> ApiFactory { get; set; } = default!;

    public Task InitializeAsync()
    {
        ApiFactory = new WebApplicationFactory<Program>();
        ApiFactory = ApiFactory.WithWebHostBuilder(builder =>
        {
            // Instantiate the sink, with any configuration (like outputTemplate, formatProvider)
            var injectableTestOutputSink = new InjectableTestOutputSink();

            builder.ConfigureServices(services =>
            {
                // Register the sink as a singleton
                services.AddSingleton<IInjectableTestOutputSink>(injectableTestOutputSink); 
            });

            builder.UseSerilog((_, loggerConfiguration) =>
            {
                // Add the sink to the logger configuration
                loggerConfiguration.WriteTo.InjectableTestOutput(injectableTestOutputSink);
            });
        });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await ApiFactory.DisposeAsync();
    }
}
```

```csharp
[Collection("ApiCollection")]
public class ApiTests
{
    private readonly HttpClient _client;

    public ApiTests(ApiFixture fixture, ITestOutputHelper testOutputHelper)
    {
        var outputSink = (IInjectableTestOutputSink)fixture.ApiFactory.Services.GetService(typeof(IInjectableTestOutputSink))!;
        outputSink.Inject(testOutputHelper); // <-- inject the new ITestOutputHelper into the sink

        _client = fixture.ApiFactory.CreateClient();
    }

    [Fact]
    public async Task Get_should_have_log_messages_and_be_successful()
    {
        HttpResponseMessage response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();
    }
}
```

---
### Example: `ServiceProvider` "Fixtured unit tests"
---
```csharp
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
            .WriteTo.InjectableTestOutput(injectableTestOutputSink)
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

```

```csharp
[Collection("UnitCollection")]
public class SampleUtilTests
{
    private readonly SampleUtil _util;

    public SampleUtilTests(UnitFixture fixture, ITestOutputHelper testOutputHelper)
    {
        var outputSink = (IInjectableTestOutputSink)fixture.ServiceProvider.GetService(typeof(IInjectableTestOutputSink));
        outputSink.Inject(testOutputHelper);

        _util = (SampleUtil)fixture.ServiceProvider.GetService(typeof(SampleUtil));
    }

    [Fact]
    public void DoWork_should_result_with_log_messages()
    {
        _util.DoWork();
    }
}
```
---
### `SampleUtil`
---
```csharp
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
```