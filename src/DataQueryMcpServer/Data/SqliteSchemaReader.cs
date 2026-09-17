using DataQueryMcpServer.Configuration;
using Microsoft.Data.Sqlite;

namespace DataQueryMcpServer.Data;

public sealed class SqliteSchemaReader : ISchemaReader
{
    public DbProvider Provider => DbProvider.Sqlite;

    public async Task<IReadOnlyList<TableSchema>> GetSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString) { Mode = SqliteOpenMode.ReadOnly };

        await using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tableNames = new List<string>();
        await using (var tablesCommand = connection.CreateCommand())
        {
            tablesCommand.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

            await using var tablesReader = await tablesCommand.ExecuteReaderAsync(cancellationToken);
            while (await tablesReader.ReadAsync(cancellationToken))
            {
                tableNames.Add(tablesReader.GetString(0));
            }
        }

        var tables = new List<TableSchema>(tableNames.Count);
        foreach (var tableName in tableNames)
        {
            var columns = new List<ColumnSchema>();

            await using var columnsCommand = connection.CreateCommand();
            // Table name comes from sqlite_master above (not caller input), and PRAGMA does not support
            // parameters, so it is quoted rather than parameterized.
            columnsCommand.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"")}\")";

            await using var columnsReader = await columnsCommand.ExecuteReaderAsync(cancellationToken);
            while (await columnsReader.ReadAsync(cancellationToken))
            {
                columns.Add(new ColumnSchema(
                    Name: columnsReader.GetString(columnsReader.GetOrdinal("name")),
                    DataType: columnsReader.GetString(columnsReader.GetOrdinal("type")),
                    IsNullable: columnsReader.GetInt64(columnsReader.GetOrdinal("notnull")) == 0));
            }

            tables.Add(new TableSchema(tableName, columns));
        }

        return tables;
    }
}
