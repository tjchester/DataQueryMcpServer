using DataQueryMcpServer.Configuration;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DataQueryMcpServer.Validation;

/// <summary>
/// Classifies T-SQL using a real parser (ScriptDom) rather than keyword matching, so comments,
/// string literals, and statement smuggling tricks can't be used to sneak a write past the gate.
/// Covers both SQL Server and Azure SQL, which share the same T-SQL grammar.
/// </summary>
public sealed class TSqlStatementClassifier : IStatementClassifier
{
    public DbProvider Provider => DbProvider.SqlServer;

    public StatementCategory Classify(string sql)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out var errors);

        if (errors.Count > 0 || fragment is not TSqlScript script)
        {
            // Unparseable input can't be safely classified as read-only.
            return StatementCategory.Write;
        }

        var statements = script.Batches.SelectMany(batch => batch.Statements).ToList();
        if (statements.Count == 0)
        {
            return StatementCategory.Write;
        }

        return statements.All(IsReadOnlyStatement) ? StatementCategory.ReadOnly : StatementCategory.Write;
    }

    private static bool IsReadOnlyStatement(TSqlStatement statement)
    {
        // A CTE-prefixed INSERT/UPDATE/DELETE/MERGE parses as that statement type (not SelectStatement),
        // so it is rejected here regardless of the leading WITH clause. SELECT ... INTO creates a table
        // as a side effect, so it is rejected too even though it parses as a SelectStatement.
        return statement is SelectStatement { Into: null };
    }
}
