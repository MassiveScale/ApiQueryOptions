using System.Text;
using ApiQueryOptions.Options;
using ApiQueryOptions.SkipToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

[TestClass]
public class PagingTests
{
    private static readonly ApiQueryOptionsSettings CustomNames = new()
    {
        ExpandParameterNames = ["include"],
        FilterParameterNames = ["q"],
        OrderByParameterNames = ["sort"],
        SkipParameterNames = ["offset"],
        SkipTokenParameterNames = ["cursor"],
        TopParameterNames = ["limit"],
    };

    private static DefaultHttpContext BuildContext(string queryString)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("api.example.com");
        ctx.Request.Path = "/pets";
        ctx.Request.QueryString = new QueryString(queryString);
        return ctx;
    }

    private static Dictionary<string, StringValues> ParseLink(string link) =>
        QueryHelpers.ParseQuery(new Uri(link).Query);

    private static QueryCollection Query(params (string Key, string Value)[] pairs) =>
        new(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Value)));

    private static string RawToken(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [TestMethod]
    public void SkipToken_CustomParameterNames_SecondPageKeepsState()
    {
        QueryCollection page1Query = Query(
            ("q", "Name eq 'Rex'"),
            ("limit", "2"),
            ("sort", "Name desc"),
            ("include", "Owner"));
        var page1 = new ApiQueryOptions<object>(page1Query, CustomNames);

        string token = page1.GetNextToken(resultCount: 2)!;
        var page2 = new ApiQueryOptions<object>(Query(("cursor", token)), CustomNames);

        page2.SkipToken!.RawValue.Should().Be(token);
        page2.Filter!.RawValue.Should().Be("Name eq 'Rex'");
        page2.Top!.Value.Should().Be(2);
        page2.Skip!.Value.Should().Be(2);
        page2.OrderBy!.Items.Should().ContainSingle().Which.Descending.Should().BeTrue();
        page2.Expand!.NavigationProperties.Should().Equal("Owner");
    }

    [TestMethod]
    public void SkipToken_CustomParameterNames_NextLinkFromSecondPageAdvances()
    {
        DefaultHttpContext page1Ctx = BuildContext("?q=Id%20gt%200&limit=5&tenant=a");
        var page1 = ApiQueryOptions<object>.FromRequest(page1Ctx.Request, CustomNames);

        string link1 = page1.NextLink(resultCount: 5)!;
        Dictionary<string, StringValues> query1 = ParseLink(link1);
        query1.Keys.Should().BeEquivalentTo("tenant", "cursor");

        DefaultHttpContext page2Ctx = BuildContext(new Uri(link1).Query);
        var page2 = ApiQueryOptions<object>.FromRequest(page2Ctx.Request, CustomNames);
        page2.Filter!.RawValue.Should().Be("Id gt 0");
        page2.Skip!.Value.Should().Be(5);

        string token2 = ParseLink(page2.NextLink(resultCount: 5)!)["cursor"].ToString();
        SkipTokenEncoder.Decode<object>(token2, CustomNames).Skip!.Value.Should().Be(10);
    }

    [TestMethod]
    public void Decode_IgnoresUnknownAndNestedTokenKeys()
    {
        string token = RawToken("""{"top":"5","$skiptoken":"abc","skiptoken":"def","bogus":"x"}""");

        ApiQueryOptions<object> decoded = SkipTokenEncoder.Decode<object>(token);

        decoded.Top!.Value.Should().Be(5);
        decoded.SkipToken.Should().BeNull();
        decoded.Filter.Should().BeNull();
    }

    [TestMethod]
    public void Decode_KeysAreCaseInsensitive_LastDuplicateWins()
    {
        string token = RawToken("""{"top":"1","TOP":"7","Filter":"Id eq 1"}""");

        ApiQueryOptions<object> decoded = SkipTokenEncoder.Decode<object>(token);

        decoded.Top!.Value.Should().Be(7);
        decoded.Filter!.RawValue.Should().Be("Id eq 1");
    }

    [TestMethod]
    public void Decode_NullValue_IsIgnored()
    {
        string token = RawToken("""{"top":null,"skip":"3"}""");

        ApiQueryOptions<object> decoded = SkipTokenEncoder.Decode<object>(token);

        decoded.Top.Should().BeNull();
        decoded.Skip!.Value.Should().Be(3);
    }

    [TestMethod]
    public void Decode_EmptyParameterNameList_ThrowsArgumentException()
    {
        string token = RawToken("""{"top":"5"}""");
        var settings = new ApiQueryOptionsSettings { TopParameterNames = [] };

        Action act = () => SkipTokenEncoder.Decode<object>(token, settings);

        act.Should().Throw<ArgumentException>().WithMessage("TopParameterNames*");
    }

    [TestMethod]
    public void Decode_NullParameterNameList_ThrowsArgumentException()
    {
        string token = RawToken("""{"top":"5"}""");
        var settings = new ApiQueryOptionsSettings { TopParameterNames = null! };

        Action act = () => SkipTokenEncoder.Decode<object>(token, settings);

        act.Should().Throw<ArgumentException>().WithMessage("TopParameterNames*");
    }

    [TestMethod]
    public void Decode_NegativeSkipInToken_IsIgnored()
    {
        string token = RawToken("""{"top":"5","skip":"-10"}""");

        SkipTokenEncoder.Decode<object>(token).Skip.Should().BeNull();
    }

    [TestMethod]
    public void NextLink_SkipTokenDisabled_EmitsOffsetLink()
    {
        var settings = new ApiQueryOptionsSettings { SkipTokenEnabled = false };
        DefaultHttpContext ctx = BuildContext("?$filter=Id%20gt%200&$orderby=Name&$top=10&$skip=20&tenant=a&$skiptoken=stale");
        var options = ApiQueryOptions<object>.FromRequest(ctx.Request, settings);

        string link = options.NextLink(resultCount: 10)!;

        link.Should().StartWith("https://api.example.com/pets?");
        Dictionary<string, StringValues> query = ParseLink(link);
        query["$top"].ToString().Should().Be("10");
        query["$skip"].ToString().Should().Be("30");
        query["$filter"].ToString().Should().Be("Id gt 0");
        query["$orderby"].ToString().Should().Be("Name");
        query["tenant"].ToString().Should().Be("a");
        query.Should().NotContainKey("$skiptoken");
    }

    [TestMethod]
    public void NextLink_SkipTokenDisabled_FollowingLinkAdvancesPage()
    {
        var settings = new ApiQueryOptionsSettings { SkipTokenEnabled = false, DefaultPageSize = 3 };
        var page1 = ApiQueryOptions<object>.FromRequest(BuildContext("?top=3").Request, settings);

        string link1 = page1.NextLink(resultCount: 3)!;
        var page2 = ApiQueryOptions<object>.FromRequest(BuildContext(new Uri(link1).Query).Request, settings);
        string link2 = page2.NextLink(resultCount: 3)!;

        page2.Skip!.Value.Should().Be(3);
        ParseLink(link1).Should().NotContainKey("top");
        ParseLink(link2)["$skip"].ToString().Should().Be("6");
    }

    [TestMethod]
    public void NextLink_SkipTokenDisabled_UsesFirstConfiguredNames()
    {
        var settings = new ApiQueryOptionsSettings
        {
            SkipTokenEnabled = false,
            SkipParameterNames = ["offset", "$skip"],
            TopParameterNames = ["limit", "$top"],
        };
        DefaultHttpContext ctx = BuildContext("?$top=4&$skip=4");
        var options = ApiQueryOptions<object>.FromRequest(ctx.Request, settings);

        Dictionary<string, StringValues> query = ParseLink(options.NextLink(ctx.Request, resultCount: 4)!);

        query.Keys.Should().BeEquivalentTo("limit", "offset");
        query["limit"].ToString().Should().Be("4");
        query["offset"].ToString().Should().Be("8");
    }

    [TestMethod]
    public void NextLink_SkipTokenAndSkipDisabled_ReturnsNull()
    {
        var settings = new ApiQueryOptionsSettings { SkipTokenEnabled = false, SkipEnabled = false };
        DefaultHttpContext ctx = BuildContext("?$top=10");
        var options = ApiQueryOptions<object>.FromRequest(ctx.Request, settings);

        options.NextLink(resultCount: 10).Should().BeNull();
        options.NextLink(ctx.Request, resultCount: 10).Should().BeNull();
    }

    [TestMethod]
    public void NextLink_SkipTokenDisabled_LastPage_ReturnsNull()
    {
        var settings = new ApiQueryOptionsSettings { SkipTokenEnabled = false };
        var options = ApiQueryOptions<object>.FromRequest(BuildContext("?$top=10").Request, settings);

        options.NextLink(resultCount: 9).Should().BeNull();
    }

    [TestMethod]
    public void NextLink_SkipTokenDisabled_NoRequest_ReturnsNull()
    {
        var settings = new ApiQueryOptionsSettings { SkipTokenEnabled = false };
        var options = new ApiQueryOptions<object>(Query(("$top", "10")), settings);

        options.NextLink(resultCount: 10).Should().BeNull();
    }

    [TestMethod]
    public void GetNextToken_SkipTokenDisabled_ReturnsNull()
    {
        var settings = new ApiQueryOptionsSettings { SkipTokenEnabled = false };
        var options = new ApiQueryOptions<object>(Query(("$top", "10")), settings);

        options.GetNextToken(resultCount: 10).Should().BeNull();
    }

    [TestMethod]
    public void NextLink_NullRequest_ThrowsArgumentNull()
    {
        var options = new ApiQueryOptions<object>(Query(("$top", "10")));

        Action act = () => options.NextLink(null!, resultCount: 10);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void NextLink_SkipOverflow_ReturnsNull()
    {
        var options = new ApiQueryOptions<object>(Query(("$top", "10"), ("$skip", int.MaxValue.ToString())));

        options.GetNextToken(resultCount: 10).Should().BeNull();
    }

    [TestMethod]
    public void NextLink_SkipReachesIntMax_StillReturnsToken()
    {
        var options = new ApiQueryOptions<object>(Query(("$top", "10"), ("$skip", (int.MaxValue - 10).ToString())));

        string token = options.GetNextToken(resultCount: 10)!;

        SkipTokenEncoder.Decode<object>(token).Skip!.Value.Should().Be(int.MaxValue);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("-2147483648")]
    public void TopTryParse_NotPositive_ReturnsNull(string raw)
    {
        TopQueryOption.TryParse(raw).Should().BeNull();
    }

    [TestMethod]
    public void TopTryParse_One_ReturnsOption()
    {
        TopQueryOption.TryParse("1")!.Value.Should().Be(1);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void TopCtor_NotPositive_Throws(int value)
    {
        Action act = () => _ = new TopQueryOption(value);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("-2147483648")]
    public void SkipTryParse_Negative_ReturnsNull(string raw)
    {
        SkipQueryOption.TryParse(raw).Should().BeNull();
    }

    [TestMethod]
    public void SkipTryParse_Zero_ReturnsOption()
    {
        SkipQueryOption.TryParse("0")!.Value.Should().Be(0);
    }

    [TestMethod]
    public void SkipCtor_Negative_Throws()
    {
        Action act = () => _ = new SkipQueryOption(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void ApiQueryOptions_TopZero_FallsBackToDefaultPageSize()
    {
        var settings = new ApiQueryOptionsSettings { DefaultPageSize = 25 };

        var options = new ApiQueryOptions<object>(Query(("$top", "0")), settings);

        options.Top!.Value.Should().Be(25);
    }

    [TestMethod]
    public void ApiQueryOptions_TopZero_NoDefault_NoNextLink()
    {
        var options = new ApiQueryOptions<object>(Query(("$top", "0")));

        options.Top.Should().BeNull();
        options.GetNextToken(resultCount: 0).Should().BeNull();
    }

    [TestMethod]
    public void ApiQueryOptions_NegativeSkip_IsIgnored()
    {
        var options = new ApiQueryOptions<object>(Query(("$skip", "-5")));

        options.Skip.Should().BeNull();
    }
}
