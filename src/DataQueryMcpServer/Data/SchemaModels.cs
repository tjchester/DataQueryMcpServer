namespace DataQueryMcpServer.Data;

public sealed record ColumnSchema(string Name, string DataType, bool IsNullable);

public sealed record TableSchema(string Name, IReadOnlyList<ColumnSchema> Columns);
