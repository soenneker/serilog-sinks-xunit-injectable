using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.XUnit.Injectable;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Tests.Benchmarks;
using System;
using System.Threading.Tasks;

[ThreadingDiagnoser]
[SimpleJob(RunStrategy.Throughput, RuntimeMoniker.Net90, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class SinkBenchmark
{
    // each thread will execute this many emits per iteration
    [Params(1, 4, 8, 16)] public int Degree;
    [Params(10, 100, 1_000, 10_000, 100_000)] public int EventsTotal;

    // BenchmarkDotNet will spin up a fresh *instance of this class* per thread,
    // so the fields below are already thread-local; no further protection needed.
    private LogEvent _evt = null!;
    private InjectableTestOutputSink _queueSink = null!;
    private ConcurrentInjectableTestOutputSink _conSink = null!;
    private readonly MockTestOutputHelper _helper = new();

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

    private void BuildSinks(bool queue)
    {
        MessageTemplate template = new MessageTemplateParser().Parse("Benchmark {Value}");
        _evt = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, exception: null, messageTemplate: template, properties: []);

        _queueSink = new InjectableTestOutputSink();
        _conSink = new ConcurrentInjectableTestOutputSink();

        IInjectableTestOutputSink sink = queue ? _queueSink : _conSink;
        sink.Inject(_helper);
    }

    /*================  BENCHMARKS  ================*/

    [Benchmark(Baseline = true)]
    public void Queue()
    {
        Parallel.For(0, EventsTotal, new ParallelOptions {MaxDegreeOfParallelism = Degree}, _ => _queueSink.Emit(_evt));
    }

    [Benchmark]
    public void ConcurrentQueue()
    {
        Parallel.For(0, EventsTotal, new ParallelOptions {MaxDegreeOfParallelism = Degree}, _ => _conSink.Emit(_evt));
    }

    /*================  CLEANUP  ================*/

    [GlobalCleanup]
    public void Cleanup()
    {
    }
}