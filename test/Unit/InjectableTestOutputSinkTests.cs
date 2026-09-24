using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Serilog.Sinks.XUnit.Injectable.Tests.Utils;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Unit;

public sealed class InjectableTestOutputSinkTests
{
    [Test]
    public async Task Queued_events_keep_their_original_helper()
    {
        await VerifyReinjection(false);
    }

    [Test]
    public async Task Failed_old_helper_does_not_replace_or_contaminate_new_helper()
    {
        await VerifyReinjection(true);
    }

    private static async Task VerifyReinjection(bool fail)
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var first = new OutputHelper(() =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException();
            if (fail)
                throw new InvalidOperationException("Test finished");
        });
        var second = new OutputHelper();
        var firstDiagnostic = new DiagnosticSink();
        var secondDiagnostic = new DiagnosticSink();
        await using var sink = new InjectableTestOutputSink();
        sink.Inject(first, firstDiagnostic);
        sink.Emit(MockTestOutputHelper.CreateEvent("first-active"));
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            sink.Emit(MockTestOutputHelper.CreateEvent("first-queued"));
            sink.Inject(second, secondDiagnostic);
            sink.Emit(MockTestOutputHelper.CreateEvent("second-event"));
            Task flush = sink.FlushAsync().AsTask();
            flush.IsCompleted.Should().BeFalse();
            release.Set();
            await flush.WaitAsync(TimeSpan.FromSeconds(10));
            second.Output.Should().Contain("second-event").And.NotContain("first-");
            firstDiagnostic.Output.Should().Contain("first-queued").And.NotContain("second-event");
            secondDiagnostic.Output.Should().Contain("second-event").And.NotContain("first-");
            if (!fail)
                first.Output.Should().Contain("first-active").And.Contain("first-queued").And.NotContain("second-event");
        }
        finally
        {
            release.Set();
        }
    }

    [Test]
    public async Task Startup_backlog_flushes_after_injection_without_another_emit()
    {
        await using IInjectableTestOutputSink sink = new InjectableTestOutputSink();
        var helper = new OutputHelper();
        sink.Emit(MockTestOutputHelper.CreateEvent("startup"));
        sink.Inject(helper);
        await sink.FlushAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        helper.Output.Should().Contain("startup");
        sink.Emit(MockTestOutputHelper.CreateEvent("after-flush"));
        await sink.FlushAsync();
        helper.Output.Should().Contain("after-flush");
    }

    [Test]
    public async Task Complete_preserves_queued_output_and_flush_remains_callable()
    {
        await using var sink = new InjectableTestOutputSink();
        var helper = new OutputHelper();
        sink.Inject(helper);
        sink.Emit(MockTestOutputHelper.CreateEvent("before-complete"));
        sink.Complete();
        sink.Emit(MockTestOutputHelper.CreateEvent("after-complete"));
        await sink.FlushAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        helper.Output.Should().Contain("before-complete").And.NotContain("after-complete");
        await sink.DisposeAsync();
        await sink.FlushAsync();
    }

    [Test]
    public async Task Flush_waits_for_capacity_when_the_channel_is_full()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var helper = new OutputHelper(() =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
        });
        await using var sink = new InjectableTestOutputSink();
        sink.Inject(helper);
        sink.Emit(MockTestOutputHelper.CreateEvent("active"));
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            var evt = MockTestOutputHelper.CreateEvent("queued");
            for (int i = 0; i < 5000; i++)
                sink.Emit(evt);
            Task flush = sink.FlushAsync().AsTask();
            flush.IsCompleted.Should().BeFalse();
            release.Set();
            await flush.WaitAsync(TimeSpan.FromSeconds(10));
            helper.Output.Split(Environment.NewLine).Length.Should().Be(4097);
        }
        finally
        {
            release.Set();
        }
    }

    [Test]
    public async Task Benchmark_invocations_can_repeat()
    {
        var benchmark = new Benchmarks.SinkBenchmark { Degree = 2, EventsTotal = 10 };
        benchmark.Setup();
        for (int i = 0; i < 2; i++)
        {
            await benchmark.Queue();
            await benchmark.ConcurrentQueue();
            await benchmark.BlockingCollection();
            await benchmark.Channel();
            await benchmark.Production();
        }
    }

    private sealed class DiagnosticSink : IMessageSink
    {
        private readonly ConcurrentQueue<string> _lines = new();
        public string Output => string.Join(Environment.NewLine, _lines);
        public bool OnMessage(IMessageSinkMessage message)
        {
            if (message is IDiagnosticMessage diagnostic)
                _lines.Enqueue(diagnostic.Message);
            return true;
        }
    }

    private sealed class OutputHelper(Action? beforeWrite = null) : ITestOutputHelper
    {
        private readonly ConcurrentQueue<string> _lines = new();
        public string Output => string.Join(Environment.NewLine, _lines);
        public void Write(string message) => WriteLine(message);
        public void Write(string format, params object[] args) => WriteLine(string.Format(format, args));
        public void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));
        public void WriteLine(string message)
        {
            beforeWrite?.Invoke();
            _lines.Enqueue(message);
        }
    }
}
