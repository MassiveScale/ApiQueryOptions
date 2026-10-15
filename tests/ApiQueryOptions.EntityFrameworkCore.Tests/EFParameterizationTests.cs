using ApiQueryOptions.EntityFrameworkCore.Extensions;
using ApiQueryOptions.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.EntityFrameworkCore.Tests;

/// <summary>
/// Uses the SQLite provider because <c>ToQueryString</c> needs a relational provider.
/// This file imports both <c>ApiQueryOptions.Extensions</c> and
/// <c>ApiQueryOptions.EntityFrameworkCore.Extensions</c>; it only compiles while the
/// core <c>Apply</c> and the EF Core <c>ApplyWithExpand</c> have distinct names.
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

    private static ApiQueryOptions<BlogPost> Options(string filter, string? expand = null)
    {
        var dict = new Dictionary<string, StringValues> { ["$filter"] = filter };
        if (expand is not null)
        {
            dict["$expand"] = expand;
        }

        return new ApiQueryOptions<BlogPost>(new QueryCollection(dict));
    }

    [TestMethod]
    [DataRow("Id eq 42", "42")]
    [DataRow("Title eq 'Secret Title'", "Secret Title")]
    [DataRow("contains(Title, 'needle')", "needle")]
    [DataRow("startswith(Title, 'prefix')", "prefix")]
    public void ApplyWithExpand_FilterValue_IsSentAsSqlParameter(string filter, string value)
    {
        string sql = _db.Posts.ApplyWithExpand(Options(filter)).ToQueryString();

        sql.Should().Contain("@");
        sql.Split('\n').Where(IsCommandLine)
            .Should().NotContain(line => line.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ApplyWithExpand_DifferentValues_ProduceIdenticalSql()
    {
        static string CommandText(string sql) =>
            string.Join('\n', sql.Split('\n').Where(IsCommandLine));

        string first = CommandText(_db.Posts.ApplyWithExpand(Options("Title eq 'Alpha' and AuthorId gt 1")).ToQueryString());
        string second = CommandText(_db.Posts.ApplyWithExpand(Options("Title eq 'Beta' and AuthorId gt 2")).ToQueryString());

        first.Should().Be(second);
    }

    [TestMethod]
    public void ApplyWithExpand_NullValue_StaysALiteral()
    {
        // Ordinal comparison: with OrdinalIgnoreCase, 'eq null' on a string calls ToUpper() on null,
        // which is a separate, pre-existing null-handling issue.
        var settings = new ApiQueryOptionsSettings { StringComparison = StringComparison.Ordinal };
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["$filter"] = "Title eq null" });

        string sql = _db.Posts.ApplyWithExpand(new ApiQueryOptions<BlogPost>(query, settings)).ToQueryString();

        sql.Should().NotContain("@");
    }

    [TestMethod]
    public void ApplyWithExpand_WithExpand_KeepsParametersAndJoins()
    {
        string sql = _db.Posts.ApplyWithExpand(Options("AuthorId eq 7", expand: "Author")).ToQueryString();

        sql.Should().Contain("JOIN").And.Contain("@");
    }

    [TestMethod]
    public void CoreApply_AndApplyWithExpand_CoexistInOneFile()
    {
        ApiQueryOptions<BlogPost> options = Options("Id eq 1", expand: "Author");

        string core = _db.Posts.Apply(options).ToQueryString();
        string ef = _db.Posts.ApplyWithExpand(options).ToQueryString();

        core.Should().NotContain("JOIN");
        ef.Should().Contain("JOIN");
    }

    [TestMethod]
    public void ApplyWithExpand_NullOptions_ThrowsArgumentNull()
    {
        Action act = () => _db.Posts.ApplyWithExpand(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
