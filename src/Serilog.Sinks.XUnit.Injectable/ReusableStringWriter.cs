using System.IO;
using System.Text;

namespace Serilog.Sinks.XUnit.Injectable;

internal sealed class ReusableStringWriter : StringWriter
{
    public ReusableStringWriter(StringBuilder sb) : base(sb)
    {
    }

    public void Reset() => GetStringBuilder().Clear();
}