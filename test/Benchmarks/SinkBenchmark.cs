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
    private QueueInjectableTestOutputSink _queueSink = null!;
    private ConcurrentInjectableTestOutputSink _conSink = null!;
    private BlockingCollectionInjectableTestOutputSink _blockingCollection = null!;
    private OriginalInjectableTestOutputSink _originalSink = null!;

    private readonly MockTestOutputHelper _helper = new();

    [GlobalSetup(Target = nameof(Original))]
    public void SetupOriginal()
    {
        BuildSinks(queue: false);
    }

    [GlobalSetup(Target = nameof(Queue))]
    public void SetupQueue()
    {
        BuildSinks(queue: true);
    }

    [GlobalSetup(Target = nameof(ConcurrentQueue))]
    public void SetupConcurrent()
    {
        BuildSinks(queue: false);
    }

    [GlobalSetup(Target = nameof(BlockingCollection))]
    public void SetupBlockingCollection()
    {
        BuildSinks(queue: false);
    }

    private void BuildSinks(bool queue)
    {
        MessageTemplate template = new MessageTemplateParser().Parse("Benchmark {Value}");
        _evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, exception: null, messageTemplate: template, properties: []);

        _originalSink = new OriginalInjectableTestOutputSink();
        _queueSink = new QueueInjectableTestOutputSink();
        _conSink = new ConcurrentInjectableTestOutputSink();
        _conSink = new ConcurrentInjectableTestOutputSink();
        _blockingCollection = new BlockingCollectionInjectableTestOutputSink();

        IInjectableTestOutputSink sink = queue ? _queueSink : _conSink;
        sink.Inject(_helper);
    }

    [Benchmark(Baseline = true)]
    public void Original()
    {
        Parallel.For(0, EventsTotal, new ParallelOptions { MaxDegreeOfParallelism = Degree }, _ => _originalSink.Emit(_evt));
    }

    [Benchmark]
    public void Queue()
    {
        Parallel.For(0, EventsTotal, new ParallelOptions {MaxDegreeOfParallelism = Degree}, _ => _queueSink.Emit(_evt));
    }

    [Benchmark]
    public void ConcurrentQueue()
    {
        Parallel.For(0, EventsTotal, new ParallelOptions {MaxDegreeOfParallelism = Degree}, _ => _conSink.Emit(_evt));
    }

    [Benchmark]
    public void BlockingCollection()
    {
        Parallel.For(0, EventsTotal, new ParallelOptions { MaxDegreeOfParallelism = Degree }, _ => _blockingCollection.Emit(_evt));
    }
}