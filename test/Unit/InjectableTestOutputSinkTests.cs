using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Serilog.Sinks.XUnit.Injectable;
using Serilog.Sinks.XUnit.Injectable.Tests.Utils;
using TUnit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Unit;

public sealed class InjectableTestOutputSinkTests
{
    [Test]
    public async Task Reinjecting_a_helper_should_not_leak_previous_helper_output()
    {
        var sink = new InjectableTestOutputSink();
        var first = new MockTestOutputHelper();
        var second = new MockTestOutputHelper();

        sink.Inject(first);

        for (var i = 0; i < 4_000; i++)
            sink.Emit(MockTestOutputHelper.CreateEvent($"test1-{i}"));

        sink.Inject(second);
        sink.Emit(MockTestOutputHelper.CreateEvent("test2"));

        await sink.DisposeAsync();

        second.Output.Should().NotContain(
            "test1-",
            because: "output written while the first helper was active must not leak into the second helper");
        second.Output.Should().Contain("test2");
        first.Output.Should().Contain("test1-");
    }

    [Test]
    public async Task Reinjecting_when_channel_is_full_should_not_deadlock()
    {
        var sink = new InjectableTestOutputSink();
        // A gated first helper blocks the reader on its first write, so the bounded channel stays
        // full while re-injection happens (the reader cannot drain). This guarantees the drain
        // barrier is written to a full channel.
        var first = new GatedTestOutputHelper();
        var second = new MockTestOutputHelper();

        sink.Inject(first);

        // Saturate the channel well beyond its 4096 capacity. The reader is gated, so it cannot
        // drain and the channel remains full when re-injection happens.
        for (var i = 0; i < 8_192; i++)
            sink.Emit(MockTestOutputHelper.CreateEvent($"test1-{i}"));

        // Re-injection must always complete, even while the channel is full. Run it on a separate
        // thread so the gated reader can be released after the drain barrier write has begun.
        // Under DropWrite the barrier write would drop the barrier (channel full) and re-injection
        // would hang forever; under Wait it blocks until the reader frees space, then completes.
        var reinject = Task.Run(() => sink.Inject(second));
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        first.Unlock();

        var completed = await Task.WhenAny(reinject, Task.Delay(TimeSpan.FromSeconds(10)));
        completed.Should().BeSameAs(reinject, because: "re-injection must not deadlock while the channel is full");
        await reinject;

        sink.Emit(MockTestOutputHelper.CreateEvent("test2"));
        await sink.DisposeAsync();

        second.Output.Should().NotContain(
            "test1-",
            because: "output written while the first helper was active must not leak into the second helper");
        second.Output.Should().Contain("test2");
    }

    [Test]
    public async Task Flushing_should_wait_until_all_queued_output_is_written()
    {
        var sink = new InjectableTestOutputSink();
        // A gated helper blocks the reader on its first write, so nothing is flushed while the
        // test produces output.
        var helper = new GatedTestOutputHelper();

        sink.Inject(helper);

        for (var i = 0; i < 4_000; i++)
            sink.Emit(MockTestOutputHelper.CreateEvent($"flush-{i}"));

        var flush = sink.FlushAsync().AsTask();
        helper.Output.Should().NotContain(
            "flush-3999",
            because: "the reader is gated; the flush must not complete before it is released");
        helper.Unlock();

        var completed = await Task.WhenAny(flush, Task.Delay(TimeSpan.FromSeconds(10)));
        completed.Should().BeSameAs(flush, because: "the flush must not deadlock while the reader is gated");
        await flush;

        helper.Output.Should().Contain(
            "flush-3999",
            because: "the flush must not complete until every enqueued event has been written");

        await sink.DisposeAsync();
    }

    [Test]
    public async Task Flushing_when_channel_is_full_should_not_deadlock()
    {
        var sink = new InjectableTestOutputSink();
        // A gated helper blocks the reader on its first write, so the bounded channel stays full
        // while the flush happens (the reader cannot drain). This guarantees the flush barrier is
        // written to a full channel.
        var helper = new GatedTestOutputHelper();

        sink.Inject(helper);

        // Saturate the channel well beyond its 4096 capacity. The reader is gated, so it cannot
        // drain and the channel remains full when the flush happens.
        for (var i = 0; i < 8_192; i++)
            sink.Emit(MockTestOutputHelper.CreateEvent($"flush-{i}"));

        // The flush barrier must always be enqueued, even while the channel is full. Wait for the
        // barrier write to begin (it blocks on the full channel under Wait mode), then release the
        // gated reader. Under DropWrite the barrier write would drop the barrier and the flush
        // would hang forever; under Wait it blocks until the reader frees space, then completes.
        var flush = sink.FlushAsync().AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        helper.Unlock();

        var completed = await Task.WhenAny(flush, Task.Delay(TimeSpan.FromSeconds(10)));
        completed.Should().BeSameAs(flush, because: "the flush must not deadlock while the channel is full");
        await flush;

        helper.Output.Should().Contain("flush-0");
        await sink.DisposeAsync();
    }

    [Test]
    public async Task Flushing_should_keep_the_sink_usable()
    {
        var sink = new InjectableTestOutputSink();
        var helper = new MockTestOutputHelper();

        sink.Inject(helper);

        sink.Emit(MockTestOutputHelper.CreateEvent("before-flush"));
        await sink.FlushAsync();
        helper.Output.Should().Contain("before-flush");

        // A flush must not close the channel; the sink remains usable afterwards.
        sink.Emit(MockTestOutputHelper.CreateEvent("after-flush"));
        await sink.FlushAsync();
        helper.Output.Should().Contain("after-flush");

        await sink.DisposeAsync();
    }

    [Test]
    public async Task Flushing_without_a_helper_or_after_disposal_should_not_throw()
    {
        var sink = new InjectableTestOutputSink();

        await sink.FlushAsync(); // no helper injected
        await sink.DisposeAsync();
        await sink.FlushAsync(); // sink disposed
    }
}
