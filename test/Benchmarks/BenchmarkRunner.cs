using System.Threading.Tasks;
using BenchmarkDotNet.Reports;
using Soenneker.Benchmarking.Extensions.Summary;
using Soenneker.Tests.Benchmark;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Benchmarks;

public sealed class BenchmarkRunner : BenchmarkTest
{
    public BenchmarkRunner(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

   // [Fact]
    public async ValueTask SinkBenchmark()
    {
        Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<SinkBenchmark>(DefaultConf);

        await summary.OutputSummaryToLog(OutputHelper, CancellationToken);
    }
}