using System.Text;
using DataQueryMcpServer.Configuration;

namespace DataQueryMcpServer.Validation;

/// <summary>
/// Classifies SQLite SQL text using a small hand-rolled scanner, since there is no equivalent to
/// ScriptDom for SQLite's grammar. This is a best-effort first line of defense, not the authoritative
/// enforcement boundary — <see cref="Data.SqliteConnectionFactory"/> opens the file in the driver's
/// real read-only mode as the hard guarantee this classifier can't fully provide on its own.
/// </summary>
public sealed class SqliteStatementClassifier : IStatementClassifier
{
    private static readonly HashSet<string> ReadOnlyLeadingKeywords =
        new(StringComparer.OrdinalIgnoreCase) { "SELECT", "EXPLAIN", "VALUES" };

    public DbProvider Provider => DbProvider.Sqlite;

    public StatementCategory Classify(string sql)
    {
        var statements = SplitStatements(sql);
        if (statements.Count == 0)
        {
            return StatementCategory.Write;
        }

        return statements.All(IsReadOnlyStatement) ? StatementCategory.ReadOnly : StatementCategory.Write;
    }

    private static bool IsReadOnlyStatement(string statement)
    {
        var sanitized = Sanitize(statement);
        var (keyword, keywordEnd) = NextKeyword(sanitized, 0);
        if (keyword is null)
        {
            return false;
        }

        if (keyword.Equals("WITH", StringComparison.OrdinalIgnoreCase))
        {
            // WITH can prefix SELECT, or in SQLite, INSERT/UPDATE/DELETE — walk past the CTE
            // definitions to classify the statement the CTEs actually apply to.
            var afterCtes = SkipCommonTableExpressions(sanitized, keywordEnd);
            (keyword, _) = NextKeyword(sanitized, afterCtes);
        }

        return keyword is not null && ReadOnlyLeadingKeywords.Contains(keyword);
    }

    /// <summary>
    /// Splits top-level statements on ';', ignoring semicolons and parens that appear inside
    /// string/identifier literals or comments.
    /// </summary>
    private static List<string> SplitStatements(string sql)
    {
        var sanitized = Sanitize(sql);
        var statements = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < sanitized.Length; i++)
        {
            switch (sanitized[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ';' when depth == 0:
                    AddIfNotEmpty(statements, sql[start..i]);
                    start = i + 1;
                    break;
            }
        }

        AddIfNotEmpty(statements, sql[start..]);
        return statements;
    }

    private static void AddIfNotEmpty(List<string> statements, string candidate)
    {
        var trimmed = candidate.Trim();
        if (trimmed.Length > 0)
        {
            statements.Add(trimmed);
        }
    }

    /// <summary>
    /// Replaces comments and the contents of string/identifier literals with spaces so callers can
    /// scan for structural characters (parens, semicolons, keywords) without being misled by content
    /// inside literals. Preserves the original length/positions.
    /// </summary>
    private static string Sanitize(string sql)
    {
        var result = new StringBuilder(sql.Length);
        var i = 0;

        while (i < sql.Length)
        {
            var c = sql[i];

            if (c == '-' && Peek(sql, i + 1) == '-')
            {
                while (i < sql.Length && sql[i] != '\n')
                {
                    result.Append(sql[i] == '\n' ? '\n' : ' ');
                    i++;
                }

                continue;
            }

            if (c == '/' && Peek(sql, i + 1) == '*')
            {
                result.Append("  ");
                i += 2;
                while (i < sql.Length && !(sql[i] == '*' && Peek(sql, i + 1) == '/'))
                {
                    result.Append(sql[i] == '\n' ? '\n' : ' ');
                    i++;
                }

                if (i < sql.Length)
                {
                    result.Append("  ");
                    i += 2;
                }

                continue;
            }

            if (c is '\'' or '"' or '`')
            {
                var quote = c;
                result.Append(' ');
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == quote && Peek(sql, i + 1) == quote)
                    {
                        result.Append("  ");
                        i += 2;
                        continue;
                    }

                    if (sql[i] == quote)
                    {
                        result.Append(' ');
                        i++;
                        break;
                    }

                    result.Append(' ');
                    i++;
                }

                continue;
            }

            if (c == '[')
            {
                result.Append(' ');
                i++;
                while (i < sql.Length && sql[i] != ']')
                {
                    result.Append(' ');
                    i++;
                }

                if (i < sql.Length)
                {
                    result.Append(' ');
                    i++;
                }

                continue;
            }

            result.Append(c);
            i++;
        }

        return result.ToString();
    }

    private static char? Peek(string s, int index) => index < s.Length ? s[index] : null;

    private static (string? Keyword, int EndIndex) NextKeyword(string sanitized, int start)
    {
        var i = SkipWhitespace(sanitized, start);
        var wordStart = i;
        while (i < sanitized.Length && (char.IsLetterOrDigit(sanitized[i]) || sanitized[i] == '_'))
        {
            i++;
        }

        return i == wordStart ? (null, i) : (sanitized[wordStart..i], i);
    }

    private static int SkipWhitespace(string s, int index)
    {
        while (index < s.Length && char.IsWhiteSpace(s[index]))
        {
            index++;
        }

        return index;
    }

    private static int SkipBalancedParens(string s, int index)
    {
        var depth = 0;
        for (var i = index; i < s.Length; i++)
        {
            if (s[i] == '(')
            {
                depth++;
            }
            else if (s[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i + 1;
                }
            }
        }

        return s.Length;
    }

    /// <summary>
    /// Walks past "[RECURSIVE] name [(cols)] AS (query) [, name [(cols)] AS (query) ...]" starting
    /// just after the WITH keyword, returning the index of the statement the CTEs apply to.
    /// </summary>
    private static int SkipCommonTableExpressions(string sanitized, int index)
    {
        var (keyword, afterKeyword) = NextKeyword(sanitized, index);
        if (keyword is not null && keyword.Equals("RECURSIVE", StringComparison.OrdinalIgnoreCase))
        {
            index = afterKeyword;
        }

        while (true)
        {
            var (name, afterName) = NextKeyword(sanitized, index);
            if (name is null)
            {
                return index;
            }

            index = SkipWhitespace(sanitized, afterName);

            if (index < sanitized.Length && sanitized[index] == '(')
            {
                index = SkipWhitespace(sanitized, SkipBalancedParens(sanitized, index));
            }

            var (asKeyword, afterAs) = NextKeyword(sanitized, index);
            if (asKeyword is null || !asKeyword.Equals("AS", StringComparison.OrdinalIgnoreCase))
            {
                // Malformed input; let the caller classify whatever follows and fail closed.
                return index;
            }

            index = SkipWhitespace(sanitized, afterAs);
            if (index < sanitized.Length && sanitized[index] == '(')
            {
                index = SkipBalancedParens(sanitized, index);
            }

            index = SkipWhitespace(sanitized, index);
            if (index < sanitized.Length && sanitized[index] == ',')
            {
                index++;
                continue;
            }

            return index;
        }
    }
}
