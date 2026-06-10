using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Options;
using ApiQueryOptions.SkipToken;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

[TestClass]
public class QueryOptionTests
{
    [TestMethod]
    public void Settings_DefaultsAllEnabled()
    {
        var s = new ApiQueryOptionsSettings();
        s.DefaultPageSize.Should().BeNull();
        s.ExpandEnabled.Should().BeTrue();
        s.FilterEnabled.Should().BeTrue();
        s.MaxPageSize.Should().BeNull();
        s.OrderByEnabled.Should().BeTrue();
        s.SkipEnabled.Should().BeTrue();
        s.SkipTokenEnabled.Should().BeTrue();
        s.StringComparison.Should().Be(StringComparison.OrdinalIgnoreCase);
        s.TopEnabled.Should().BeTrue();
    }

    [TestMethod]
    public void Settings_WithPageSizeOverrides_MaxPageSize_ReplacesValue()
    {
        var original = new ApiQueryOptionsSettings { FilterEnabled = false, MaxPageSize = 50 };
        ApiQueryOptionsSettings updated = original.WithPageSizeOverrides(maxPageSize: 100, defaultPageSize: null);

        updated.MaxPageSize.Should().Be(100);
        updated.DefaultPageSize.Should().BeNull();
        updated.FilterEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void Settings_WithPageSizeOverrides_DefaultPageSize_ReplacesValue()
    {
        var original = new ApiQueryOptionsSettings { DefaultPageSize = 10 };
        ApiQueryOptionsSettings updated = original.WithPageSizeOverrides(maxPageSize: null, defaultPageSize: 25);

        updated.DefaultPageSize.Should().Be(25);
        updated.MaxPageSize.Should().BeNull();
    }

    [TestMethod]
    public void Settings_WithPageSizeOverrides_NullArgs_PreservesExistingValues()
    {
        var original = new ApiQueryOptionsSettings { MaxPageSize = 50, DefaultPageSize = 10 };
        ApiQueryOptionsSettings updated = original.WithPageSizeOverrides(maxPageSize: null, defaultPageSize: null);

        updated.MaxPageSize.Should().Be(50);
        updated.DefaultPageSize.Should().Be(10);
    }

    [TestMethod]
    public void ApiQueryOptions_MaxPageSize_ClampsTopWhenExceeded()
    {
        IQueryCollection q = BuildQuery(("$top", "200"));
        var settings = new ApiQueryOptionsSettings { MaxPageSize = 100 };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);

        options.Top!.Value.Should().Be(100);
    }

    [TestMethod]
    public void ApiQueryOptions_MaxPageSize_TopBelowLimit_NotClamped()
    {
        IQueryCollection q = BuildQuery(("$top", "50"));
        var settings = new ApiQueryOptionsSettings { MaxPageSize = 100 };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);

