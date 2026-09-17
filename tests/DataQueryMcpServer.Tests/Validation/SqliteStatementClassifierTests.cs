using DataQueryMcpServer.Validation;

namespace DataQueryMcpServer.Tests.Validation;

public class SqliteStatementClassifierTests
{
    private readonly SqliteStatementClassifier _classifier = new();

    [Theory]
    [InlineData("SELECT * FROM widgets")]
    [InlineData("SELECT * FROM widgets WHERE id = 1;")]
    [InlineData("select * from widgets")]
    [InlineData("WITH cte AS (SELECT * FROM widgets) SELECT * FROM cte")]
    [InlineData("WITH RECURSIVE cte AS (SELECT 1) SELECT * FROM cte")]
    [InlineData("EXPLAIN QUERY PLAN SELECT * FROM widgets")]
    [InlineData("VALUES (1), (2)")]
    [InlineData("  -- a leading comment\nSELECT * FROM widgets")]
    [InlineData("/* block comment */ SELECT * FROM widgets")]
    public void Classify_ReadOnlyStatements_ReturnsReadOnly(string sql)
    {
        _classifier.Classify(sql).Should().Be(StatementCategory.ReadOnly);
    }

    [Theory]
    [InlineData("INSERT INTO widgets (name) VALUES ('x')")]
    [InlineData("UPDATE widgets SET name = 'x'")]
    [InlineData("DELETE FROM widgets")]
    [InlineData("DROP TABLE widgets")]
    [InlineData("CREATE TABLE widgets (id INTEGER)")]
    [InlineData("ALTER TABLE widgets ADD COLUMN name TEXT")]
    [InlineData("PRAGMA journal_mode = WAL")]
    [InlineData("ATTACH DATABASE 'other.db' AS other")]
    [InlineData("VACUUM")]
    public void Classify_WriteStatements_ReturnsWrite(string sql)
    {
        _classifier.Classify(sql).Should().Be(StatementCategory.Write);
    }

    [Fact]
    public void Classify_CteFollowedByInsert_ReturnsWrite()
    {
        // SQLite allows WITH to prefix INSERT/UPDATE/DELETE, not just SELECT - this is the specific
        // bypass the CTE-skipping logic exists to catch.
        const string sql = "WITH cte AS (SELECT id FROM widgets) INSERT INTO log (id) SELECT id FROM cte";

        _classifier.Classify(sql).Should().Be(StatementCategory.Write);
    }

    [Fact]
    public void Classify_MultipleCtesFollowedByInsert_ReturnsWrite()
    {
        const string sql = "WITH a AS (SELECT 1), b AS (SELECT 2) INSERT INTO log VALUES (1)";

        _classifier.Classify(sql).Should().Be(StatementCategory.Write);
    }

    [Fact]
    public void Classify_MultipleStatementsWhereOneIsAWrite_ReturnsWrite()
    {
        const string sql = "SELECT * FROM widgets; DELETE FROM widgets;";

        _classifier.Classify(sql).Should().Be(StatementCategory.Write);
    }

    [Fact]
    public void Classify_WriteKeywordInsideStringLiteral_IsNotFooled()
    {
        const string sql = "SELECT * FROM widgets WHERE description = 'INSERT INTO x; DROP TABLE y'";

        _classifier.Classify(sql).Should().Be(StatementCategory.ReadOnly);
    }

    [Fact]
    public void Classify_SemicolonInsideStringLiteral_DoesNotSplitStatement()
    {
        const string sql = "SELECT * FROM widgets WHERE note = 'a; DELETE FROM widgets;'";

        _classifier.Classify(sql).Should().Be(StatementCategory.ReadOnly);
    }

    [Fact]
    public void Classify_WriteKeywordInsideComment_IsNotFooled()
    {
        const string sql = "SELECT * FROM widgets -- DELETE FROM widgets\n WHERE id = 1";

        _classifier.Classify(sql).Should().Be(StatementCategory.ReadOnly);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(";;;")]
    public void Classify_EmptyOrBlankInput_ReturnsWrite(string sql)
    {
        _classifier.Classify(sql).Should().Be(StatementCategory.Write);
    }
}
