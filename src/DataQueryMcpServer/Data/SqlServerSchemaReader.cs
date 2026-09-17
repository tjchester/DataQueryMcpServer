using DataQueryMcpServer.Configuration;
using Microsoft.Data.SqlClient;

namespace DataQueryMcpServer.Data;

public sealed class SqlServerSchemaReader : ISchemaReader
{
    public DbProvider Provider => DbProvider.SqlServer;

    public async Task<IReadOnlyList<TableSchema>> GetSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { ApplicationIntent = ApplicationIntent.ReadOnly };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.TABLE_SCHEMA, t.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE
            FROM INFORMATION_SCHEMA.TABLES t
            JOIN INFORMATION_SCHEMA.COLUMNS c
                ON c.TABLE_SCHEMA = t.TABLE_SCHEMA AND c.TABLE_NAME = t.TABLE_NAME
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var tables = new List<TableSchema>();
        List<ColumnSchema>? currentColumns = null;
        string? currentTableName = null;

        while (await reader.ReadAsync(cancellationToken))
        {
            var qualifiedName = $"{reader.GetString(0)}.{reader.GetString(1)}";
            var column = new ColumnSchema(
                Name: reader.GetString(2),
                DataType: reader.GetString(3),
                IsNullable: string.Equals(reader.GetString(4), "YES", StringComparison.OrdinalIgnoreCase));

            if (qualifiedName != currentTableName)
            {
                currentColumns = [];
                tables.Add(new TableSchema(qualifiedName, currentColumns));
                currentTableName = qualifiedName;
            }

            currentColumns!.Add(column);
        }

        return tables;
    }
}
