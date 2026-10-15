using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using ApiQueryOptions.Filter;
using ApiQueryOptions.Options;

namespace ApiQueryOptions.Tests;

[TestClass]
public class InvalidQueryOptionTests
{
    private static readonly ShapeEntity[] Data =
    [
        new() { Id = 1, Name = "a", Location = new Point(1, 2) },
        new() { Id = 2, Name = "b" },
    ];

    private static Func<List<ShapeEntity>> Filter(string filter) =>
        () => Data.AsQueryable().ApplyFilter(new FilterQueryOption(filter), new ApiQueryOptionsSettings()).ToList();

    [TestMethod]
    public void ApiQueryOptionsException_IsBaseOfClientInputExceptions()
    {
        typeof(FilterParseException).Should().BeDerivedFrom<ApiQueryOptionsException>();
        typeof(InvalidQueryOptionException).Should().BeDerivedFrom<ApiQueryOptionsException>();
        typeof(QueryOptionDisabledException).Should().NotBeDerivedFrom<ApiQueryOptionsException>();
        typeof(ApiQueryOptionsException).Should().BeAbstract();
    }

    [TestMethod]
    public void InvalidQueryOptionException_Constructor_SetsProperties()
    {
        var inner = new InvalidOperationException("inner");

        var ex = new InvalidQueryOptionException("orderby", "Name", "message", inner);

        ex.OptionName.Should().Be("orderby");
        ex.Property.Should().Be("Name");
        ex.Message.Should().Be("message");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [TestMethod]
    public void InvalidQueryOptionException_ConstructorWithoutInner_HasNoInnerException()
    {
        var ex = new InvalidQueryOptionException("filter", null, "message");

        ex.Property.Should().BeNull();
        ex.InnerException.Should().BeNull();
    }

    [TestMethod]
    public void FilterParseException_CaughtAsBaseType()
    {
        Action act = () => _ = new FilterParser().Parse("Name eq");

        act.Should().Throw<ApiQueryOptionsException>().Which.Should().BeOfType<FilterParseException>();
    }

    [TestMethod]
    [DataRow("startswith(Id, 'a')", "The 'StartsWith' function requires a string property, but 'Id' is of type 'Int32'.")]
    [DataRow("contains(Location, 'a')", "The 'Contains' function requires a string property, but 'Location' is of type 'Nullable`1'.")]
    [DataRow("Id eq null", "Property 'Id' of type 'Int32' cannot be compared with null.")]
    [DataRow("Name lt null", "The 'Lt' operator cannot be used with null for property 'Name'.")]
    [DataRow("Location ge null", "The 'Ge' operator cannot be used with null for property 'Location'.")]
    [DataRow("IsActive lt true", "The 'Lt' operator is not supported for property 'IsActive'.")]

    [DataRow("Missing.Name eq 'a'", "Property 'Missing' not found on type 'ShapeEntity'.")]
    [DataRow("Child.Missing eq 'a'", "Property 'Missing' not found on type 'ShapeChild'.")]
    [DataRow("Name. eq 'a'", "Property '' not found on type 'String'.")]
    [DataRow("WriteOnly eq 'a'", "Property 'WriteOnly' not found on type 'ShapeEntity'.")]
    [DataRow("Item eq 'a'", "Property 'Item' not found on type 'ShapeEntity'.")]
    public void ApplyFilter_InvalidUse_ThrowsInvalidQueryOption(string filter, string message)
    {
        InvalidQueryOptionException ex = Filter(filter).Should().Throw<InvalidQueryOptionException>().Which;

        ex.OptionName.Should().Be("filter");
        ex.Message.Should().Be(message);
    }

    [TestMethod]
    public void ApplyFilter_OperatorNotDefined_KeepsInnerException()
    {
        Filter("IsActive gt false").Should().Throw<InvalidQueryOptionException>()
            .WithInnerException<InvalidOperationException>();
    }

    [TestMethod]
    public void ApplyFilter_NullableValueTypeEqNull_Works()
    {
        var data = new[] { new ShapeEntity { Id = 1, Count = null }, new ShapeEntity { Id = 2, Count = 3 } };

        data.AsQueryable()
            .ApplyFilter(new FilterQueryOption("Count ne null"), new ApiQueryOptionsSettings())
            .Select(e => e.Id)
            .Should().Equal(2);
    }

    [TestMethod]
    public void ApplyFilter_NullableStructWithoutEqualityOperatorEqNull_Works()
    {
        Data.AsQueryable()
            .ApplyFilter(new FilterQueryOption("Location eq null"), new ApiQueryOptionsSettings())
            .Select(e => e.Id)
            .Should().Equal(2);
    }

    [TestMethod]
    public void ApplyFilter_ReferencePropertyEqNull_Works()
    {
        var data = new[] { new ShapeEntity { Id = 1 }, new ShapeEntity { Id = 2, Child = new ShapeChild() } };

        data.AsQueryable()
            .ApplyFilter(new FilterQueryOption("Child eq null"), new ApiQueryOptionsSettings())
            .Select(e => e.Id)
            .Should().Equal(1);
    }

    [TestMethod]
    public void ApplyOrderBy_UnknownProperty_ThrowsInvalidQueryOption()
    {
        Action act = () => Data.AsQueryable().ApplyOrderBy(new OrderByQueryOption("Missing desc"), new ApiQueryOptionsSettings());

        InvalidQueryOptionException ex = act.Should().Throw<InvalidQueryOptionException>().Which;
        ex.OptionName.Should().Be("orderby");
        ex.Property.Should().Be("Missing");
    }

    [TestMethod]
    public void ApplyOrderBy_PropertyNameInAnyCase_Sorts()
    {
        Data.AsQueryable()
            .ApplyOrderBy(new OrderByQueryOption("NAME desc"), new ApiQueryOptionsSettings())
            .Select(e => e.Id)
            .Should().Equal(2, 1);
    }

    [TestMethod]
    public void PropertyCache_ExactCaseMatch_IsPreferred()
    {
        PropertyCache.Find(typeof(CasedEntity), "value")!.Name.Should().Be("value");
        PropertyCache.Find(typeof(CasedEntity), "Value")!.Name.Should().Be("Value");
    }

    [TestMethod]
    public void PropertyCache_NoExactMatch_FallsBackToCaseInsensitive()
    {
        PropertyCache.Find(typeof(ShapeEntity), "nAmE")!.Name.Should().Be("Name");
    }

    [TestMethod]
    public void PropertyCache_RepeatedLookup_ReturnsSameInstance()
    {
        PropertyCache.Find(typeof(ShapeEntity), "Id").Should().BeSameAs(PropertyCache.Find(typeof(ShapeEntity), "id"));
    }

    [TestMethod]
    public void PropertyCache_HiddenProperty_ReturnsMostDerived()
    {
        PropertyCache.Find(typeof(DerivedEntity), "Value")!.DeclaringType.Should().Be<DerivedEntity>();
        PropertyCache.Find(typeof(DerivedEntity), "value")!.DeclaringType.Should().Be<DerivedEntity>();
    }

    [TestMethod]
    public void ApplyFilter_HiddenProperty_UsesDerivedType()
    {
        var data = new[] { new DerivedEntity { Value = "x" } };

        data.AsQueryable()
            .ApplyFilter(new FilterQueryOption("Value eq 'x'"), new ApiQueryOptionsSettings())
            .Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("Name eq 'abc")]
    [DataRow("Name eq '")]
    [DataRow("Name eq 'it''s")]
    public void Parse_UnterminatedString_ThrowsWithPosition(string filter)
    {
        Action act = () => new FilterParser().Parse(filter);

        FilterParseException ex = act.Should().Throw<FilterParseException>().Which;
        ex.Position.Should().Be(8);
        ex.Message.Should().Be("Unterminated string literal starting at position 8.");
    }

    [TestMethod]
    public void Parse_EscapedQuoteAtEnd_IsTerminated()
    {
        var node = (BinaryFilterNode)new FilterParser().Parse("Name eq 'it'''").Expression;

        node.Value.Should().Be("it'");
    }

    [TestMethod]
    [DataRow("Id eq 2147483647", typeof(int))]
    [DataRow("Id eq 2147483648", typeof(long))]
    [DataRow("Id eq -9223372036854775808", typeof(long))]
    [DataRow("Id eq 9223372036854775808", typeof(decimal))]
    [DataRow("Id eq -9223372036854775809", typeof(decimal))]
    public void Parse_IntegerLiteral_UsesSmallestType(string filter, Type expected)
    {
        var node = (BinaryFilterNode)new FilterParser().Parse(filter).Expression;

        node.Value.Should().BeOfType(expected);
    }

    [TestMethod]
    public void Parse_IntegerBeyondDecimal_Throws()
    {
        Action act = () => new FilterParser().Parse("Id eq 99999999999999999999999999999999");

        act.Should().Throw<FilterParseException>().WithMessage("Integer value '*' is out of range at position 6.");
    }
}

internal readonly struct Point(int x, int y)
{
    public int X { get; } = x;

    public int Y { get; } = y;
}

internal sealed class ShapeChild
{
    public string? Label { get; set; }
}

internal sealed class ShapeEntity
{
    public ShapeChild? Child { get; set; }

    public int? Count { get; set; }

    public int Id { get; set; }

    public bool IsActive { get; set; }

    public Point? Location { get; set; }

    public string? Name { get; set; }

    public string WriteOnly { set => WrittenValue = value; }

    public string? WrittenValue { get; private set; }

    public string this[int index] => Name ?? string.Empty;
}

internal sealed class CasedEntity
{
    public string? Value { get; set; }

#pragma warning disable IDE1006 // Two properties that differ only by case are the point of this test.
    public string? value { get; set; }
#pragma warning restore IDE1006
}

internal class BaseEntity
{
    public int Value { get; set; }
}

internal sealed class DerivedEntity : BaseEntity
{
    public new string? Value { get; set; }
}
