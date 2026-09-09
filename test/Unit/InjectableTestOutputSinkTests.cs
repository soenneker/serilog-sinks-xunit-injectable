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
}
