[![](https://img.shields.io/nuget/v/Serilog.Sinks.XUnit.Injectable.svg?style=for-the-badge)](https://www.nuget.org/packages/Serilog.Sinks.XUnit.Injectable/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/serilog-sinks-xunit-injectable/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/serilog-sinks-xunit-injectable/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Serilog.Sinks.XUnit.Injectable.svg?style=for-the-badge)](https://www.nuget.org/packages/Serilog.Sinks.XUnit.Injectable/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/serilog-sinks-xunit-injectable/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/serilog-sinks-xunit-injectable/actions/workflows/codeql.yml)

# Serilog.Sinks.XUnit.Injectable

A Serilog sink whose active xUnit `ITestOutputHelper` can be replaced for each test while the logger and test fixture remain shared.

## Installation

```bash
dotnet add package Serilog.Sinks.XUnit.Injectable
```

## Configure the shared fixture

Create one sink for the lifetime of the shared service provider or `WebApplicationFactory`, register that same instance in DI, and pass it to Serilog:

```csharp
using Serilog;
using Serilog.Sinks.XUnit.Injectable;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Extensions;

var outputSink = new InjectableTestOutputSink(
    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{Exception}");

services.AddSingleton<IInjectableTestOutputSink>(outputSink);

Log.Logger = new LoggerConfiguration()
    .WriteTo.InjectableTestOutput(outputSink)
    .CreateLogger();
```

The sink must be the same instance used by the shared logger. Creating a new sink for each test will not redirect logs already produced by services in the fixture.

## Inject each test's output helper

Inject the helper before the test starts work:

```csharp
public sealed class ApiTests
{
    private readonly HttpClient _client;

    public ApiTests(ApiFixture fixture, ITestOutputHelper output)
    {
        IInjectableTestOutputSink sink =
            fixture.Services.GetRequiredService<IInjectableTestOutputSink>();

        sink.Inject(output);
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Get_returns_success()
    {
        using HttpResponseMessage response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();
    }
}
```

Calling `Inject` replaces the helper and optional diagnostic message sink used for subsequent writes. Tests sharing this sink should not run concurrently: a later injection can redirect queued output from another test.

Events received before a helper is available are buffered and written when a helper is injected. If xUnit rejects a write because that test has ended, the sink clears the helper and retains that event for the next injection.

## Capacity and disposal

Logging never blocks producers. The sink uses a bounded 4,096-event channel and retains at most 2,048 events while no helper is available. Events beyond either capacity are silently dropped, so this is test-output plumbing rather than durable log storage.

Dispose or flush the Serilog logger during fixture teardown. `Dispose` and `DisposeAsync` are idempotent; asynchronous disposal allows up to two seconds for a normal drain before cancellation. `Complete()` stops accepting events early and is normally unnecessary when the logger owns the sink.

An optional xUnit `IMessageSink` can be supplied alongside the helper:

```csharp
sink.Inject(output, diagnosticMessageSink);
```

Formatted events are sent to both destinations while a helper is active.
