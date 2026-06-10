using ApiQueryOptions.SkipToken;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

[TestClass]
public class SkipTokenTests
{
    [TestMethod]
    public void Decode_InvalidBase64_ThrowsFormatException()
    {
        Func<ApiQueryOptions<RoundTripEntity>> act = () => SkipTokenEncoder.Decode<RoundTripEntity>("!!!not-base64!!!");
        act.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void Decode_SettingsPropagated()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues> { ["$top"] = "5" });
        var settings = new ApiQueryOptionsSettings { FilterEnabled = false };
        string token = SkipTokenEncoder.Encode(new ApiQueryOptions<RoundTripEntity>(q, settings));
        ApiQueryOptions<RoundTripEntity> decoded = SkipTokenEncoder.Decode<RoundTripEntity>(token, settings);
        decoded.Settings.FilterEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void Decode_ValidBase64ButNotJson_ThrowsFormatException()
    {
        string notJson = Convert.ToBase64String("hello world"u8.ToArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Func<ApiQueryOptions<RoundTripEntity>> act = () => SkipTokenEncoder.Decode<RoundTripEntity>(notJson);
        act.Should().Throw<FormatException>();
    }

    [TestMethod]
    public void Encode_DifferentOptions_ProduceDifferentTokens()
    {
        var q1 = new QueryCollection(new Dictionary<string, StringValues> { ["$top"] = "1" });
        var q2 = new QueryCollection(new Dictionary<string, StringValues> { ["$top"] = "2" });
        SkipTokenEncoder.Encode(new ApiQueryOptions<RoundTripEntity>(q1))
            .Should().NotBe(SkipTokenEncoder.Encode(new ApiQueryOptions<RoundTripEntity>(q2)));
    }

    [TestMethod]
    public void Encode_NoSkipOverride_UsesOptionsSkip()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
            ["$skip"] = "5",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        string token = SkipTokenEncoder.Encode(options);
        ApiQueryOptions<RoundTripEntity> decoded = SkipTokenEncoder.Decode<RoundTripEntity>(token);

        decoded.Skip!.Value.Should().Be(5);
    }

