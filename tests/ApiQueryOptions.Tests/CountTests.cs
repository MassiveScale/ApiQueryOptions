using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

[TestClass]
public class CountTests
{
    private static IQueryable<SampleEntity> Q() => new List<SampleEntity>
    {
        new() { Id = 1, Name = "Alice", Status = "Active" },
        new() { Id = 2, Name = "Bob",   Status = "Inactive" },
        new() { Id = 3, Name = "Carol", Status = "Active" },
        new() { Id = 4, Name = "Dave",  Status = "Pending" },
        new() { Id = 5, Name = "Eve",   Status = "Active" },
    }.AsQueryable();

    private static ApiQueryOptions<SampleEntity> Options(
        ApiQueryOptionsSettings? settings,
        params (string key, string value)[] pairs)
    {
        Dictionary<string, StringValues> dict = pairs.ToDictionary(
            p => p.key,
            p => new StringValues(p.value));
        return new ApiQueryOptions<SampleEntity>(new QueryCollection(dict), settings);
    }

    private static ApiQueryOptions<SampleEntity> Options(params (string key, string value)[] pairs) =>
        Options(settings: null, pairs);

    [TestMethod]
    public void Count_WithFilterAndPaging_CountsAllFilteredItems()
    {
        ApiQueryOptions<SampleEntity> options = Options(
            ("$filter", "Status eq 'Active'"),
            ("$orderby", "Name desc"),
            ("$skip", "1"),
            ("$top", "1"));

        Q().Count(options).Should().Be(3);
    }

    [TestMethod]
    public void Count_NoFilter_CountsAllItems()
    {
        Q().Count(Options(("$top", "2"))).Should().Be(5);
    }

    [TestMethod]
    public void Count_NoMatches_ReturnsZero()
    {
        Q().Count(Options(("$filter", "Status eq 'Archived'"))).Should().Be(0);
    }

    [TestMethod]
    public void Count_SecondPageFromSkipToken_CountsAllFilteredItems()
    {
        ApiQueryOptions<SampleEntity> page1 = Options(("$filter", "Status eq 'Active'"), ("$top", "1"));
        string token = page1.GetNextToken(resultCount: 1)!;

        ApiQueryOptions<SampleEntity> page2 = Options(("$skiptoken", token));

        page2.Skip!.Value.Should().Be(1);
        Q().Count(page2).Should().Be(3);
    }

    [TestMethod]
    public void Count_SkipTokenFilterWithFilterDisabled_IgnoresFilterAndCountsAllItems()
    {
        ApiQueryOptions<SampleEntity> page1 = Options(("$filter", "Status eq 'Active'"), ("$top", "1"));
        string token = page1.GetNextToken(resultCount: 1)!;
        ApiQueryOptions<SampleEntity> page2 = Options(new ApiQueryOptionsSettings { FilterEnabled = false }, ("$skiptoken", token));

        Q().Count(page2).Should().Be(5);
    }

    [TestMethod]
    public void Count_FilterDisabledInSettings_IgnoresFilterAndCountsAllItems()
    {
        ApiQueryOptions<SampleEntity> options = Options(
            new ApiQueryOptionsSettings { FilterEnabled = false },
            ("$filter", "Status eq 'Active'"));

        Q().Count(options).Should().Be(5);
    }

    [TestMethod]
    public void Count_UnknownFilterProperty_ThrowsInvalidQueryOption()
    {
        Func<int> act = () => Q().Count(Options(("$filter", "Missing eq 'x'")));

        act.Should().Throw<InvalidQueryOptionException>()
            .Which.Property.Should().Be("Missing");
    }

    [TestMethod]
    public void Count_NullOptions_ThrowsArgumentNull()
    {
        Func<int> act = () => Q().Count((ApiQueryOptions<SampleEntity>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [TestMethod]
    public void ApplyFilter_Options_IgnoresOrderByAndPaging()
    {
        ApiQueryOptions<SampleEntity> options = Options(
            ("$filter", "Status eq 'Active'"),
            ("$orderby", "Name desc"),
            ("$skip", "1"),
            ("$top", "1"));

        Q().ApplyFilter(options).Select(e => e.Id).Should().Equal(1, 3, 5);
    }

    [TestMethod]
    public void ApplyFilter_OptionsWithoutFilter_ReturnsSameQuery()
    {
        IQueryable<SampleEntity> query = Q();

        query.ApplyFilter(Options(("$top", "2"))).Should().BeSameAs(query);
    }

    [TestMethod]
    public void ApplyFilter_NullOptions_ThrowsArgumentNull()
    {
        Func<IQueryable<SampleEntity>> act = () => Q().ApplyFilter((ApiQueryOptions<SampleEntity>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }
}
