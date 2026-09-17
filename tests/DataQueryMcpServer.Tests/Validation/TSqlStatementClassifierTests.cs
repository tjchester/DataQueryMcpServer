using DataQueryMcpServer.Validation;

namespace DataQueryMcpServer.Tests.Validation;

public class TSqlStatementClassifierTests
{
    private readonly TSqlStatementClassifier _classifier = new();

    [Theory]
    [InlineData("SELECT * FROM Widgets")]
    [InlineData("SELECT * FROM Widgets WHERE Id = 1;")]
    [InlineData("SELECT a.* FROM Widgets a JOIN Gadgets b ON a.Id = b.WidgetId")]
    [InlineData("WITH Cte AS (SELECT * FROM Widgets) SELECT * FROM Cte")]
    [InlineData("SELECT 1 UNION SELECT 2")]
    public void Classify_ReadOnlyStatements_ReturnsReadOnly(string sql)
    {
        _classifier.Classify(sql).Should().Be(StatementCategory.ReadOnly);
    }

    [Theory]
    [InlineData("INSERT INTO Widgets (Name) VALUES ('x')")]
    [InlineData("UPDATE Widgets SET Name = 'x'")]
    [InlineData("DELETE FROM Widgets")]
    [InlineData("DROP TABLE Widgets")]
    [InlineData("CREATE TABLE Widgets (Id INT)")]
    [InlineData("ALTER TABLE Widgets ADD Name NVARCHAR(50)")]
    [InlineData("TRUNCATE TABLE Widgets")]
    [InlineData("EXEC sp_helptext 'Widgets'")]
    [InlineData("SELECT * INTO NewTable FROM Widgets")]
    public void Classify_WriteStatements_ReturnsWrite(string sql)
    {
        _classifier.Classify(sql).Should().Be(StatementCategory.Write);
    }

    [Fact]
    public void Classify_CteFollowedByInsert_ReturnsWrite()
    {
        // T-SQL allows a CTE to prefix INSERT/UPDATE/DELETE, not just SELECT.
        const string sql = "WITH Cte AS (SELECT Id FROM Widgets) INSERT INTO Log (Id) SELECT Id FROM Cte";

        _classifier.Classify(sql).Should().Be(StatementCategory.Write);
    }

    [Fact]
    public void Classify_MultipleStatementsWhereOneIsAWrite_ReturnsWrite()
    {
        const string sql = "SELECT * FROM Widgets; DELETE FROM Widgets;";

        _classifier.Classify(sql).Should().Be(StatementCategory.Write);
    }

    [Fact]
    public void Classify_UnparseableSql_ReturnsWrite()
    {
        _classifier.Classify("SELECT FROM WHERE ((( ").Should().Be(StatementCategory.Write);
    }

    [Fact]
    public void Classify_EmptyStatement_ReturnsWrite()
    {
        _classifier.Classify(";").Should().Be(StatementCategory.Write);
    }
}
