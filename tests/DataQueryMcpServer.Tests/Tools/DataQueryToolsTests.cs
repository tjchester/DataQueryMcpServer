using DataQueryMcpServer.Configuration;
using DataQueryMcpServer.Data;
using DataQueryMcpServer.Infrastructure;
using DataQueryMcpServer.Tools;
using DataQueryMcpServer.Validation;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using NSubstitute;

namespace DataQueryMcpServer.Tests.Tools;

public class DataQueryToolsTests
{
    private readonly IDbConnectionFactory _connectionFactory = Substitute.For<IDbConnectionFactory>();
    private readonly ISchemaReader _schemaReader = Substitute.For<ISchemaReader>();
    private readonly ISqlStatementValidator _validator = Substitute.For<ISqlStatementValidator>();
    private readonly DatabaseServerOptions _serverOptions = new();

    public DataQueryToolsTests()
    {
        _connectionFactory.Provider.Returns(DbProvider.Sqlite);
        _schemaReader.Provider.Returns(DbProvider.Sqlite);

        // Default to "allowed" so tests only need to configure the validator when they care about
        // denial. NSubstitute uses last-configured-wins, so a test's own Validate(...).Returns(...)
        // set up before calling CreateTools() correctly overrides this default.
        _validator.Validate(Arg.Any<string>(), Arg.Any<DbProvider>()).Returns(StatementValidationResult.Allowed);
    }

    [Fact]
    public void ListConnections_ReturnsConfiguredConnectionsSortedByName_WithoutConnectionStrings()
    {
        _serverOptions.Connections["Zeta"] = new ConnectionSettings
        {
            Provider = DbProvider.Sqlite,
            ConnectionString = "Data Source=zeta.db",
            Description = "Zeta db"
        };
        _serverOptions.Connections["Alpha"] = new ConnectionSettings
        {
            Provider = DbProvider.SqlServer,
            ConnectionString = "Server=.;Database=Alpha;",
            Description = null
        };

        var tools = CreateTools();

        var result = tools.ListConnections();

        result.Select(c => c.Name).Should().Equal("Alpha", "Zeta");
        result.Single(c => c.Name == "Zeta").Provider.Should().Be(nameof(DbProvider.Sqlite));
        result.Single(c => c.Name == "Zeta").Description.Should().Be("Zeta db");
    }

    [Fact]
    public async Task GetSchemaAsync_UnknownConnection_ThrowsMcpException()
    {
        var tools = CreateTools();

        var act = () => tools.GetSchemaAsync("does-not-exist", CancellationToken.None);

        await act.Should().ThrowAsync<McpException>();
    }

    [Fact]
    public async Task GetSchemaAsync_KnownConnection_DelegatesToMatchingSchemaReader()
    {
        AddConnection("Main", DbProvider.Sqlite, "Data Source=main.db");
        var expected = new List<TableSchema> { new("widgets", [new ColumnSchema("id", "INTEGER", false)]) };
        _schemaReader.GetSchemaAsync("Data Source=main.db", Arg.Any<CancellationToken>()).Returns(expected);

        var tools = CreateTools();

        var result = await tools.GetSchemaAsync("Main", CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task RunQueryAsync_UnknownConnection_ThrowsMcpException()
    {
        var tools = CreateTools();

        var act = () => tools.RunQueryAsync("does-not-exist", "SELECT 1", CancellationToken.None);

        await act.Should().ThrowAsync<McpException>();
    }

    [Fact]
    public async Task RunQueryAsync_ValidatorRejectsStatement_ThrowsAndNeverOpensConnection()
    {
        AddConnection("Main", DbProvider.Sqlite, "Data Source=main.db");
        _validator.Validate("DELETE FROM widgets", DbProvider.Sqlite)
            .Returns(StatementValidationResult.Denied("Only read-only (SELECT) statements are allowed."));

        var tools = CreateTools();

        var act = () => tools.RunQueryAsync("Main", "DELETE FROM widgets", CancellationToken.None);

        (await act.Should().ThrowAsync<McpException>()).Which.Message.Should().Contain("read-only");
        _connectionFactory.DidNotReceive().CreateConnection(Arg.Any<string>(), Arg.Any<bool>());
    }

    private void AddConnection(string name, DbProvider provider, string connectionString)
    {
        _serverOptions.Connections[name] = new ConnectionSettings { Provider = provider, ConnectionString = connectionString };
    }

    private DataQueryTools CreateTools()
    {
        var options = Substitute.For<IOptionsMonitor<DatabaseServerOptions>>();
        options.CurrentValue.Returns(_serverOptions);

        var connectionFactories = new ProviderRegistry<IDbConnectionFactory>([_connectionFactory], f => f.Provider);
        var schemaReaders = new ProviderRegistry<ISchemaReader>([_schemaReader], r => r.Provider);

        return new DataQueryTools(options, connectionFactories, schemaReaders, _validator);
    }
}
