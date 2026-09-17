using DataQueryMcpServer.Configuration;
using DataQueryMcpServer.Data;
using DataQueryMcpServer.Infrastructure;
using DataQueryMcpServer.Tools;
using DataQueryMcpServer.Validation;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using NSubstitute;

namespace DataQueryMcpServer.Tests.Tools;

/// <summary>
/// Exercises DataQueryTools with real, wired-together components (validator, T-SQL classifier,
/// connection factory, schema reader) against a local SQL Server instance. Creates and drops a
/// throwaway database per test run so nothing touches real data.
///
/// Requires a SQL Server instance reachable at "localhost" via Windows Integrated Security (matching
/// the "LocalSqlServer" sample connection in appsettings.json), with permission to CREATE/DROP DATABASE.
/// These tests fail rather than skip when no such instance is available, since they are meant to run
/// on a developer machine that has one, not in an environment-agnostic CI pipeline.
///
/// Unlike the SQLite equivalents, there is no dedicated driver-level read-only backstop test here:
/// ApplicationIntent=ReadOnly (set by SqlServerConnectionFactory) only affects read-routing against an
/// Always On Availability Group; on a standalone instance like this one it does not itself block writes.
/// The statement validator is therefore the sole enforcement layer these tests exercise for SQL Server.
/// </summary>
[Trait("Category", "RequiresLocalSqlServer")]
public class DataQueryToolsSqlServerIntegrationTests : IAsyncLifetime
{
    private const string MasterConnectionString =
        "Server=localhost;Database=master;Integrated Security=true;TrustServerCertificate=true;";

    private readonly string _databaseName = $"DataQueryMcpServerTests_{Guid.NewGuid():N}";
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await using (var master = new SqlConnection(MasterConnectionString))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await create.ExecuteNonQueryAsync();
        }

        _connectionString =
            $"Server=localhost;Database={_databaseName};Integrated Security=true;TrustServerCertificate=true;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using (var createTable = connection.CreateCommand())
        {
            createTable.CommandText = "CREATE TABLE dbo.Widgets (Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(100) NOT NULL)";
            await createTable.ExecuteNonQueryAsync();
        }

        await using var seed = connection.CreateCommand();
        seed.CommandText = "INSERT INTO dbo.Widgets (Name) VALUES ('a'), ('b'), ('c')";
        await seed.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var master = new SqlConnection(MasterConnectionString);
        await master.OpenAsync();

        await using (var setSingleUser = master.CreateCommand())
        {
            setSingleUser.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
            await setSingleUser.ExecuteNonQueryAsync();
        }

        await using var drop = master.CreateCommand();
        drop.CommandText = $"DROP DATABASE [{_databaseName}]";
        await drop.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task RunQueryAsync_Select_ReturnsExpectedColumnsAndRows()
    {
        var tools = CreateTools(QueryMode.ReadOnly, maxRows: 100);

        var result = await tools.RunQueryAsync("Main", "SELECT Id, Name FROM dbo.Widgets ORDER BY Id", CancellationToken.None);

        result.Columns.Should().Equal("Id", "Name");
        result.Rows.Should().HaveCount(3);
        result.Rows[0]["Name"].Should().Be("a");
        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task RunQueryAsync_MaxRowsLowerThanResultSet_TruncatesResults()
    {
        var tools = CreateTools(QueryMode.ReadOnly, maxRows: 2);

        var result = await tools.RunQueryAsync("Main", "SELECT Id FROM dbo.Widgets ORDER BY Id", CancellationToken.None);

        result.Rows.Should().HaveCount(2);
        result.Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task RunQueryAsync_ReadOnlyMode_RejectsInsertAndLeavesDataUnchanged()
    {
        var tools = CreateTools(QueryMode.ReadOnly, maxRows: 100);

        var act = () => tools.RunQueryAsync("Main", "INSERT INTO dbo.Widgets (Name) VALUES ('d')", CancellationToken.None);

        await act.Should().ThrowAsync<McpException>();
        (await CountWidgetsAsync()).Should().Be(3);
    }

    [Fact]
    public async Task RunQueryAsync_ReadWriteMode_AllowsInsert()
    {
        var tools = CreateTools(QueryMode.ReadWrite, maxRows: 100);

        await tools.RunQueryAsync("Main", "INSERT INTO dbo.Widgets (Name) VALUES ('d')", CancellationToken.None);

        (await CountWidgetsAsync()).Should().Be(4);
    }

    [Fact]
    public async Task GetSchemaAsync_ReturnsTableAndColumnMetadata()
    {
        var tools = CreateTools(QueryMode.ReadOnly, maxRows: 100);

        var schema = await tools.GetSchemaAsync("Main", CancellationToken.None);

        var widgets = schema.Should().ContainSingle(t => t.Name == "dbo.Widgets").Subject;
        widgets.Columns.Select(c => c.Name).Should().Contain(["Id", "Name"]);
    }

    private async Task<int> CountWidgetsAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.Widgets";
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private DataQueryTools CreateTools(QueryMode mode, int maxRows)
    {
        var serverOptions = new DatabaseServerOptions
        {
            Mode = mode,
            MaxRows = maxRows,
            CommandTimeoutSeconds = 30,
            Connections =
            {
                ["Main"] = new ConnectionSettings { Provider = DbProvider.SqlServer, ConnectionString = _connectionString }
            }
        };

        var options = Substitute.For<IOptionsMonitor<DatabaseServerOptions>>();
        options.CurrentValue.Returns(serverOptions);

        var connectionFactories =
            new ProviderRegistry<IDbConnectionFactory>([new SqlServerConnectionFactory()], f => f.Provider);
        var schemaReaders = new ProviderRegistry<ISchemaReader>([new SqlServerSchemaReader()], r => r.Provider);
        var classifiers =
            new ProviderRegistry<IStatementClassifier>([new TSqlStatementClassifier()], c => c.Provider);
        var validator = new SqlStatementValidator(classifiers, options);

        return new DataQueryTools(options, connectionFactories, schemaReaders, validator);
    }
}
