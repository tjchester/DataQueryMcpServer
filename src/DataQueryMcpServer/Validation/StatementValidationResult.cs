namespace DataQueryMcpServer.Validation;

public sealed record StatementValidationResult(bool IsAllowed, string? ErrorMessage)
{
    public static StatementValidationResult Allowed { get; } = new(true, null);

    public static StatementValidationResult Denied(string message) => new(false, message);
}