        options.Top!.Value.Should().Be(50);
    }

    [TestMethod]
    public void ApiQueryOptions_MaxPageSize_TopEqualsLimit_NotClamped()
    {
        IQueryCollection q = BuildQuery(("$top", "100"));
        var settings = new ApiQueryOptionsSettings { MaxPageSize = 100 };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);

        options.Top!.Value.Should().Be(100);
    }

    [TestMethod]
    public void ApiQueryOptions_MaxPageSize_Null_TopUnchanged()
    {
        IQueryCollection q = BuildQuery(("$top", "999"));
        var settings = new ApiQueryOptionsSettings { MaxPageSize = null };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);

        options.Top!.Value.Should().Be(999);
    }

    [TestMethod]
    public void ApiQueryOptions_DefaultPageSize_NoTopProvided_AppliesDefault()
    {
        IQueryCollection q = BuildQuery(("$filter", "Name eq 'Alice'"));
        var settings = new ApiQueryOptionsSettings { DefaultPageSize = 25 };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);

        options.Top!.Value.Should().Be(25);
    }

    [TestMethod]
    public void ApiQueryOptions_DefaultPageSize_TopProvided_DefaultNotApplied()
    {
        IQueryCollection q = BuildQuery(("$top", "10"));
        var settings = new ApiQueryOptionsSettings { DefaultPageSize = 25 };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);

        options.Top!.Value.Should().Be(10);
    }

    [TestMethod]
    public void ApiQueryOptions_DefaultPageSize_TopDisabled_DefaultNotApplied()
    {
        IQueryCollection q = BuildQuery(("$filter", "Name eq 'Alice'"));
        var settings = new ApiQueryOptionsSettings { DefaultPageSize = 25, TopEnabled = false };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);

        options.Top.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_DefaultPageSize_Null_TopRemainsNull()
    {
        IQueryCollection q = BuildQuery(("$filter", "Name eq 'x'"));
        var settings = new ApiQueryOptionsSettings { DefaultPageSize = null };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);

        options.Top.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_MaxPageSizeAndDefaultPageSize_DefaultExceedsMax_ClampsDefault()
    {
        IQueryCollection q = BuildQuery(("$filter", "Name eq 'x'"));
        var settings = new ApiQueryOptionsSettings { MaxPageSize = 10, DefaultPageSize = 50 };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);

        options.Top!.Value.Should().Be(10);
    }

    [TestMethod]
    public void TopQueryOption_ValidInt_ParsesCorrectly()
    {
        var opt = TopQueryOption.TryParse("25");
        opt.Should().NotBeNull();
        opt!.Value.Should().Be(25);
    }

    [TestMethod]
    public void TopQueryOption_NonInt_ReturnsNull()
        => TopQueryOption.TryParse("abc").Should().BeNull();

    [TestMethod]
    public void TopQueryOption_NullRaw_ReturnsNull()
        => TopQueryOption.TryParse(null).Should().BeNull();

    [TestMethod]
    public void TopQueryOption_EmptyRaw_ReturnsNull()
        => TopQueryOption.TryParse(string.Empty).Should().BeNull();

    [TestMethod]
    public void SkipQueryOption_ValidInt_ParsesCorrectly()
    {
        var opt = SkipQueryOption.TryParse("10");
        opt.Should().NotBeNull();
        opt!.Value.Should().Be(10);
    }

    [TestMethod]
    public void SkipQueryOption_NonInt_ReturnsNull()
        => SkipQueryOption.TryParse("3.5").Should().BeNull();

    [TestMethod]
    public void OrderByQueryOption_SingleAsc_ParsesCorrectly()
    {
        var opt = new OrderByQueryOption("Name asc");
        opt.Items.Should().HaveCount(1);
        opt.Items[0].Property.Should().Be("Name");
        opt.Items[0].Descending.Should().BeFalse();
    }

    [TestMethod]
    public void OrderByQueryOption_SingleDesc_ParsesCorrectly()
    {
        var opt = new OrderByQueryOption("CreatedAt desc");
        opt.Items[0].Descending.Should().BeTrue();
    }

    [TestMethod]
    public void OrderByQueryOption_NoDirection_DefaultsToAsc()
    {
        var opt = new OrderByQueryOption("Name");
        opt.Items[0].Descending.Should().BeFalse();
    }

    [TestMethod]
    public void OrderByQueryOption_MultipleItems_ParsesAll()
    {
        var opt = new OrderByQueryOption("Name asc, Age desc");
        opt.Items.Should().HaveCount(2);
        opt.Items[1].Descending.Should().BeTrue();
    }

    [TestMethod]
    public void OrderByQueryOption_Empty_ReturnsEmptyList()
    {
        var opt = new OrderByQueryOption(string.Empty);
        opt.Items.Should().BeEmpty();
    }

    [TestMethod]
    public void ExpandQueryOption_SingleNav_Parsed()
    {
        var opt = new ExpandQueryOption("Orders");
        opt.NavigationProperties.Should().ContainSingle("Orders");
    }

    [TestMethod]
    public void ExpandQueryOption_MultipleNav_AllParsed()
    {
        var opt = new ExpandQueryOption("Orders,Tags, Profile");
        opt.NavigationProperties.Should().HaveCount(3);
    }

    [TestMethod]
    public void ExpandQueryOption_Empty_ReturnsEmptyList()
        => new ExpandQueryOption(string.Empty).NavigationProperties.Should().BeEmpty();

    [TestMethod]
    public void ExpandQueryOption_Null_ReturnsEmptyList()
        => new ExpandQueryOption(null!).NavigationProperties.Should().BeEmpty();

    [TestMethod]
    public void SkipTokenQueryOption_StoresRawValue()
    {
        var opt = new SkipTokenQueryOption("abc123");
        opt.RawValue.Should().Be("abc123");
    }

    [TestMethod]
    public void FilterQueryOption_StoresRawValue()
    {
        var opt = new FilterQueryOption("Name eq 'x'");
        opt.RawValue.Should().Be("Name eq 'x'");
    }

    private static IQueryCollection BuildQuery(params (string key, string value)[] pairs)
    {
        Dictionary<string, StringValues> dict = pairs.ToDictionary(
            p => p.key,
            p => new StringValues(p.value));
        return new QueryCollection(dict);
    }

    [TestMethod]
    public void ApiQueryOptions_NullQuery_AllOptionsNull()
    {
        var options = new ApiQueryOptions<SampleEntity>(null!);
        options.Filter.Should().BeNull();
        options.Expand.Should().BeNull();
        options.OrderBy.Should().BeNull();
        options.Top.Should().BeNull();
        options.Skip.Should().BeNull();
        options.SkipToken.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_AllOptionsPresent_WithSkipToken_TokenApplied()
    {
        // Encode a token that captures all options
        string token = SkipTokenEncoder.Encode(new ApiQueryOptions<SampleEntity>(BuildQuery(
            ("$filter", "Name eq 'x'"),
            ("$expand", "Orders"),
            ("$orderby", "Name asc"),
            ("$top", "10"),
            ("$skip", "5"))));

        // Only the token is in the URL — all options come from the token
        IQueryCollection q = BuildQuery(("$skiptoken", token));

        var options = new ApiQueryOptions<SampleEntity>(q);
        options.SkipToken.Should().NotBeNull();
        options.Filter.Should().NotBeNull();
        options.Expand.Should().NotBeNull();
        options.OrderBy.Should().NotBeNull();
        options.Top!.Value.Should().Be(10);
        options.Skip!.Value.Should().Be(5);
    }

    [TestMethod]
    public void ApiQueryOptions_AllIndividualOptionsPresent_NoSkipToken_Parsed()
    {
        IQueryCollection q = BuildQuery(
            ("$filter", "Name eq 'x'"),
            ("$expand", "Orders"),
            ("$orderby", "Name asc"),
            ("$top", "10"),
            ("$skip", "5"));

        var options = new ApiQueryOptions<SampleEntity>(q);
        options.Filter.Should().NotBeNull();
        options.Expand.Should().NotBeNull();
        options.OrderBy.Should().NotBeNull();
        options.Top!.Value.Should().Be(10);
        options.Skip!.Value.Should().Be(5);
        options.SkipToken.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_WithoutDollarPrefix_AlsoParsed()
    {
        IQueryCollection q = BuildQuery(("filter", "Name eq 'x'"), ("top", "3"));
        var options = new ApiQueryOptions<SampleEntity>(q);
        options.Filter.Should().NotBeNull();
        options.Top!.Value.Should().Be(3);
    }

    [TestMethod]
    public void ApiQueryOptions_DisabledFilter_FilterIsNull()
    {
        IQueryCollection q = BuildQuery(("$filter", "Name eq 'x'"));
        var settings = new ApiQueryOptionsSettings { FilterEnabled = false };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);
        options.Filter.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_DisabledTop_TopIsNull()
    {
        IQueryCollection q = BuildQuery(("$top", "10"));
        var settings = new ApiQueryOptionsSettings { TopEnabled = false };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);
        options.Top.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_DisabledSkip_SkipIsNull()
    {
        IQueryCollection q = BuildQuery(("$skip", "5"));
        var settings = new ApiQueryOptionsSettings { SkipEnabled = false };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);
        options.Skip.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_DisabledExpand_ExpandIsNull()
    {
        IQueryCollection q = BuildQuery(("$expand", "Orders"));
        var settings = new ApiQueryOptionsSettings { ExpandEnabled = false };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);
        options.Expand.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_DisabledOrderBy_OrderByIsNull()
    {
        IQueryCollection q = BuildQuery(("$orderby", "Name asc"));
        var settings = new ApiQueryOptionsSettings { OrderByEnabled = false };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);
        options.OrderBy.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_DisabledSkipToken_SkipTokenIsNull()
    {
        IQueryCollection q = BuildQuery(("$skiptoken", "abc"));
        var settings = new ApiQueryOptionsSettings { SkipTokenEnabled = false };
        var options = new ApiQueryOptions<SampleEntity>(q, settings);
        options.SkipToken.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_InvalidTop_TopIsNull()
    {
        IQueryCollection q = BuildQuery(("$top", "not-a-number"));
        var options = new ApiQueryOptions<SampleEntity>(q);
        options.Top.Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_SettingsExposed()
    {
        var settings = new ApiQueryOptionsSettings();
        var options = new ApiQueryOptions<SampleEntity>(new QueryCollection(), settings);
        options.Settings.Should().BeSameAs(settings);
    }

    [TestMethod]
    public void ApiQueryOptions_FromRequest_ExtractsQuery()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?$top=7");
        var options = ApiQueryOptions.FromRequest<SampleEntity>(httpContext.Request);
        options.Top!.Value.Should().Be(7);
    }

    [TestMethod]
    public void ApiQueryOptionsAttribute_ApplyTo_AllDefaults_ReturnsCopyOfSettings()
    {
        var settings = new ApiQueryOptionsSettings { MaxPageSize = 50, DefaultPageSize = 10, FilterEnabled = false };
        ApiQueryOptionsSettings result = new ApiQueryOptionsAttribute().ApplyTo(settings);

        result.MaxPageSize.Should().Be(50);
        result.DefaultPageSize.Should().Be(10);
        result.FilterEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void ApiQueryOptionsAttribute_ApplyTo_DefaultPageSize_Override_Applies()
    {
        var settings = new ApiQueryOptionsSettings { DefaultPageSize = 10 };
        ApiQueryOptionsSettings result = new ApiQueryOptionsAttribute { DefaultPageSize = 25 }.ApplyTo(settings);

        result.DefaultPageSize.Should().Be(25);
    }

    [TestMethod]
    public void ApiQueryOptionsAttribute_ApplyTo_FilterEnabled_Default_PreservesRegistered()
    {
        var settings = new ApiQueryOptionsSettings { FilterEnabled = false };
        ApiQueryOptionsSettings result = new ApiQueryOptionsAttribute { FilterEnabled = QueryOptionState.Default }.ApplyTo(settings);

        result.FilterEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void ApiQueryOptionsAttribute_ApplyTo_FilterEnabled_Disabled_Overrides()
    {
        var settings = new ApiQueryOptionsSettings { FilterEnabled = true };
        ApiQueryOptionsSettings result = new ApiQueryOptionsAttribute { FilterEnabled = QueryOptionState.Disabled }.ApplyTo(settings);

        result.FilterEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void ApiQueryOptionsAttribute_ApplyTo_FilterEnabled_Enabled_Overrides()
    {
        var settings = new ApiQueryOptionsSettings { FilterEnabled = false };
        ApiQueryOptionsSettings result = new ApiQueryOptionsAttribute { FilterEnabled = QueryOptionState.Enabled }.ApplyTo(settings);

        result.FilterEnabled.Should().BeTrue();
    }

    [TestMethod]
    public void ApiQueryOptionsAttribute_ApplyTo_MaxPageSize_Override_Applies()
    {
        var settings = new ApiQueryOptionsSettings { MaxPageSize = 50 };
        ApiQueryOptionsSettings result = new ApiQueryOptionsAttribute { MaxPageSize = 100 }.ApplyTo(settings);

        result.MaxPageSize.Should().Be(100);
    }

    [TestMethod]
    public void ApiQueryOptionsAttribute_ApplyTo_MaxPageSize_Zero_PreservesRegistered()
    {
        var settings = new ApiQueryOptionsSettings { MaxPageSize = 50 };
        ApiQueryOptionsSettings result = new ApiQueryOptionsAttribute { MaxPageSize = 0 }.ApplyTo(settings);

        result.MaxPageSize.Should().Be(50);
    }

    [TestMethod]
    public void ApiQueryOptionsAttribute_ApplyTo_StringComparison_AlwaysPreservedFromSettings()
    {
        var settings = new ApiQueryOptionsSettings { StringComparison = StringComparison.Ordinal };
        ApiQueryOptionsSettings result = new ApiQueryOptionsAttribute { MaxPageSize = 10 }.ApplyTo(settings);

        result.StringComparison.Should().Be(StringComparison.Ordinal);
    }

    [TestMethod]
    public void ApiQueryOptionsAttribute_ApplyTo_MultipleOverrides_AllApply()
    {
        var settings = new ApiQueryOptionsSettings();
        var attr = new ApiQueryOptionsAttribute
        {
            DefaultPageSize = 20,
            ExpandEnabled = QueryOptionState.Disabled,
            FilterEnabled = QueryOptionState.Disabled,
            MaxPageSize = 100,
            OrderByEnabled = QueryOptionState.Disabled,
        };
        ApiQueryOptionsSettings result = attr.ApplyTo(settings);

        result.DefaultPageSize.Should().Be(20);
        result.ExpandEnabled.Should().BeFalse();
        result.FilterEnabled.Should().BeFalse();
        result.MaxPageSize.Should().Be(100);
        result.OrderByEnabled.Should().BeFalse();
        result.SkipEnabled.Should().BeTrue();
        result.TopEnabled.Should().BeTrue();
    }
}

internal sealed class SampleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? Score { get; set; }
    public int Balance { get; set; }
    public SampleAddress? Address { get; set; }
}

internal sealed class SampleAddress
{
    public string City { get; set; } = string.Empty;
}