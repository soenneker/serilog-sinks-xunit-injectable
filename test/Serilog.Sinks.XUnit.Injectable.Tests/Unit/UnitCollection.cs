using Xunit;

namespace Serilog.Sinks.XUnit.Injectable.Tests.Unit;

/// <summary>
/// This class has no code, and is never created. Its purpose is simply
/// to be the place to apply [CollectionDefinition] and all the
/// ICollectionFixture interfaces.
/// </summary>
[CollectionDefinition("UnitCollection")]
public class UnitCollection : ICollectionFixture<UnitFixture>
{
}