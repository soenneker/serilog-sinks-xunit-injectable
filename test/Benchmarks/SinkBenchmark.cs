using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.XUnit.Injectable.Tests.Sinks;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Benchmarks;

[ThreadingDiagnoser]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 1, iterationCount: 1)]
public class SinkBenchmark
{
    [Params(1, 8, 16)] public int Degree;
    [Params(10, 10_000, 100_000)] public int EventsTotal;
    private LogEvent _evt = null!;
    private readonly NullOutputHelper _helper = new();

    [GlobalSetup]
    public void Setup()
    {
        MessageTemplate template = new MessageTemplateParser().Parse("Benchmark event");
        _evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, template, []);
    }

    private void Produce(ILogEventSink sink) =>
        Parallel.For(0, EventsTotal, new ParallelOptions {MaxDegreeOfParallelism = Degree}, _ => sink.Emit(_evt));

    // Each invocation includes construction, production, and drain; no disposed sink is reused.
    private async Task RunAsync(IBenchmarkOutputSink sink)
    {
        await using (sink)
        {
            sink.Inject(_helper);
            Produce(sink);
        }
    }

    [Benchmark]
    public Task Queue() => RunAsync(new QueueInjectableTestOutputSink());

    [Benchmark]
    public Task ConcurrentQueue() => RunAsync(new ConcurrentInjectableTestOutputSink());

    [Benchmark]
    public Task BlockingCollection() => RunAsync(new BlockingCollectionInjectableTestOutputSink());

    [Benchmark]
    public Task Channel() => RunAsync(new ChannelInjectableTestOutputSink());

    // The production sink is bounded and may drop events under load, unlike unbounded variants.
    [Benchmark]
    public async Task Production()
    {
        await using var sink = new InjectableTestOutputSink();
        sink.Inject(_helper);
        Produce(sink);
        await sink.FlushAsync();
    }

    private sealed class NullOutputHelper : ITestOutputHelper
    {
        public string Output => string.Empty;
        public void Write(string message) { }
        public void Write(string format, params object[] args) { }
        public void WriteLine(string message) { }
        public void WriteLine(string format, params object[] args) { }
    }
}
