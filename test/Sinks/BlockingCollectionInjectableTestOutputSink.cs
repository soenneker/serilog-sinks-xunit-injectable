using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Sinks;

public sealed class BlockingCollectionInjectableTestOutputSink : IInjectableTestOutputSink
{
    private const string _defaultTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{Exception}";

    private readonly MessageTemplateTextFormatter _formatter;
    private readonly BlockingCollection<LogEvent> _queue;
    private readonly Task _consumerTask;

    private ITestOutputHelper? _helper;
    private IMessageSink? _sink;

    [ThreadStatic] private static ReusableStringWriter? _threadWriter;

    public BlockingCollectionInjectableTestOutputSink(string outputTemplate = _defaultTemplate, IFormatProvider? formatProvider = null)
    {
        _formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        _queue = new BlockingCollection<LogEvent>();
        _consumerTask = Task.Factory.StartNew(ConsumeQueue, TaskCreationOptions.LongRunning);
    }

    private static ReusableStringWriter RentWriter() =>
        _threadWriter ??= new ReusableStringWriter();

    public void Inject(ITestOutputHelper helper, IMessageSink? sink = null)
    {
        ArgumentNullException.ThrowIfNull(helper);

        Volatile.Write(ref _helper, helper);
        Volatile.Write(ref _sink, sink);
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (_helper == null && _sink == null)
            return;

        try
        {
            _queue.Add(logEvent);
        }
        catch (InvalidOperationException)
        {
            // Queue closed
        }
    }

    private void ConsumeQueue()
    {
        try
        {
            foreach (LogEvent evt in _queue.GetConsumingEnumerable())
            {
                ITestOutputHelper? helper = Volatile.Read(ref _helper);

                if (helper == null)
                    continue;

                ReusableStringWriter writer = RentWriter();
                writer.Reset();
                _formatter.Format(evt, writer);
                string msg = writer.Finish();

                Volatile.Read(ref _sink)?.OnMessage(new DiagnosticMessage(msg));

                try
                {
                    helper.WriteLine(msg);
                }
                catch (InvalidOperationException)
                {
                    Volatile.Write(ref _helper, null);
                }
            }
        }
        catch
        {
            // ignored
        }
    }

    public async ValueTask DisposeAsync()
    {
        _queue.CompleteAdding();

        try
        {
            await _consumerTask.ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        if (_threadWriter != null)
            await _threadWriter.DisposeAsync();

        _queue.Dispose();
    }
}