using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Sinks.XUnit.Injectable.Abstract;
using Soenneker.Utils.ReusableStringWriter;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;
namespace Serilog.Sinks.XUnit.Injectable.Tests.Sinks;
/// <inheritdoc cref="IInjectableTestOutputSink"/>
public sealed class ChannelInjectableTestOutputSink : IInjectableTestOutputSink
{
    private const string _defaultTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{Exception}";
    private readonly MessageTemplateTextFormatter _fmt;
    private readonly Channel<LogEvent> _ch = Channel.CreateUnbounded<LogEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });
    private readonly Task _readerTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly ReusableStringWriter _sw = new();
    private readonly Queue<LogEvent> _pending = new();
    private volatile ITestOutputHelper? _helper;
    private volatile IMessageSink? _sink;
    private int _disposed;
    public ChannelInjectableTestOutputSink(string outputTemplate = _defaultTemplate, IFormatProvider? formatProvider = null)
    {
        _fmt = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        _readerTask = Task.Run(ReadLoop);
    }
    public void Complete()
    {
        throw new NotImplementedException();
    }
    public void Inject(ITestOutputHelper helper, IMessageSink? sink = null)
    {
        ArgumentNullException.ThrowIfNull(helper);
        _helper = helper;
        _sink = sink;
    }
    public void Emit(LogEvent logEvent)
    {
        if (Volatile.Read(ref _disposed) == 0)
            _ch.Writer.TryWrite(logEvent);
    }
    private async Task ReadLoop()
    {
        await foreach (LogEvent evt in _ch.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
        {
            ITestOutputHelper? helper = _helper;
            if (helper is null)
            {
                _pending.Enqueue(evt);
                continue;
            }
            while (_pending.Count > 0)
            {
                Write(_pending.Dequeue(), helper);
            }
            Write(evt, helper);
        }
    }
    private void Write(LogEvent evt, ITestOutputHelper helper)
    {
        _sw.Reset();
        _fmt.Format(evt, _sw);
        string message = _sw.Finish();
        _sink?.OnMessage(new DiagnosticMessage(message));
        try
        {
            helper.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
            _helper = null;
            _pending.Enqueue(evt);
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _ch.Writer.TryComplete();
        try
        {
            await _readerTask.ConfigureAwait(false);
        }
        finally
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _cts.Dispose();
            await _sw.DisposeAsync().ConfigureAwait(false);
        }
    }
    public void Dispose()
    {
        _readerTask.Dispose();
        _cts.Dispose();
        _sw.Dispose();
    }
}