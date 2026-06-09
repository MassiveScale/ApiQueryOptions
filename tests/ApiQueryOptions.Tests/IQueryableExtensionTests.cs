using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using ApiQueryOptions.Options;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

[TestClass]
public class IQueryableExtensionTests
{
    private static List<SampleEntity> Data() =>
    [
        new() { Id = 1, Name = "Alice", Age = 30, Price = 10.00m, Status = "Active",   IsActive = true,  Score = 80 },
        new() { Id = 2, Name = "Bob",   Age = 25, Price = 20.00m, Status = "Inactive", IsActive = false, Score = 50 },
        new() { Id = 3, Name = "Carol", Age = 35, Price = 15.00m, Status = "Active",   IsActive = true,  Score = 90 },
        new() { Id = 4, Name = "Dave",  Age = 20, Price = 5.00m,  Status = "Pending",  IsActive = false, Score = 40 },
        new() { Id = 5, Name = "Eve",   Age = 28, Price = 30.00m, Status = "Active",   IsActive = true,  Score = 70 },
    ];

    private static IQueryable<SampleEntity> Q() => Data().AsQueryable();

    private static ApiQueryOptions<SampleEntity> Options(
        params (string key, string value)[] pairs)
    {
        Dictionary<string, StringValues> dict = pairs.ToDictionary(
            p => p.key,
            p => new StringValues(p.value));
        return new ApiQueryOptions<SampleEntity>(new QueryCollection(dict));
    }

    [TestMethod]
    public void ApplyTop_LimitsResults()
    {
        var result = Q().Apply(Options(("$top", "2"))).ToList();
        result.Should().HaveCount(2);
    }

    [TestMethod]
    public void ApplyTop_Disabled_Throws()
    {
        var settings = new ApiQueryOptionsSettings { TopEnabled = false };
        var opt = new ApiQueryOptions<SampleEntity>(new QueryCollection(), settings);
        var topOpt = new TopQueryOption(3);
        Func<IQueryable<SampleEntity>> act = () => Q().ApplyTop(topOpt, settings);
        act.Should().Throw<QueryOptionDisabledException>()
            .Which.OptionName.Should().Be("top");
    }

    [TestMethod]
    public void ApplySkip_SkipsItems()
    {
        var result = Q().Apply(Options(("$skip", "3"))).ToList();
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Dave");
    }

    [TestMethod]
    public void ApplySkip_Disabled_Throws()
    {
        var settings = new ApiQueryOptionsSettings { SkipEnabled = false };
        Func<IQueryable<SampleEntity>> act = () => Q().ApplySkip(new SkipQueryOption(1), settings);
        act.Should().Throw<QueryOptionDisabledException>()
            .Which.OptionName.Should().Be("skip");
    }

    [TestMethod]
    public void ApplyFilter_Eq_String_ReturnsMatches()
    {
        var result = Q().Apply(Options(("$filter", "Status eq 'Active'"))).ToList();
        result.Should().AllSatisfy(e => e.Status.Should().Be("Active"));
        result.Should().HaveCount(3);
    }

    [TestMethod]
    public void ApplyFilter_Eq_CaseInsensitive_ReturnsMatches()
    {
        var result = Q().Apply(Options(("$filter", "Status eq 'active'"))).ToList();
        result.Should().HaveCount(3);
    }

    [TestMethod]
    public void ApplyFilter_Ne_String_FiltersOut()
    {
        var result = Q().Apply(Options(("$filter", "Status ne 'Active'"))).ToList();
        result.Should().NotContain(e => e.Status == "Active");
    }

    [TestMethod]
    public void ApplyFilter_Lt_Int_ReturnsLess()
    {
        var result = Q().Apply(Options(("$filter", "Age lt 25"))).ToList();
        result.Should().AllSatisfy(e => e.Age.Should().BeLessThan(25));
    }

    [TestMethod]
    public void ApplyFilter_Gt_Int_ReturnsGreater()
    {
        var result = Q().Apply(Options(("$filter", "Age gt 28"))).ToList();
        result.Should().AllSatisfy(e => e.Age.Should().BeGreaterThan(28));
    }

    [TestMethod]
    public void ApplyFilter_Le_Int_ReturnsLessOrEqual()
    {
        var result = Q().Apply(Options(("$filter", "Age le 25"))).ToList();
        result.Should().AllSatisfy(e => e.Age.Should().BeLessThanOrEqualTo(25));
    }

    [TestMethod]
    public void ApplyFilter_Ge_Int_ReturnsGreaterOrEqual()
    {
        var result = Q().Apply(Options(("$filter", "Age ge 30"))).ToList();
        result.Should().AllSatisfy(e => e.Age.Should().BeGreaterThanOrEqualTo(30));
    }

    [TestMethod]
    public void ApplyFilter_Eq_Bool_ReturnsMatches()
    {
        var result = Q().Apply(Options(("$filter", "IsActive eq true"))).ToList();
        result.Should().AllSatisfy(e => e.IsActive.Should().BeTrue());
        result.Should().HaveCount(3);
    }

