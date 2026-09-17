using System.Data.Common;
using DataQueryMcpServer.Configuration;
using Microsoft.Data.SqlClient;

namespace DataQueryMcpServer.Data;

/// <summary>
/// Handles both SQL Server and Azure SQL, which share the same driver and connection string surface.
/// </summary>
public sealed class SqlServerConnectionFactory : IDbConnectionFactory
{
    public DbProvider Provider => DbProvider.SqlServer;

    public DbConnection CreateConnection(string connectionString, bool readOnly)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (readOnly)
        {
            builder.ApplicationIntent = ApplicationIntent.ReadOnly;
        }

        return new SqlConnection(builder.ConnectionString);
    }
}
