using System;
using System.IO;
using System.Threading;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Utils;

/// <summary>
/// A test output helper that blocks the reader on its first write until <see cref="Unlock"/> is
/// called. While locked, the reader cannot drain the sink's bounded channel, which keeps it full.
/// </summary>
internal sealed class GatedTestOutputHelper : ITestOutputHelper
{
    private readonly ManualResetEventSlim _gate = new(false);
    private readonly StringWriter _writer = new();

    public string Output => _writer.ToString();

    public void Unlock() => _gate.Set();

    public void Write(string message)
    {
        _gate.Wait();
        _writer.Write(message);
    }

    public void Write(string format, params object[] args)
    {
        _gate.Wait();
        _writer.Write(format, args);
    }

    public void WriteLine(string message)
    {
        _gate.Wait();
        _writer.WriteLine(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        _gate.Wait();
        _writer.WriteLine(format, args);
    }
}