    [TestMethod]
    public void Encode_ProducesUrlSafeString()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$filter"] = "Name eq 'x'",
        });
        string token = SkipTokenEncoder.Encode(new ApiQueryOptions<RoundTripEntity>(q));
        token.Should().NotContainAny("+", "/", "=");
    }

    [TestMethod]
    public void Encode_SkipOverride_UsesOverrideValue()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
            ["$skip"] = "5",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        string token = SkipTokenEncoder.Encode(options, skipOverride: 99);
        ApiQueryOptions<RoundTripEntity> decoded = SkipTokenEncoder.Decode<RoundTripEntity>(token);

        decoded.Skip!.Value.Should().Be(99);
    }

    [TestMethod]
    public void RoundTrip_AllOptions_RestoresFaithfully()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$filter"] = "Name eq 'Alice'",
            ["$expand"] = "Orders,Tags",
            ["$orderby"] = "Name asc,Age desc",
            ["$top"] = "10",
            ["$skip"] = "5",
        });
        var original = new ApiQueryOptions<RoundTripEntity>(q);
        string token = SkipTokenEncoder.Encode(original);
        ApiQueryOptions<RoundTripEntity> decoded = SkipTokenEncoder.Decode<RoundTripEntity>(token);

        decoded.Filter!.RawValue.Should().Be("Name eq 'Alice'");
        decoded.Top!.Value.Should().Be(10);
        decoded.Skip!.Value.Should().Be(5);
        decoded.Expand!.NavigationProperties.Should().HaveCount(2);
        decoded.OrderBy!.Items.Should().HaveCount(2);
        decoded.OrderBy.Items[1].Descending.Should().BeTrue();
    }

    [TestMethod]
    public void RoundTrip_EmptyOptions_ProducesDecodable()
    {
        var original = new ApiQueryOptions<RoundTripEntity>(new QueryCollection());
        string token = SkipTokenEncoder.Encode(original);
        ApiQueryOptions<RoundTripEntity> decoded = SkipTokenEncoder.Decode<RoundTripEntity>(token);
        decoded.Filter.Should().BeNull();
        decoded.Top.Should().BeNull();
    }

    [TestMethod]
    public void RoundTrip_FilterOnly_RestoresFilter()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$filter"] = "Status eq 'Active'",
        });
        string token = SkipTokenEncoder.Encode(new ApiQueryOptions<RoundTripEntity>(q));
        ApiQueryOptions<RoundTripEntity> decoded = SkipTokenEncoder.Decode<RoundTripEntity>(token);
        decoded.Filter!.RawValue.Should().Be("Status eq 'Active'");
        decoded.Top.Should().BeNull();
    }

    [TestMethod]
    public void SkipTokenInUrl_AppliesAllEncodedOptions()
    {
        // Build a token that encodes orderby + top + skip
        var source = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$orderby"] = "Name asc",
            ["$top"] = "3",
            ["$skip"] = "0",
        });
        string token = SkipTokenEncoder.Encode(new ApiQueryOptions<RoundTripEntity>(source));

        // Pass only the token in the URL — no individual params
        var requestQuery = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$skiptoken"] = token,
        });
        var options = new ApiQueryOptions<RoundTripEntity>(requestQuery);

        options.SkipToken.Should().NotBeNull();
        options.OrderBy.Should().NotBeNull();
        options.OrderBy!.Items.Should().HaveCount(1);
        options.OrderBy.Items[0].Property.Should().Be("Name");
        options.OrderBy.Items[0].Descending.Should().BeFalse();
        options.Top.Should().NotBeNull();
        options.Top!.Value.Should().Be(3);
        options.Skip.Should().NotBeNull();
        options.Skip!.Value.Should().Be(0);
    }

    [TestMethod]
    public void SkipTokenInUrl_FilterAbsentInToken_FilterIsNull()
    {
        // Token only encodes top — filter should be null after decode
        var source = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
        });
        string token = SkipTokenEncoder.Encode(new ApiQueryOptions<RoundTripEntity>(source));

        // URL has a $filter, but token wins and it has no filter
        var requestQuery = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$skiptoken"] = token,
            ["$filter"] = "Name eq 'Alice'",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(requestQuery);

        options.Filter.Should().BeNull();
        options.Top!.Value.Should().Be(10);
    }

    [TestMethod]
    public void SkipTokenInUrl_OverridesUrlLevelParams()
    {
        // Token encodes top=3, orderby=Name asc
        var source = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$orderby"] = "Name asc",
            ["$top"] = "3",
        });
        string token = SkipTokenEncoder.Encode(new ApiQueryOptions<RoundTripEntity>(source));

        // URL also has conflicting top=99 and orderby=Age desc — token must win
        var requestQuery = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$skiptoken"] = token,
            ["$top"] = "99",
            ["$orderby"] = "Age desc",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(requestQuery);

        options.Top!.Value.Should().Be(3);
        options.OrderBy!.Items[0].Property.Should().Be("Name");
        options.OrderBy.Items[0].Descending.Should().BeFalse();
    }

    [TestMethod]
    public void SkipTokenInUrl_WithoutPrefixDollar_AlsoApplies()
    {
        var source = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "5",
            ["$filter"] = "Status eq 'Active'",
        });
        string token = SkipTokenEncoder.Encode(new ApiQueryOptions<RoundTripEntity>(source));

        // Use "skiptoken" (no $) in the URL
        var requestQuery = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["skiptoken"] = token,
        });
        var options = new ApiQueryOptions<RoundTripEntity>(requestQuery);

        options.SkipToken.Should().NotBeNull();
        options.Top!.Value.Should().Be(5);
        options.Filter!.RawValue.Should().Be("Status eq 'Active'");
    }

    [TestMethod]
    public void NextLink_AdvancesSkipByTop()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
            ["$skip"] = "20",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        string? token = options.NextLink(resultCount: 10);

        token.Should().NotBeNull();
        ApiQueryOptions<RoundTripEntity> next = SkipTokenEncoder.Decode<RoundTripEntity>(token!);
        next.Skip!.Value.Should().Be(30);
        next.Top!.Value.Should().Be(10);
    }

    [TestMethod]
    public void NextLink_NoSkipPresent_DefaultsToZeroBeforeAdvancing()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "5",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        string? token = options.NextLink(resultCount: 5);

        token.Should().NotBeNull();
        ApiQueryOptions<RoundTripEntity> next = SkipTokenEncoder.Decode<RoundTripEntity>(token!);
        next.Skip!.Value.Should().Be(5);
        next.Top!.Value.Should().Be(5);
    }

    [TestMethod]
    public void NextLink_PreservesFilterAndOrderByInToken()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$filter"] = "Name eq 'Alice'",
            ["$orderby"] = "Name asc",
            ["$top"] = "10",
            ["$skip"] = "0",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        string? token = options.NextLink(resultCount: 10);

        token.Should().NotBeNull();
        ApiQueryOptions<RoundTripEntity> next = SkipTokenEncoder.Decode<RoundTripEntity>(token!);
        next.Filter!.RawValue.Should().Be("Name eq 'Alice'");
        next.OrderBy!.Items.Should().HaveCount(1);
        next.OrderBy.Items[0].Property.Should().Be("Name");
        next.Skip!.Value.Should().Be(10);
    }

    [TestMethod]
    public void NextLink_ResultCountEqualsTop_NoTotalCount_ReturnsToken()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        string? token = options.NextLink(resultCount: 10);

        token.Should().NotBeNull();
    }

    [TestMethod]
    public void NextLink_ResultCountLessThanTop_ReturnsNull()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        string? token = options.NextLink(resultCount: 9);

        token.Should().BeNull();
    }

    [TestMethod]
    public void NextLink_ResultCountZero_ReturnsNull()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        string? token = options.NextLink(resultCount: 0);

        token.Should().BeNull();
    }

    [TestMethod]
    public void NextLink_TopIsNull_ReturnsNull()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$filter"] = "Name eq 'Alice'",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        string? token = options.NextLink(resultCount: 5);

        token.Should().BeNull();
    }

    [TestMethod]
    public void NextLink_TotalCountExhausted_ReturnsNull()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
            ["$skip"] = "5",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        // nextSkip = 5 + 10 = 15, totalCount = 15 → exactly exhausted
        string? token = options.NextLink(resultCount: 10, totalCount: 15);

        token.Should().BeNull();
    }

    [TestMethod]
    public void NextLink_TotalCountNotExhausted_ReturnsToken()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
            ["$skip"] = "5",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);

        // nextSkip = 5 + 10 = 15, totalCount = 20 → more pages remain
        string? token = options.NextLink(resultCount: 10, totalCount: 20);

        token.Should().NotBeNull();
    }

    [TestMethod]
    public void NextLink_WithRequest_ReturnsAbsoluteUrl()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
            ["$skip"] = "0",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("example.com");
        ctx.Request.Path = "/api/items";

        string? url = options.NextLink(ctx.Request, resultCount: 10);

        url.Should().NotBeNull();
        url.Should().StartWith("https://example.com/api/items?$skiptoken=");
    }

    [TestMethod]
    public void NextLink_WithRequest_TokenEncodesNextSkip()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
            ["$skip"] = "20",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("example.com");
        ctx.Request.Path = "/api/items";

        string? url = options.NextLink(ctx.Request, resultCount: 10);

        url.Should().NotBeNull();
        string token = url!.Split("$skiptoken=")[1];
        ApiQueryOptions<RoundTripEntity> next = SkipTokenEncoder.Decode<RoundTripEntity>(token);
        next.Skip!.Value.Should().Be(30);
        next.Top!.Value.Should().Be(10);
    }

    [TestMethod]
    public void NextLink_WithRequest_LastPage_ReturnsNull()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("example.com");
        ctx.Request.Path = "/api/items";

        string? url = options.NextLink(ctx.Request, resultCount: 9);

        url.Should().BeNull();
    }

    [TestMethod]
    public void NextLink_WithRequest_IncludesPortWhenNonStandard()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "5",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("example.com", 8443);
        ctx.Request.Path = "/api/items";

        string? url = options.NextLink(ctx.Request, resultCount: 5);

        url.Should().NotBeNull();
        url.Should().StartWith("https://example.com:8443/api/items?$skiptoken=");
    }

    [TestMethod]
    public void NextLink_WithRequest_TotalCountExhausted_ReturnsNull()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$top"] = "10",
            ["$skip"] = "5",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("example.com");
        ctx.Request.Path = "/api/items";

        string? url = options.NextLink(ctx.Request, resultCount: 10, totalCount: 15);

        url.Should().BeNull();
    }

    [TestMethod]
    public void NextLink_WithRequest_DropsApiQueryOptionsParams()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$filter"]  = "Name eq 'Alice'",
            ["$orderby"] = "Name asc",
            ["$top"]     = "10",
            ["$skip"]    = "0",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("example.com");
        ctx.Request.Path = "/api/items";
        ctx.Request.QueryString = new QueryString("?$filter=Name+eq+%27Alice%27&$orderby=Name+asc&$top=10&$skip=0");

        string? url = options.NextLink(ctx.Request, resultCount: 10);

        url.Should().NotBeNull();
        url.Should().NotContain("$filter");
        url.Should().NotContain("$orderby");
        url.Should().NotContain("$skip=");
        url.Should().NotContain("$top=");
        url.Should().Contain("$skiptoken=");
    }

    [TestMethod]
    public void NextLink_WithRequest_PreservesNonOwnedQueryParams()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["api-version"] = "2",
            ["$top"]        = "5",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("example.com");
        ctx.Request.Path = "/api/items";
        ctx.Request.QueryString = new QueryString("?api-version=2&$top=5");

        string? url = options.NextLink(ctx.Request, resultCount: 5);

        url.Should().NotBeNull();
        url.Should().Contain("api-version=2");
        url.Should().NotContain("$top=");
        url.Should().Contain("$skiptoken=");
    }

    [TestMethod]
    public void NextLink_WithRequest_PreservesMultipleNonOwnedParams()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["api-version"] = "2",
            ["tenant"]      = "acme",
            ["$top"]        = "10",
        });
        var options = new ApiQueryOptions<RoundTripEntity>(q);
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("example.com");
        ctx.Request.Path = "/api/items";
        ctx.Request.QueryString = new QueryString("?api-version=2&tenant=acme&$top=10");

        string? url = options.NextLink(ctx.Request, resultCount: 10);

        url.Should().NotBeNull();
        url.Should().Contain("api-version=2");
        url.Should().Contain("tenant=acme");
        url.Should().Contain("$skiptoken=");
        url.Should().NotContain("$top=");
    }

    [TestMethod]
    public void NextLink_WithRequest_ReplacesExistingSkipToken()
    {
        var q = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["$skiptoken"] = "old-token",
        });

        // Decode an existing skiptoken that has $top set
        var innerQ = new QueryCollection(new Dictionary<string, StringValues> { ["$top"] = "5" });
        var options = new ApiQueryOptions<RoundTripEntity>(new QueryCollection(
            new Dictionary<string, StringValues> { ["$top"] = "5" }));

        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("example.com");
        ctx.Request.Path = "/api/items";
        ctx.Request.QueryString = new QueryString("?$skiptoken=old-token");

        // Simulate a decoded-from-skiptoken options (top=5 already set)
        string? url = options.NextLink(ctx.Request, resultCount: 5);

        url.Should().NotBeNull();
        url.Should().NotContain("old-token");
        url.Should().Contain("$skiptoken=");
    }
}

internal sealed class RoundTripEntity
{
    public int Age { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}