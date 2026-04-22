using System;
using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Utils;

internal sealed class TUnitTestOutputHelper : ITestOutputHelper
{
    public void Write(string message)
    {
        Console.Write(message);
    }

    public void Write(string format, params object[] args)
    {
        Console.Write(format, args);
    }

    public void WriteLine(string message)
    {
        Console.WriteLine(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        Console.WriteLine(format, args);
    }
}
