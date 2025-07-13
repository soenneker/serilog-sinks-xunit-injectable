using System.IO;
using System.Text;

namespace Serilog.Sinks.XUnit.Injectable;

public sealed class ReusableStringWriter : StringWriter
{
    private readonly StringBuilder _sb;

    public ReusableStringWriter() : base(new StringBuilder(256))
    {
        _sb = GetStringBuilder();
    }

    public void Reset() => _sb.Clear();

    public string Finish() => _sb.ToString();
}