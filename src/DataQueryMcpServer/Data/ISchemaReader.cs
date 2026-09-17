using DataQueryMcpServer.Configuration;

namespace DataQueryMcpServer.Data;

public interface ISchemaReader
{
    DbProvider Provider { get; }

    Task<IReadOnlyList<TableSchema>> GetSchemaAsync(string connectionString, CancellationToken cancellationToken);
}
