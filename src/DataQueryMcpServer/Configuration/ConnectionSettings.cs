namespace DataQueryMcpServer.Configuration;

public sealed class ConnectionSettings
{
    public required DbProvider Provider { get; init; }

    public required string ConnectionString { get; init; }

    public string? Description { get; init; }
}
