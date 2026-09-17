using DataQueryMcpServer.Configuration;
using DataQueryMcpServer.Infrastructure;

namespace DataQueryMcpServer.Tests.Infrastructure;

public class ProviderRegistryTests
{
    private sealed record Item(DbProvider Provider);

    [Fact]
    public void Resolve_RegisteredProvider_ReturnsMatchingItem()
    {
        var sqlServerItem = new Item(DbProvider.SqlServer);
        var sqliteItem = new Item(DbProvider.Sqlite);
        var registry = new ProviderRegistry<Item>([sqlServerItem, sqliteItem], i => i.Provider);

        registry.Resolve(DbProvider.Sqlite).Should().BeSameAs(sqliteItem);
        registry.Resolve(DbProvider.SqlServer).Should().BeSameAs(sqlServerItem);
    }

    [Fact]
    public void Resolve_UnregisteredProvider_ThrowsNotSupportedException()
    {
        var registry = new ProviderRegistry<Item>([new Item(DbProvider.Sqlite)], i => i.Provider);

        var act = () => registry.Resolve(DbProvider.SqlServer);

        act.Should().Throw<NotSupportedException>();
    }
}
