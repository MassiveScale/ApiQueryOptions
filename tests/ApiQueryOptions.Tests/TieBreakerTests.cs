using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

[TestClass]
public class TieBreakerTests
{
    private static readonly TieEntity[] Data =
    [
        new() { Id = 3, Group = "a" },
        new() { Id = 1, Group = "b" },
        new() { Id = 2, Group = "a" },
        new() { Id = 4, Group = "b" },
    ];

    private static ApiQueryOptions<TieEntity> Options(ApiQueryOptionsSettings settings, params (string Key, string Value)[] pairs) =>
        new(new QueryCollection(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Value))), settings);

    private static List<string> OrderingCalls(IQueryable<TieEntity> query) =>
        ExpressionInspector.MethodNames(query.Expression)
            .Where(name => name.StartsWith("OrderBy", StringComparison.Ordinal) || name.StartsWith("ThenBy", StringComparison.Ordinal))
            .ToList();

    [TestMethod]
    public void Apply_TopWithoutOrderBy_OrdersByTieBreaker()
    {
        var settings = new ApiQueryOptionsSettings { TieBreakerProperty = "Id" };

        IQueryable<TieEntity> query = Data.AsQueryable().Apply(Options(settings, ("$top", "2")));

        query.Select(e => e.Id).Should().Equal(1, 2);
        OrderingCalls(query).Should().Equal("OrderBy");
    }

    [TestMethod]
    public void Apply_SkipWithOrderBy_AppendsThenBy()
    {
        var settings = new ApiQueryOptionsSettings { TieBreakerProperty = "Id" };

        IQueryable<TieEntity> query = Data.AsQueryable().Apply(Options(settings, ("$orderby", "Group desc"), ("$skip", "1")));

        query.Select(e => e.Id).Should().Equal(4, 2, 3);
        OrderingCalls(query).Should().Equal("ThenBy", "OrderByDescending");
    }

    [TestMethod]
    [DataRow("id desc")]
    [DataRow("Group, ID")]
    public void Apply_OrderByAlreadyContainsTieBreaker_AddsNothing(string orderBy)
    {
        var settings = new ApiQueryOptionsSettings { TieBreakerProperty = "Id" };

        IQueryable<TieEntity> query = Data.AsQueryable().Apply(Options(settings, ("$orderby", orderBy), ("$top", "10")));

        OrderingCalls(query).Should().HaveCount(orderBy.Split(',').Length);
    }

    [TestMethod]
    public void Apply_NoPaging_AddsNothing()
    {
        var settings = new ApiQueryOptionsSettings { TieBreakerProperty = "Id" };

        IQueryable<TieEntity> query = Data.AsQueryable().Apply(Options(settings, ("$orderby", "Group")));

        OrderingCalls(query).Should().Equal("OrderBy");
    }

    [TestMethod]
    public void Apply_NoTieBreaker_AddsNothing()
    {
        IQueryable<TieEntity> query = Data.AsQueryable().Apply(Options(new ApiQueryOptionsSettings(), ("$top", "2")));

        OrderingCalls(query).Should().BeEmpty();
    }

    [TestMethod]
    public void Apply_EmptyOrderByItems_UsesOrderBy()
    {
        var settings = new ApiQueryOptionsSettings { TieBreakerProperty = "Id" };

        IQueryable<TieEntity> query = Data.AsQueryable().Apply(Options(settings, ("$orderby", " , "), ("$top", "2")));

        OrderingCalls(query).Should().Equal("OrderBy");
    }

    [TestMethod]
    public void Apply_UnknownTieBreaker_ThrowsInvalidQueryOption()
    {
        var settings = new ApiQueryOptionsSettings { TieBreakerProperty = "Missing" };

        Action act = () => Data.AsQueryable().Apply(Options(settings, ("$top", "2")));

        InvalidQueryOptionException ex = act.Should().Throw<InvalidQueryOptionException>().Which;
        ex.OptionName.Should().Be("orderby");
        ex.Property.Should().Be("Missing");
    }

    [TestMethod]
    public void Apply_OrderByDisabled_StillAppliesTieBreaker()
    {
        var settings = new ApiQueryOptionsSettings { TieBreakerProperty = "Id", OrderByEnabled = false };

        IQueryable<TieEntity> query = Data.AsQueryable().Apply(Options(settings, ("$orderby", "Group desc"), ("$top", "4")));

        query.Select(e => e.Id).Should().Equal(1, 2, 3, 4);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Settings_BlankTieBreaker_ThrowsArgumentException(string value)
    {
        Action act = () => _ = new ApiQueryOptionsSettings { TieBreakerProperty = value };

        act.Should().Throw<ArgumentException>().WithParameterName("TieBreakerProperty");
    }

    [TestMethod]
    public void Attribute_TieBreaker_OverridesRegisteredValue()
    {
        var settings = new ApiQueryOptionsSettings { TieBreakerProperty = "Id" };

        new ApiQueryOptionsAttribute { TieBreakerProperty = "Group" }.ApplyTo(settings).TieBreakerProperty.Should().Be("Group");
        new ApiQueryOptionsAttribute().ApplyTo(settings).TieBreakerProperty.Should().Be("Id");
    }

    [TestMethod]
    public void Apply_SkipTokenPage_KeepsStableOrder()
    {
        var settings = new ApiQueryOptionsSettings { TieBreakerProperty = "Id" };
        ApiQueryOptions<TieEntity> page1 = Options(settings, ("$orderby", "Group"), ("$top", "2"));
        string token = page1.GetNextToken(resultCount: 2)!;
        ApiQueryOptions<TieEntity> page2 = Options(settings, ("$skiptoken", token));

        Data.AsQueryable().Apply(page1).Select(e => e.Id).Should().Equal(2, 3);
        Data.AsQueryable().Apply(page2).Select(e => e.Id).Should().Equal(1, 4);
    }
}

internal sealed class TieEntity
{
    public string Group { get; set; } = string.Empty;

    public int Id { get; set; }
}
