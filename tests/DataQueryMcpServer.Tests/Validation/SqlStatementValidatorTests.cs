using DataQueryMcpServer.Configuration;
using DataQueryMcpServer.Infrastructure;
using DataQueryMcpServer.Validation;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DataQueryMcpServer.Tests.Validation;

public class SqlStatementValidatorTests
{
    private readonly IStatementClassifier _classifier = Substitute.For<IStatementClassifier>();

    public SqlStatementValidatorTests()
    {
        _classifier.Provider.Returns(DbProvider.Sqlite);
    }

    [Fact]
    public void Validate_ReadOnlyModeAndReadOnlyStatement_IsAllowed()
    {
        _classifier.Classify(Arg.Any<string>()).Returns(StatementCategory.ReadOnly);
        var validator = CreateValidator(QueryMode.ReadOnly);

        var result = validator.Validate("SELECT 1", DbProvider.Sqlite);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReadOnlyModeAndWriteStatement_IsDenied()
    {
        _classifier.Classify(Arg.Any<string>()).Returns(StatementCategory.Write);
        var validator = CreateValidator(QueryMode.ReadOnly);

        var result = validator.Validate("DELETE FROM widgets", DbProvider.Sqlite);

        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("read-only");
    }

    [Fact]
    public void Validate_ReadWriteMode_IsAllowedWithoutConsultingClassifier()
    {
        var validator = CreateValidator(QueryMode.ReadWrite);

        var result = validator.Validate("DELETE FROM widgets", DbProvider.Sqlite);

        result.IsAllowed.Should().BeTrue();
        _classifier.DidNotReceive().Classify(Arg.Any<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrBlankSql_IsDeniedRegardlessOfMode(string sql)
    {
        var validator = CreateValidator(QueryMode.ReadWrite);

        var result = validator.Validate(sql, DbProvider.Sqlite);

        result.IsAllowed.Should().BeFalse();
    }

    private SqlStatementValidator CreateValidator(QueryMode mode)
    {
        var registry = new ProviderRegistry<IStatementClassifier>([_classifier], c => c.Provider);

        var options = Substitute.For<IOptionsMonitor<DatabaseServerOptions>>();
        options.CurrentValue.Returns(new DatabaseServerOptions { Mode = mode });

        return new SqlStatementValidator(registry, options);
    }
}
