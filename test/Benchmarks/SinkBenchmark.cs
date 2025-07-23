using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Tests.Sinks;
using Serilog.Sinks.XUnit.Injectable.Tests.Utils;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Benchmarks;

[ThreadingDiagnoser]
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, RuntimeMoniker.Net90, launchCount: 1, warmupCount: 1, iterationCount: 1)]
public class SinkBenchmark
{
    // each thread will execute this many emits per iteration
    [Params(1, 8, 16)] public int Degree;
    [Params(10, 10_000, 100_000)] public int EventsTotal;

    // BenchmarkDotNet will spin up a fresh *instance of this class* per thread,
    // so the fields below are already thread-local; no further protection needed.
    private LogEvent _evt = null!;

    private IInjectableTestOutputSink _orig = null!;
    private IInjectableTestOutputSink _queue = null!;
    private IInjectableTestOutputSink _cc = null!;
    private IInjectableTestOutputSink _block = null!;
    private IInjectableTestOutputSink _chan = null!;

    private readonly MockTestOutputHelper _helper = new();

    [GlobalSetup]
    public void Setup()
    {
        MessageTemplate template = new MessageTemplateParser().Parse("Benchmark {Value}");
        _evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, exception: null, messageTemplate: template, properties: []);

        _orig = new OriginalInjectableTestOutputSink();
        _queue = new QueueInjectableTestOutputSink();
        _cc = new ConcurrentInjectableTestOutputSink();
        _block = new BlockingCollectionInjectableTestOutputSink();
        _chan = new ChannelInjectableTestOutputSink();

        foreach (IInjectableTestOutputSink s in new[] {_orig, _queue, _cc, _block, _chan})
            s.Inject(_helper);
    }

    private void Produce(IInjectableTestOutputSink sink) =>
        Parallel.For(0, EventsTotal, new ParallelOptions {MaxDegreeOfParallelism = Degree}, _ => sink.Emit(_evt));

    private async Task RunAsync(IInjectableTestOutputSink sink)
    {
        Produce(sink);
        await sink.DisposeAsync();
    }

    [Benchmark]
    public async Task Queue() => await RunAsync(_queue);

    [Benchmark]
    public async Task ConcurrentQueue() => await RunAsync(_cc);

    [Benchmark]
    public async Task BlockingCollection() => await RunAsync(_block);

    [Benchmark]
    public async Task Channel() => await RunAsync(_chan);
}