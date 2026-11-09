using FluentAssertions;
using Newtonsoft.Json.Linq;
using TestBuilder.Core.Checks;
using Xunit;

namespace TestBuilder.Tests.Checks;

/// <summary>
/// Проверки операторов <see cref="CheckEvaluator"/>.
/// </summary>
public sealed class CheckEvaluatorTests
{
    [Fact]
    public void Equals_ComparesNumericTokenWithStringExpectedValue()
    {
        var (success, message) = CheckEvaluator.Evaluate(
            JToken.Parse("42"),
            new JsonPathCheck { Path = "$.id", Operator = CheckOperator.Equals, ExpectedValue = "42" });

        success.Should().BeTrue(message);
    }

    [Theory]
    [InlineData(CheckOperator.Greater, "10", true)]
    [InlineData(CheckOperator.Greater, "20", false)]
    [InlineData(CheckOperator.Less, "20", true)]
    [InlineData(CheckOperator.Less, "5", false)]
    public void NumericComparisons_Work(CheckOperator checkOperator, string expected, bool shouldPass)
    {
        var (success, _) = CheckEvaluator.Evaluate(
            JToken.Parse("15"),
            new JsonPathCheck { Path = "$.value", Operator = checkOperator, ExpectedValue = expected });

        success.Should().Be(shouldPass);
    }

    [Fact]
    public void Between_IsInclusive()
    {
        var check = new JsonPathCheck
        {
            Path = "$.value",
            Operator = CheckOperator.Between,
            ExpectedValue = "10..20",
        };

        CheckEvaluator.Evaluate(JToken.Parse("10"), check).success.Should().BeTrue();
        CheckEvaluator.Evaluate(JToken.Parse("20"), check).success.Should().BeTrue();
        CheckEvaluator.Evaluate(JToken.Parse("20.01"), check).success.Should().BeFalse();
    }

    [Fact]
    public void In_SplitsCommaSeparatedValues()
    {
        var check = new JsonPathCheck
        {
            Path = "$.state",
            Operator = CheckOperator.In,
            ExpectedValue = "new, active, done",
        };

        CheckEvaluator.Evaluate(JToken.Parse("\"active\""), check).success.Should().BeTrue();
        CheckEvaluator.Evaluate(JToken.Parse("\"failed\""), check).success.Should().BeFalse();
    }

    [Fact]
    public void Empty_AcceptsNullMissingAndEmptyArray()
    {
        var check = new JsonPathCheck { Path = "$.items", Operator = CheckOperator.Empty };

        CheckEvaluator.Evaluate(null, check).success.Should().BeTrue();
        CheckEvaluator.Evaluate(JToken.Parse("[]"), check).success.Should().BeTrue();
        CheckEvaluator.Evaluate(JToken.Parse("\"text\""), check).success.Should().BeFalse();
    }

    [Fact]
    public void MatchesRegex_InvalidPattern_ReturnsFailureInsteadOfThrowing()
    {
        var check = new JsonPathCheck
        {
            Path = "$.name",
            Operator = CheckOperator.MatchesRegex,
            ExpectedValue = "[",
        };

        var (success, message) = CheckEvaluator.Evaluate(JToken.Parse("\"test\""), check);

        success.Should().BeFalse();
        message.Should().Contain("не пройдена");
    }

    [Fact]
    public void CountGreater_CountsArrayItems()
    {
        var check = new JsonPathCheck
        {
            Path = "$.items",
            Operator = CheckOperator.CountGreater,
            ExpectedValue = "2",
        };

        CheckEvaluator.Evaluate(JToken.Parse("[1,2,3]"), check).success.Should().BeTrue();
        CheckEvaluator.Evaluate(JToken.Parse("[1,2]"), check).success.Should().BeFalse();
    }
}
