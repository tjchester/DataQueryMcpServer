using System.ComponentModel;
using DataQueryMcpServer.Configuration;
using DataQueryMcpServer.Data;
using DataQueryMcpServer.Infrastructure;
using DataQueryMcpServer.Validation;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DataQueryMcpServer.Tools;

internal sealed class DataQueryTools(
    IOptionsMonitor<DatabaseServerOptions> options,
    ProviderRegistry<IDbConnectionFactory> connectionFactories,
    ProviderRegistry<ISchemaReader> schemaReaders,
    ISqlStatementValidator validator)
{
    [McpServerTool]
    [Description("Lists the database connections configured on this server (name, provider, description). Does not expose connection strings or credentials.")]
    public IReadOnlyList<ConnectionSummary> ListConnections()
    {
        return options.CurrentValue.Connections
            .Select(kvp => new ConnectionSummary(kvp.Key, kvp.Value.Provider.ToString(), kvp.Value.Description))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [McpServerTool]
    [Description("Retrieves table and column metadata for a configured connection. Call ListConnections first to see available connection names.")]
    public async Task<IReadOnlyList<TableSchema>> GetSchemaAsync(
        [Description("The name of a connection returned by ListConnections.")] string connectionName,
        CancellationToken cancellationToken)
    {
        var settings = GetConnectionSettingsOrThrow(connectionName);
        var reader = schemaReaders.Resolve(settings.Provider);
        return await reader.GetSchemaAsync(settings.ConnectionString, cancellationToken);
    }

    [McpServerTool]
    [Description("Executes a SQL statement against a configured connection and returns the results. Only SELECT statements are allowed unless the server is configured for read-write access.")]
    public async Task<QueryResult> RunQueryAsync(
        [Description("The name of a connection returned by ListConnections.")] string connectionName,
        [Description("The SQL statement to execute.")] string sql,
        CancellationToken cancellationToken)
    {
        var settings = GetConnectionSettingsOrThrow(connectionName);

        var validation = validator.Validate(sql, settings.Provider);
        if (!validation.IsAllowed)
        {
            throw new McpException(validation.ErrorMessage ?? "The statement was rejected by the SQL validator.");
        }

        var serverOptions = options.CurrentValue;
        var readOnly = serverOptions.Mode == QueryMode.ReadOnly;
        var factory = connectionFactories.Resolve(settings.Provider);

        await using var connection = factory.CreateConnection(settings.ConnectionString, readOnly);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = serverOptions.CommandTimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<Dictionary<string, object?>>();

        while (rows.Count < serverOptions.MaxRows && await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(columns.Length);
            for (var i = 0; i < columns.Length; i++)
            {
                var value = reader.GetValue(i);
                row[columns[i]] = value is DBNull ? null : value;
            }

            rows.Add(row);
        }

        var truncated = rows.Count == serverOptions.MaxRows && await reader.ReadAsync(cancellationToken);

        return new QueryResult(columns, rows, truncated);
    }

    private ConnectionSettings GetConnectionSettingsOrThrow(string connectionName)
    {
        if (!options.CurrentValue.Connections.TryGetValue(connectionName, out var settings))
        {
            throw new McpException(
                $"No connection named '{connectionName}' is configured. Call ListConnections to see the available connections.");
        }

        return settings;
    }
}
