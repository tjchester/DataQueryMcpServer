using System.Data.Common;
using DataQueryMcpServer.Configuration;

namespace DataQueryMcpServer.Data;

public interface IDbConnectionFactory
{
    DbProvider Provider { get; }

    /// <param name="readOnly">
    /// When true, the returned connection is configured for read-only intent using whatever mechanism
    /// the provider supports (e.g. SQL Server's ApplicationIntent=ReadOnly, SQLite's read-only file mode).
    /// This is a defense-in-depth measure, not the sole enforcement of read-only access.
    /// </param>
    DbConnection CreateConnection(string connectionString, bool readOnly);
}
