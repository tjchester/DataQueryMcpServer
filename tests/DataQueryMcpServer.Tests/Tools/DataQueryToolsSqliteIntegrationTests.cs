using DataQueryMcpServer.Configuration;
using DataQueryMcpServer.Data;
using DataQueryMcpServer.Infrastructure;
using DataQueryMcpServer.Tools;
using DataQueryMcpServer.Validation;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using NSubstitute;

namespace DataQueryMcpServer.Tests.Tools;

/// <summary>
/// Exercises DataQueryTools with real, wired-together components (validator, classifier, connection
/// factory, schema reader) against a temp SQLite file, to prove the pieces compose correctly rather
/// than just testing each in isolation with mocks.
/// </summary>
public class DataQueryToolsSqliteIntegrationTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dataquerymcp-tools-tests-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE widgets (id INTEGER PRIMARY KEY, name TEXT NOT NULL)";
            await create.ExecuteNonQueryAsync();
        }

        await using var seed = connection.CreateCommand();
        seed.CommandText = "INSERT INTO widgets (name) VALUES ('a'), ('b'), ('c')";
        await seed.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task RunQueryAsync_Select_ReturnsExpectedColumnsAndRows()
    {
        var tools = CreateTools(QueryMode.ReadOnly, maxRows: 100);

        var result = await tools.RunQueryAsync("Main", "SELECT id, name FROM widgets ORDER BY id", CancellationToken.None);

        result.Columns.Should().Equal("id", "name");
        result.Rows.Should().HaveCount(3);
        result.Rows[0]["name"].Should().Be("a");
        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task RunQueryAsync_MaxRowsLowerThanResultSet_TruncatesResults()
    {
        var tools = CreateTools(QueryMode.ReadOnly, maxRows: 2);

        var result = await tools.RunQueryAsync("Main", "SELECT id FROM widgets ORDER BY id", CancellationToken.None);

        result.Rows.Should().HaveCount(2);
        result.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task RunQueryAsync_ReadOnlyMode_RejectsInsertAndLeavesDataUnchanged()
    {
        var tools = CreateTools(QueryMode.ReadOnly, maxRows: 100);

        var act = () => tools.RunQueryAsync("Main", "INSERT INTO widgets (name) VALUES ('d')", CancellationToken.None);

        await act.Should().ThrowAsync<McpException>();
        (await CountWidgetsAsync()).Should().Be(3);
    }

    [Fact]
    public async Task RunQueryAsync_ReadWriteMode_AllowsInsert()
    {
        var tools = CreateTools(QueryMode.ReadWrite, maxRows: 100);

        await tools.RunQueryAsync("Main", "INSERT INTO widgets (name) VALUES ('d')", CancellationToken.None);

        (await CountWidgetsAsync()).Should().Be(4);
    }

    [Fact]
    public async Task GetSchemaAsync_ReturnsTableAndColumnMetadata()
    {
        var tools = CreateTools(QueryMode.ReadOnly, maxRows: 100);

        var schema = await tools.GetSchemaAsync("Main", CancellationToken.None);

        var widgets = schema.Should().ContainSingle(t => t.Name == "widgets").Subject;
        widgets.Columns.Select(c => c.Name).Should().Contain(["id", "name"]);
    }

    private async Task<long> CountWidgetsAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM widgets";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private DataQueryTools CreateTools(QueryMode mode, int maxRows)
    {
        var serverOptions = new DatabaseServerOptions
        {
            Mode = mode,
            MaxRows = maxRows,
            CommandTimeoutSeconds = 5,
            Connections =
            {
                ["Main"] = new ConnectionSettings { Provider = DbProvider.Sqlite, ConnectionString = _connectionString }
            }
        };

        var options = Substitute.For<IOptionsMonitor<DatabaseServerOptions>>();
        options.CurrentValue.Returns(serverOptions);

        var connectionFactories =
            new ProviderRegistry<IDbConnectionFactory>([new SqliteConnectionFactory()], f => f.Provider);
        var schemaReaders = new ProviderRegistry<ISchemaReader>([new SqliteSchemaReader()], r => r.Provider);
        var classifiers =
            new ProviderRegistry<IStatementClassifier>([new SqliteStatementClassifier()], c => c.Provider);
        var validator = new SqlStatementValidator(classifiers, options);

        return new DataQueryTools(options, connectionFactories, schemaReaders, validator);
    }
}
