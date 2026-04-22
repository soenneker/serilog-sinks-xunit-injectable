using System.Threading.Tasks;
using BenchmarkDotNet.Reports;
using Soenneker.Benchmarking.Extensions.Summary;
using Soenneker.Tests.Benchmark;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Benchmarks;

public sealed class BenchmarkRunner : BenchmarkTest
{
    public BenchmarkRunner() : base()
    {
    }

    //[Test]
    public async ValueTask SinkBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<SinkBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(OutputHelper, CancellationToken);
    }
}