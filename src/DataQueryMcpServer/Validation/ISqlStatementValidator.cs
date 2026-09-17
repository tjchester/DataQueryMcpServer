using DataQueryMcpServer.Configuration;

namespace DataQueryMcpServer.Validation;

public interface ISqlStatementValidator
{
    StatementValidationResult Validate(string sql, DbProvider provider);
}
