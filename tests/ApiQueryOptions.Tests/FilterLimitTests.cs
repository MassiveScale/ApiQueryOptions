using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Filter;
using ApiQueryOptions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

[TestClass]
public class FilterLimitTests
{
    private static string Nest(int depth, string inner = "Id eq 1") =>
        new string('(', depth) + inner + new string(')', depth);

    private static string OrChain(int comparisons) =>
        string.Join(" or ", Enumerable.Range(1, comparisons).Select(i => $"Id eq {i}"));

    [TestMethod]
    public void Ctor_MaxDepthNotPositive_ThrowsArgumentOutOfRange()
    {
        Action act = () => _ = new FilterParser(0, 10);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxDepth");
    }

    [TestMethod]
    public void Ctor_MaxNodeCountNotPositive_ThrowsArgumentOutOfRange()
    {
        Action act = () => _ = new FilterParser(10, -1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxNodeCount");
    }

    [TestMethod]
    public void FilterQueryOption_NullSettings_ThrowsArgumentNull()
    {
        Action act = () => _ = new FilterQueryOption("Id eq 1", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [TestMethod]
    public void FilterQueryOption_SettingsLimitsAreUsed()
    {
        var settings = new ApiQueryOptionsSettings { MaxFilterDepth = 1 };
        var option = new FilterQueryOption(Nest(2), settings);

        Func<FilterClause> act = () => option.FilterClause;

        act.Should().Throw<FilterParseException>().WithMessage("*maximum nesting depth of 1*");
    }

    [TestMethod]
    public void Parse_AtDefaultMaxDepth_Succeeds()
    {
        FilterClause clause = new FilterParser().Parse(Nest(ApiQueryOptionsSettings.DefaultMaxFilterDepth));

        clause.Expression.Should().BeOfType<BinaryFilterNode>();
    }

    [TestMethod]
    public void Parse_BeyondDefaultMaxDepth_ThrowsFilterParseException()
    {
        int depth = ApiQueryOptionsSettings.DefaultMaxFilterDepth + 1;

        Action act = () => new FilterParser().Parse(Nest(depth));

        act.Should().Throw<FilterParseException>()
            .Where(ex => ex.Position == depth - 1)
            .WithMessage($"*maximum nesting depth of {ApiQueryOptionsSettings.DefaultMaxFilterDepth}*");
    }

    [TestMethod]
    public void Parse_HostileNesting_ThrowsInsteadOfOverflowingStack()
    {
        // ~200 KB of '(' used to recurse once per character and crash the process.
        string hostile = new string('(', 100_000) + "Id eq 1" + new string(')', 100_000);

        Action act = () => new FilterParser().Parse(hostile);

        act.Should().Throw<FilterParseException>();
    }

    [TestMethod]
    public void Parse_SiblingGroups_DoNotAccumulateDepth()
    {
        FilterClause clause = new FilterParser(maxDepth: 1, maxNodeCount: 100)
            .Parse("(Id eq 1) and (Id eq 2) or (Id eq 3)");

        clause.Expression.Should().BeOfType<LogicalFilterNode>();
    }

    [TestMethod]
    public void Parse_CustomDepth_AtLimitSucceeds_BeyondLimitThrows()
    {
        var parser = new FilterParser(maxDepth: 2, maxNodeCount: 100);

        parser.Parse(Nest(2)).Should().NotBeNull();
        Action act = () => parser.Parse(Nest(3));
        act.Should().Throw<FilterParseException>();
    }

    [TestMethod]
    public void Parse_AtMaxNodeCount_Succeeds()
    {
        // 2 comparisons + 1 'and' = 3 nodes.
        FilterClause clause = new FilterParser(maxDepth: 10, maxNodeCount: 3).Parse("Id eq 1 and Name eq 'x'");

        clause.Expression.Should().BeOfType<LogicalFilterNode>();
    }

    [TestMethod]
    public void Parse_ExceedsMaxNodeCountOnComparison_Throws()
    {
        // The 'and' is node 2 of 2; the second comparison is node 3.
        Action act = () => new FilterParser(maxDepth: 10, maxNodeCount: 2).Parse("Id eq 1 and Name eq 'x'");

        act.Should().Throw<FilterParseException>()
            .Where(ex => ex.Position == 12)
            .WithMessage("*maximum of 2 nodes*");
    }

    [TestMethod]
    public void Parse_ExceedsMaxNodeCountOnLogicalOperator_Throws()
    {
        Action act = () => new FilterParser(maxDepth: 10, maxNodeCount: 1).Parse("Id eq 1 or Id eq 2");

        act.Should().Throw<FilterParseException>().Where(ex => ex.Position == 8);
    }

    [TestMethod]
    public void Parse_ExceedsMaxNodeCountOnAnd_Throws()
    {
        Action act = () => new FilterParser(maxDepth: 10, maxNodeCount: 1).Parse("Id eq 1 and Id eq 2");

        act.Should().Throw<FilterParseException>().Where(ex => ex.Position == 8);
    }

    [TestMethod]
    public void Parse_FunctionCallsCountAsNodes()
    {
        var parser = new FilterParser(maxDepth: 10, maxNodeCount: 1);

        parser.Parse("contains(Name, 'x')").Expression.Should().BeOfType<FunctionFilterNode>();
        Action act = () => parser.Parse("contains(Name, 'x') and startswith(Name, 'y')");
        act.Should().Throw<FilterParseException>();
    }

    [TestMethod]
    public void Parse_FunctionCallBeyondMaxNodeCount_ReportsFunctionPosition()
    {
        Action act = () => new FilterParser(maxDepth: 10, maxNodeCount: 2).Parse("Id eq 1 or contains(Name, 'x')");

        act.Should().Throw<FilterParseException>().Where(ex => ex.Position == 11);
    }

    [TestMethod]
    public void Parse_DefaultNodeLimit_AllowsNinetyNineNodes_RejectsOneHundredOne()
    {
        var parser = new FilterParser();

        parser.Parse(OrChain(50)).Should().NotBeNull();
        Action act = () => parser.Parse(OrChain(51));
        act.Should().Throw<FilterParseException>();
    }

    [TestMethod]
    public void Parse_ReusedParser_ResetsCountersBetweenCalls()
    {
        var parser = new FilterParser(maxDepth: 2, maxNodeCount: 3);

        Action fail = () => parser.Parse(Nest(3));
        fail.Should().Throw<FilterParseException>();

        parser.Parse(Nest(2, "Id eq 1 and Id eq 2")).Should().NotBeNull();
        parser.Parse(Nest(2, "Id eq 1 and Id eq 2")).Should().NotBeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_FilterBeyondConfiguredDepth_ThrowsOnAccess()
    {
        var settings = new ApiQueryOptionsSettings { MaxFilterDepth = 2, MaxFilterNodeCount = 5 };
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["$filter"] = Nest(3) });

        var options = new ApiQueryOptions<object>(query, settings);
        Func<FilterClause> act = () => options.Filter!.FilterClause;

        act.Should().Throw<FilterParseException>();
    }

    [TestMethod]
    public void Settings_FilterLimitDefaults()
    {
        var settings = new ApiQueryOptionsSettings();

        settings.MaxFilterDepth.Should().Be(32);
        settings.MaxFilterNodeCount.Should().Be(100);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Settings_MaxFilterDepthNotPositive_Throws(int value)
    {
        Action act = () => _ = new ApiQueryOptionsSettings { MaxFilterDepth = value };
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(ApiQueryOptionsSettings.MaxFilterDepth));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Settings_MaxFilterNodeCountNotPositive_Throws(int value)
    {
        Action act = () => _ = new ApiQueryOptionsSettings { MaxFilterNodeCount = value };
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(ApiQueryOptionsSettings.MaxFilterNodeCount));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void Settings_MaxPageSizeNotPositive_Throws(int value)
    {
        Action act = () => _ = new ApiQueryOptionsSettings { MaxPageSize = value };
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(ApiQueryOptionsSettings.MaxPageSize));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void Settings_DefaultPageSizeNotPositive_Throws(int value)
    {
        Action act = () => _ = new ApiQueryOptionsSettings { DefaultPageSize = value };
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(ApiQueryOptionsSettings.DefaultPageSize));
    }

    [TestMethod]
    public void Settings_NullPageSizes_AreAllowed()
    {
        var settings = new ApiQueryOptionsSettings { DefaultPageSize = null, MaxPageSize = null };

        settings.DefaultPageSize.Should().BeNull();
        settings.MaxPageSize.Should().BeNull();
    }

    [TestMethod]
    public void Settings_PositiveLimits_AreStored()
    {
        var settings = new ApiQueryOptionsSettings
        {
            DefaultPageSize = 1,
            MaxFilterDepth = 1,
            MaxFilterNodeCount = 1,
            MaxPageSize = 1,
        };

        settings.DefaultPageSize.Should().Be(1);
        settings.MaxFilterDepth.Should().Be(1);
        settings.MaxFilterNodeCount.Should().Be(1);
        settings.MaxPageSize.Should().Be(1);
    }

    [TestMethod]
    public void WithPageSizeOverrides_ZeroOverride_Throws()
    {
        Action act = () => new ApiQueryOptionsSettings().WithPageSizeOverrides(maxPageSize: 0, defaultPageSize: null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
