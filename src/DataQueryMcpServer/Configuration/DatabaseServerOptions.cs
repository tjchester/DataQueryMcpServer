namespace DataQueryMcpServer.Configuration;

public sealed class DatabaseServerOptions
{
    public const string SectionName = "Database";

    public QueryMode Mode { get; set; } = QueryMode.ReadOnly;

    public int CommandTimeoutSeconds { get; set; } = 30;

    public int MaxRows { get; set; } = 1000;

    public Dictionary<string, ConnectionSettings> Connections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