    [TestMethod]
    public void ApplyFilter_And_CombinesConditions()
    {
        var result = Q().Apply(Options(("$filter", "Status eq 'Active' and Age gt 28"))).ToList();
        result.Should().AllSatisfy(e =>
        {
            e.Status.Should().Be("Active");
            e.Age.Should().BeGreaterThan(28);
        });
    }

    [TestMethod]
    public void ApplyFilter_Or_BothBranches()
    {
        var result = Q().Apply(Options(("$filter", "Name eq 'Alice' or Name eq 'Bob'"))).ToList();
        result.Should().HaveCount(2);
        result.Select(e => e.Name).Should().Contain(["Alice", "Bob"]);
    }

    [TestMethod]
    public void ApplyFilter_StartsWith_ReturnsMatches()
    {
        var result = Q().Apply(Options(("$filter", "startswith(Name, 'A')"))).ToList();
        result.Should().ContainSingle(e => e.Name == "Alice");
    }

    [TestMethod]
    public void ApplyFilter_EndsWith_ReturnsMatches()
    {
        var result = Q().Apply(Options(("$filter", "endswith(Name, 've')"))).ToList();
        result.Should().ContainSingle(e => e.Name == "Dave");
    }

    [TestMethod]
    public void ApplyFilter_Contains_ReturnsMatches()
    {
        var result = Q().Apply(Options(("$filter", "contains(Name, 'ar')"))).ToList();
        result.Should().ContainSingle(e => e.Name == "Carol");
    }

    [TestMethod]
    public void ApplyFilter_Disabled_Throws()
    {
        var settings = new ApiQueryOptionsSettings { FilterEnabled = false };
        Func<IQueryable<SampleEntity>> act = () => Q().ApplyFilter(new FilterQueryOption("Name eq 'x'"), settings);
        act.Should().Throw<QueryOptionDisabledException>()
            .Which.OptionName.Should().Be("filter");
    }

    [TestMethod]
    public void ApplyOrderBy_SingleAsc_SortsCorrectly()
    {
        var result = Q().Apply(Options(("$orderby", "Name asc"))).ToList();
        result.Select(e => e.Name).Should().BeInAscendingOrder();
    }

    [TestMethod]
    public void ApplyOrderBy_SingleDesc_SortsCorrectly()
    {
        var result = Q().Apply(Options(("$orderby", "Age desc"))).ToList();
        result.Select(e => e.Age).Should().BeInDescendingOrder();
    }

    [TestMethod]
    public void ApplyOrderBy_Multiple_SortsCorrectly()
    {
        var result = Q().Apply(Options(("$orderby", "Status asc, Name asc"))).ToList();
        var statusGroups = result.GroupBy(e => e.Status).ToList();
        foreach (IGrouping<string, SampleEntity>? grp in statusGroups)
        {
            grp.Select(e => e.Name).Should().BeInAscendingOrder();
        }
    }

    [TestMethod]
    public void ApplyOrderBy_EmptyItems_ReturnsSameQuery()
    {
        var result = Q().ApplyOrderBy(new OrderByQueryOption(string.Empty), new ApiQueryOptionsSettings()).ToList();
        result.Should().HaveCount(5);
    }

    [TestMethod]
    public void ApplyOrderBy_Disabled_Throws()
    {
        var settings = new ApiQueryOptionsSettings { OrderByEnabled = false };
        Func<IQueryable<SampleEntity>> act = () => Q().ApplyOrderBy(new OrderByQueryOption("Name asc"), settings);
        act.Should().Throw<QueryOptionDisabledException>()
            .Which.OptionName.Should().Be("orderby");
    }

    [TestMethod]
    public void Apply_FilterThenSkipThenTop_CombinesCorrectly()
    {
        ApiQueryOptions<SampleEntity> opts = Options(
            ("$filter", "Status eq 'Active'"),
            ("$orderby", "Name asc"),
            ("$skip", "1"),
            ("$top", "1"));
        var result = Q().Apply(opts).ToList();
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Carol");
    }

    [TestMethod]
    public void Apply_NoOptions_ReturnsAll()
    {
        var opts = new ApiQueryOptions<SampleEntity>(new QueryCollection());
        var result = Q().Apply(opts).ToList();
        result.Should().HaveCount(5);
    }

    [TestMethod]
    public void ApplyFilter_DottedProperty_Works()
    {
        IQueryable<SampleEntity> data = new[]
        {
            new SampleEntity { Id = 1, Name = "A", Address = new SampleAddress { City = "Seattle" } },
            new SampleEntity { Id = 2, Name = "B", Address = new SampleAddress { City = "Portland" } },
        }.AsQueryable();

        var filter = new FilterQueryOption("Address.City eq 'Seattle'");
        var result = data.ApplyFilter(filter, new ApiQueryOptionsSettings()).ToList();
        result.Should().ContainSingle(e => e.Name == "A");
    }

    [TestMethod]
    public void ApplyFilter_PropertyNotFound_ThrowsInvalidOperation()
    {
        var filter = new FilterQueryOption("NonExistent eq 'x'");
        Func<List<SampleEntity>> act = () => Q().ApplyFilter(filter, new ApiQueryOptionsSettings()).ToList();
        act.Should().Throw<InvalidOperationException>();
    }
}
