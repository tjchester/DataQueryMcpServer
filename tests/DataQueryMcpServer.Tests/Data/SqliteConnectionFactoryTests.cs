using DataQueryMcpServer.Data;
using Microsoft.Data.Sqlite;

namespace DataQueryMcpServer.Tests.Data;

/// <summary>
/// Verifies the read-only backstop claimed in <see cref="SqliteConnectionFactory"/>: when readOnly is
/// true, writes are rejected by the driver itself, independent of anything the statement validator does.
/// </summary>
public class SqliteConnectionFactoryTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dataquerymcp-factory-tests-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = "CREATE TABLE widgets (id INTEGER PRIMARY KEY)";
        await create.ExecuteNonQueryAsync();
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
    public async Task CreateConnection_ReadOnlyTrue_RejectsWritesAtTheDriverLevel()
    {
        var factory = new SqliteConnectionFactory();
        await using var connection = factory.CreateConnection(_connectionString, readOnly: true);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO widgets DEFAULT VALUES";

        var act = () => command.ExecuteNonQueryAsync();

        await act.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task CreateConnection_ReadOnlyFalse_AllowsWrites()
    {
        var factory = new SqliteConnectionFactory();
        await using var connection = factory.CreateConnection(_connectionString, readOnly: false);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO widgets DEFAULT VALUES";

        await command.ExecuteNonQueryAsync();
    }
}
