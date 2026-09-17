using DataQueryMcpServer.Configuration;

namespace DataQueryMcpServer.Validation;

public interface IStatementClassifier
{
    DbProvider Provider { get; }

    StatementCategory Classify(string sql);
}
