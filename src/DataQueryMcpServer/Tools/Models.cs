namespace DataQueryMcpServer.Tools;

public sealed record ConnectionSummary(string Name, string Provider, string? Description);

public sealed record QueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    bool Truncated);
