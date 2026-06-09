using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Filter;
using AwesomeAssertions;

namespace ApiQueryOptions.Tests;

[TestClass]
public class FilterParserTests
{
    private static FilterParser Parser() => new();

    [TestMethod]
    public void Eq_StringLiteral_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Name eq 'Alice'");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Property.Should().Be("Name");
        node.Operator.Should().Be(FilterOperator.Eq);
        node.Value.Should().Be("Alice");
    }

    [TestMethod]
    public void Ne_StringLiteral_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Status ne 'Active'");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Operator.Should().Be(FilterOperator.Ne);
    }

    [TestMethod]
    public void Lt_IntLiteral_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Age lt 30");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Operator.Should().Be(FilterOperator.Lt);
        node.Value.Should().Be(30);
    }

    [TestMethod]
    public void Gt_IntLiteral_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Age gt 18");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Operator.Should().Be(FilterOperator.Gt);
    }

    [TestMethod]
    public void Le_IntLiteral_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Score le 100");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Operator.Should().Be(FilterOperator.Le);
    }

    [TestMethod]
    public void Ge_IntLiteral_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Score ge 0");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Operator.Should().Be(FilterOperator.Ge);
    }

    [TestMethod]
    public void Eq_DecimalLiteral_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Price eq 9.99");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Value.Should().Be(9.99m);
    }

    [TestMethod]
    public void Eq_NullLiteral_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("DeletedAt eq null");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Value.Should().BeNull();
    }

    [TestMethod]
    public void Eq_TrueLiteral_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("IsActive eq true");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Value.Should().Be(true);
    }

    [TestMethod]
    public void Eq_FalseLiteral_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("IsActive eq false");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Value.Should().Be(false);
    }

    [TestMethod]
    public void Eq_NegativeInt_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Balance eq -5");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Value.Should().Be(-5);
    }

    [TestMethod]
    public void StartsWith_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("startswith(Name, 'Al')");
        FunctionFilterNode node = clause.Expression.Should().BeOfType<FunctionFilterNode>().Subject;
        node.Function.Should().Be(StringFunction.StartsWith);
        node.Property.Should().Be("Name");
        node.Value.Should().Be("Al");
    }

    [TestMethod]
    public void EndsWith_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("endswith(Email, '.com')");
        FunctionFilterNode node = clause.Expression.Should().BeOfType<FunctionFilterNode>().Subject;
        node.Function.Should().Be(StringFunction.EndsWith);
        node.Value.Should().Be(".com");
    }

    [TestMethod]
    public void Contains_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("contains(Description, 'test')");
        FunctionFilterNode node = clause.Expression.Should().BeOfType<FunctionFilterNode>().Subject;
        node.Function.Should().Be(StringFunction.Contains);
    }

    [TestMethod]
    public void And_TwoComparisons_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Age gt 18 and Age lt 65");
        LogicalFilterNode node = clause.Expression.Should().BeOfType<LogicalFilterNode>().Subject;
        node.Operator.Should().Be(LogicalOperator.And);
        node.Left.Should().BeOfType<BinaryFilterNode>();
        node.Right.Should().BeOfType<BinaryFilterNode>();
    }

    [TestMethod]
    public void Or_TwoComparisons_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Status eq 'Active' or Status eq 'Pending'");
        LogicalFilterNode node = clause.Expression.Should().BeOfType<LogicalFilterNode>().Subject;
        node.Operator.Should().Be(LogicalOperator.Or);
    }

    [TestMethod]
    public void AndOrChain_ParsesCorrectly()
    {
        // AND binds tighter than OR
        FilterClause clause = Parser().Parse("A eq 1 or B eq 2 and C eq 3");
        LogicalFilterNode node = clause.Expression.Should().BeOfType<LogicalFilterNode>().Subject;
        node.Operator.Should().Be(LogicalOperator.Or);
        node.Right.Should().BeOfType<LogicalFilterNode>()
            .Which.Operator.Should().Be(LogicalOperator.And);
    }

    [TestMethod]
    public void Parenthesized_GroupingRespected()
    {
        // (A eq 1 or B eq 2) and C eq 3
        FilterClause clause = Parser().Parse("(A eq 1 or B eq 2) and C eq 3");
        LogicalFilterNode node = clause.Expression.Should().BeOfType<LogicalFilterNode>().Subject;
        node.Operator.Should().Be(LogicalOperator.And);
        node.Left.Should().BeOfType<LogicalFilterNode>()
            .Which.Operator.Should().Be(LogicalOperator.Or);
    }

    [TestMethod]
    public void SingleQuoteEscape_InStringLiteral_ParsedCorrectly()
    {
        FilterClause clause = Parser().Parse("Name eq 'O''Brien'");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Value.Should().Be("O'Brien");
    }

    [TestMethod]
    public void DottedProperty_ParsesCorrectly()
    {
        FilterClause clause = Parser().Parse("Address.City eq 'Seattle'");
        BinaryFilterNode node = clause.Expression.Should().BeOfType<BinaryFilterNode>().Subject;
        node.Property.Should().Be("Address.City");
    }

    [TestMethod]
    public void EmptyInput_ThrowsFilterParseException()
    {
        Func<FilterClause> act = () => Parser().Parse(string.Empty);
        act.Should().Throw<FilterParseException>();
    }

    [TestMethod]
    public void MissingValue_ThrowsFilterParseException()
    {
        Func<FilterClause> act = () => Parser().Parse("Name eq");
        act.Should().Throw<FilterParseException>();
    }

    [TestMethod]
    public void UnrecognizedToken_ThrowsFilterParseException()
    {
        Func<FilterClause> act = () => Parser().Parse("Name # 'foo'");
        act.Should().Throw<FilterParseException>();
    }

    [TestMethod]
    public void UnclosedParenthesis_ThrowsFilterParseException()
    {
        Func<FilterClause> act = () => Parser().Parse("(Name eq 'foo'");
        act.Should().Throw<FilterParseException>();
    }

    [TestMethod]
    public void FunctionMissingCloseParen_ThrowsFilterParseException()
    {
        Func<FilterClause> act = () => Parser().Parse("startswith(Name, 'foo'");
        act.Should().Throw<FilterParseException>();
    }

    [TestMethod]
    public void FilterParseException_HasPosition()
    {
        Func<FilterClause> act = () => Parser().Parse("Name # 'foo'");
        act.Should().Throw<FilterParseException>()
            .Which.Position.Should().BeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    public void FilterParseException_HasRawFilter()
    {
        const string raw = "Name # 'foo'";
        Func<FilterClause> act = () => Parser().Parse(raw);
        act.Should().Throw<FilterParseException>()
            .Which.RawFilter.Should().Be(raw);
    }

    [TestMethod]
    public void FilterQueryOption_LazyParse_SameInstance()
    {
        var opt = new Options.FilterQueryOption("Name eq 'x'");
        FilterClause first = opt.FilterClause;
        FilterClause second = opt.FilterClause;
        first.Should().BeSameAs(second);
    }

    [TestMethod]
    public void FilterQueryOption_ParseError_RepeatedAccessThrowsSameException()
    {
        var opt = new Options.FilterQueryOption("Name #");
        Func<FilterClause> act = () => _ = opt.FilterClause;
        act.Should().Throw<FilterParseException>();
        act.Should().Throw<FilterParseException>(); // idempotent
    }
}
