using ApiQueryOptions.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.EntityFrameworkCore.Tests;

/// <summary>
/// Uses the SQLite provider because <c>ToQueryString</c> needs a relational provider.
/// This file deliberately imports only <c>ApiQueryOptions.Extensions</c>: the core <c>Apply</c>
/// must still apply <c>$expand</c> when the EF Core package is installed.
/// </summary>
[TestClass]
public sealed class EFParameterizationTests : IDisposable
{
    private readonly BlogContext _db = new(
        new DbContextOptionsBuilder<BlogContext>().UseSqlite("Data Source=:memory:").Options);

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// <c>ToQueryString</c> lists parameter values above the SQL (as <c>.param set</c> lines for
    /// SQLite, or <c>--</c> comments for other providers); only the command text matters here.
    /// </summary>
    private static bool IsCommandLine(string line)
    {
        string trimmed = line.TrimStart();
        return !trimmed.StartsWith(".param", StringComparison.Ordinal) && !trimmed.StartsWith("--", StringComparison.Ordinal);
    }

    private static ApiQueryOptions<T> Options<T>(string? filter, string? expand = null)
    {
        var dict = new Dictionary<string, StringValues>();
        if (filter is not null)
        {
            dict["$filter"] = filter;
        }

        if (expand is not null)
        {
            dict["$expand"] = expand;
        }

        return new ApiQueryOptions<T>(new QueryCollection(dict));
    }

    [TestMethod]
    [DataRow("Id eq 42", "42")]
    [DataRow("Title eq 'Secret Title'", "Secret Title")]
    [DataRow("contains(Title, 'needle')", "needle")]
    [DataRow("startswith(Title, 'prefix')", "prefix")]
    public void Apply_FilterValue_IsSentAsSqlParameter(string filter, string value)
    {
        string sql = _db.Posts.Apply(Options<BlogPost>(filter)).ToQueryString();

        sql.Should().Contain("@");
        sql.Split('\n').Where(IsCommandLine)
            .Should().NotContain(line => line.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Apply_DifferentValues_ProduceIdenticalSql()
    {
        static string CommandText(string sql) =>
            string.Join('\n', sql.Split('\n').Where(IsCommandLine));

        string first = CommandText(_db.Posts.Apply(Options<BlogPost>("Title eq 'Alpha' and AuthorId gt 1")).ToQueryString());
        string second = CommandText(_db.Posts.Apply(Options<BlogPost>("Title eq 'Beta' and AuthorId gt 2")).ToQueryString());

        first.Should().Be(second);
    }

    [TestMethod]
    public void Apply_NullValue_StaysALiteral()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["$filter"] = "Title eq null" });

        string sql = _db.Posts.Apply(new ApiQueryOptions<BlogPost>(query)).ToQueryString();

        sql.Should().NotContain("@");
    }

    [TestMethod]
    public void Apply_WithoutEfNamespace_IncludesExpand()
    {
        string sql = _db.Posts.Apply(Options<BlogPost>("AuthorId eq 7", expand: "Author")).ToQueryString();

        sql.Should().Contain("JOIN").And.Contain("@");
    }

    [TestMethod]
    public void Apply_NestedExpand_IncludesEveryLevel()
    {
        string sql = _db.Authors.Apply(Options<Author>(filter: null, expand: "Posts.Comments")).ToQueryString();

        sql.Should().Contain("\"Posts\"").And.Contain("\"Comments\"");
    }

    [TestMethod]
    public void Apply_NoExpand_AddsNoJoin()
    {
        string sql = _db.Posts.Apply(Options<BlogPost>("Id eq 1")).ToQueryString();

        sql.Should().NotContain("JOIN");
    }

    [TestMethod]
    public void Apply_ExpandOnNonEfQuery_IsIgnored()
    {
        BlogPost[] posts = [new() { Id = 1, Title = "A" }, new() { Id = 2, Title = "B" }];

        List<BlogPost> result = posts.AsQueryable().Apply(Options<BlogPost>("Id gt 0", expand: "Author")).ToList();

        result.Select(p => p.Id).Should().Equal(1, 2);
    }

    [TestMethod]
    public void Apply_ExpandOnValueTypeQuery_IsIgnored()
    {
        IQueryable<int> numbers = new[] { 3, 1, 2 }.AsQueryable();

        List<int> result = numbers.Apply(Options<int>(filter: null, expand: "Anything")).ToList();

        result.Should().Equal(3, 1, 2);
    }
}
