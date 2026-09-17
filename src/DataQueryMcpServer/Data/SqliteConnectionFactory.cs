using System.Data.Common;
using DataQueryMcpServer.Configuration;
using Microsoft.Data.Sqlite;

namespace DataQueryMcpServer.Data;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    public DbProvider Provider => DbProvider.Sqlite;

    public DbConnection CreateConnection(string connectionString, bool readOnly)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (readOnly)
        {
            // Hard, driver-level guarantee: the file is opened read-only regardless of what
            // the statement validator did or did not catch.
            builder.Mode = SqliteOpenMode.ReadOnly;
        }

        return new SqliteConnection(builder.ConnectionString);
    }
}
