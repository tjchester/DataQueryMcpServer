using DataQueryMcpServer.Configuration;
using DataQueryMcpServer.Infrastructure;
using Microsoft.Extensions.Options;

namespace DataQueryMcpServer.Validation;

public sealed class SqlStatementValidator(
    ProviderRegistry<IStatementClassifier> classifiers,
    IOptionsMonitor<DatabaseServerOptions> options) : ISqlStatementValidator
{
    public StatementValidationResult Validate(string sql, DbProvider provider)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return StatementValidationResult.Denied("SQL statement must not be empty.");
        }

        if (options.CurrentValue.Mode == QueryMode.ReadWrite)
        {
            return StatementValidationResult.Allowed;
        }

        var classifier = classifiers.Resolve(provider);
        var category = classifier.Classify(sql);

        return category == StatementCategory.ReadOnly
            ? StatementValidationResult.Allowed
            : StatementValidationResult.Denied(
                "Only read-only (SELECT) statements are allowed. Set Database:Mode to \"ReadWrite\" in configuration to allow other statements.");
    }
}
