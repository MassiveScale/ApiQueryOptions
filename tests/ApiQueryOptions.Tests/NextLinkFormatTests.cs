using Microsoft.AspNetCore.Http;

namespace ApiQueryOptions.Tests;

/// <summary>
/// Pins the exact next-link text. The expected values match what the previous URL builder
/// produced, except where it emitted a broken link (names containing <c>&amp;</c>, <c>=</c>,
/// <c>+</c>, <c>#</c>, or <c>%</c> followed by hex digits).
/// </summary>
[TestClass]
public class NextLinkFormatTests
{
    private const string Token = "eyJ0b3AiOiIyIiwic2tpcCI6IjIifQ";

    private static string? NextLink(string pathBase, string path, string queryString, bool skipTokens = true)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("api.example.com:8443");
        ctx.Request.PathBase = new PathString(pathBase);
        ctx.Request.Path = path.Length == 0 ? PathString.Empty : new PathString(path);
        ctx.Request.QueryString = new QueryString(queryString);

        var settings = new ApiQueryOptionsSettings { SkipTokenEnabled = skipTokens };
        return ApiQueryOptions<object>.FromRequest(ctx.Request, settings).NextLink(resultCount: 2);
    }

    [TestMethod]
    [DataRow("", "/pets", "?$top=2&tenant=a", "https://api.example.com:8443/pets?tenant=a&$skiptoken=" + Token)]
    [DataRow("/api", "/pets", "?$top=2&tenant=a", "https://api.example.com:8443/api/pets?tenant=a&$skiptoken=" + Token)]
    [DataRow("/api/v1", "/pets/list", "?$top=2", "https://api.example.com:8443/api/v1/pets/list?$skiptoken=" + Token)]
    [DataRow("/base/", "/pets", "?$top=2", "https://api.example.com:8443/base/pets?$skiptoken=" + Token)]
    [DataRow("", "", "?$top=2", "https://api.example.com:8443/?$skiptoken=" + Token)]
    [DataRow("", "/", "?$top=2", "https://api.example.com:8443/?$skiptoken=" + Token)]
    [DataRow("", "/pets/", "?$top=2", "https://api.example.com:8443/pets/?$skiptoken=" + Token)]
    [DataRow("", "/a b/café", "?$top=2", "https://api.example.com:8443/a%20b/caf%C3%A9?$skiptoken=" + Token)]
    [DataRow("", "/a%2Fb", "?$top=2", "https://api.example.com:8443/a%2Fb?$skiptoken=" + Token)]
    [DataRow("", "/p(1)/x;y,z@w$!*~'", "?$top=2", "https://api.example.com:8443/p(1)/x;y,z@w$!*~'?$skiptoken=" + Token)]
    public void NextLink_Path_MatchesPreviousOutput(string pathBase, string path, string query, string expected)
    {
        NextLink(pathBase, path, query).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("?$top=2&tag=a&tag=b&x=1", "?tag=a&tag=b&x=1&")]
    [DataRow("?tag=a&$top=2&tag=b", "?tag=a&tag=b&")]
    [DataRow("?$top=2&q=hello%20world&p=a%2Bb&e=&n", "?q=hello%20world&p=a%2Bb&e=&n=&")]
    [DataRow("?$top=2&k%20y=v&amp=%26&eq=%3D&hash=%23&u=%C3%A9", "?k%20y=v&amp=%26&eq=%3D&hash=%23&u=%C3%A9&")]
    [DataRow("?$top=2&x=%27q%27&s=a/b?c", "?x=%27q%27&s=a%2Fb%3Fc&")]
    [DataRow("?$top=2&v=%24!*()%27~;:@,%5B%5D%7B%7D%7C%5C%5E%60%22%3C%3E", "?v=%24%21%2A%28%29%27~%3B%3A%40%2C%5B%5D%7B%7D%7C%5C%5E%60%22%3C%3E&")]
    [DataRow("?$top=2&v=%25zz&w=100%25", "?v=%25zz&w=100%25&")]
    [DataRow("?$top=2&a/b=5&a%3Fb=6&a!b*()%27~;:@,=8&%5B%5D=9&%C3%A9=10&=11", "?a/b=5&a?b=6&a!b*()'~;:@,=8&[]=9&%C3%A9=10&=11&")]
    [DataRow("?$top=2&page%5Bsize%5D=10", "?page[size]=10&")]
    public void NextLink_ForwardedQuery_MatchesPreviousOutput(string query, string expectedQueryPrefix)
    {
        NextLink("", "/pets", query).Should().Be("https://api.example.com:8443/pets" + expectedQueryPrefix + "$skiptoken=" + Token);
    }

    [TestMethod]
    [DataRow("?$top=2&a%26b=1", "?a%26b=1&")]
    [DataRow("?$top=2&a%3Db=2", "?a%3Db=2&")]
    [DataRow("?$top=2&a%23b=3", "?a%23b=3&")]
    [DataRow("?$top=2&a%2Bb=7", "?a%2Bb=7&")]
    [DataRow("?$top=2&a%25b=4", "?a%25b=4&")]
    [DataRow("?$top=2&%2541=12", "?%2541=12&")]
    [DataRow("?$top=2&%7B%7D%7C%5E%60%22=9", "?%7B%7D%7C%5E%60%22=9&")]
    public void NextLink_NameWithDelimiters_IsEncoded(string query, string expectedQueryPrefix)
    {
        NextLink("", "/pets", query).Should().Be("https://api.example.com:8443/pets" + expectedQueryPrefix + "$skiptoken=" + Token);
    }

    [TestMethod]
    public void NextLink_NameWithDelimiters_RoundTripsThroughQueryParsing()
    {
        string link = NextLink("", "/pets", "?$top=2&a%26b=1&c%3Dd=2&e%2Bf=3&g%23h=4")!;

        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> parsed =
            Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(link).Query);

        parsed.Keys.Should().BeEquivalentTo("a&b", "c=d", "e+f", "g#h", "$skiptoken");
    }

    [TestMethod]
    public void NextLink_OffsetLink_MatchesPreviousOutput()
    {
        NextLink("/api", "/pets", "?$top=2&$filter=Name%20eq%20%27x%27&tag=a&tag=b", skipTokens: false)
            .Should().Be("https://api.example.com:8443/api/pets?$filter=Name%20eq%20%27x%27&tag=a&tag=b&$top=2&$skip=2");
    }

    [TestMethod]
    public void NextLink_DefaultPort_IsOmitted()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "http";
        ctx.Request.Host = new HostString("localhost");
        ctx.Request.Path = "/pets";
        ctx.Request.QueryString = new QueryString("?$top=2");

        ApiQueryOptions<object>.FromRequest(ctx.Request).NextLink(resultCount: 2)
            .Should().Be("http://localhost/pets?$skiptoken=" + Token);
    }

    [TestMethod]
    [DataRow("$skiptoken", "$skiptoken")]
    [DataRow("page[cursor]", "page[cursor]")]
    [DataRow("a b", "a%20b")]
    [DataRow("a&b", "a%26b")]
    [DataRow("%2A", "%252A")]
    [DataRow("", "")]
    public void EncodeName_EncodesOnlyUnsafeCharacters(string name, string expected)
    {
        QueryStringEncoder.EncodeName(name).Should().Be(expected);
    }
}
