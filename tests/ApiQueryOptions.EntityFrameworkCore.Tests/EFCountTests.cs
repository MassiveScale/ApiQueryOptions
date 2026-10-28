using ApiQueryOptions.EntityFrameworkCore.Extensions;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.EntityFrameworkCore.Tests;

/// <summary>
/// Runs <c>CountAsync(options)</c> against a real SQLite database, so the filter must translate to
/// SQL. The connection stays open for the life of the test so the in-memory database keeps its data.
/// </summary>
[TestClass]
public sealed class EFCountTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly BlogContext _db;

    public EFCountTests()
    {
        _connection.Open();
        _db = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Authors.AddRange(new Author { Id = 1, Name = "AuthorA" }, new Author { Id = 2, Name = "AuthorB" });
        _db.Posts.AddRange(
            new BlogPost { Id = 1, Title = "Alpha Post", AuthorId = 1 },
            new BlogPost { Id = 2, Title = "Beta Post", AuthorId = 1 },
            new BlogPost { Id = 3, Title = "Gamma Draft", AuthorId = 2 });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static ApiQueryOptions<BlogPost> Options(params (string key, string value)[] pairs) =>
        new(new QueryCollection(pairs.ToDictionary(p => p.key, p => new StringValues(p.value))));

    [TestMethod]
    public async Task CountAsync_WithFilterPagingAndExpand_CountsAllFilteredItems()
    {
        ApiQueryOptions<BlogPost> options = Options(
            ("$filter", "endswith(Title, 'post')"),
            ("$orderby", "Title desc"),
            ("$skip", "1"),
            ("$top", "1"),
            ("$expand", "Comments"));

        int total = await _db.Posts.CountAsync(options);

        total.Should().Be(2);
    }

    [TestMethod]
    public async Task CountAsync_NoFilter_CountsAllItems()
    {
        int total = await _db.Posts.CountAsync(Options(("$top", "1")));

        total.Should().Be(3);
    }

    [TestMethod]
    public async Task CountAsync_SecondPageFromSkipToken_CountsAllFilteredItems()
    {
        ApiQueryOptions<BlogPost> page1 = Options(("$filter", "AuthorId eq 1"), ("$top", "1"));
        string token = page1.GetNextToken(resultCount: 1)!;

        int total = await _db.Posts.CountAsync(Options(("$skiptoken", token)));

        total.Should().Be(2);
    }

    [TestMethod]
    public async Task CountAsync_UnknownFilterProperty_ThrowsInvalidQueryOption()
    {
        Func<Task<int>> act = () => _db.Posts.CountAsync(Options(("$filter", "Missing eq 1")));

        (await act.Should().ThrowAsync<InvalidQueryOptionException>())
            .Which.Property.Should().Be("Missing");
    }

    [TestMethod]
    public async Task CountAsync_NullOptions_ThrowsArgumentNull()
    {
        Func<Task<int>> act = () => _db.Posts.CountAsync((ApiQueryOptions<BlogPost>)null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("options");
    }

    [TestMethod]
    public async Task CountAsync_CanceledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task<int>> act = () => _db.Posts.CountAsync(Options(("$filter", "AuthorId eq 1")), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public void ApplyFilter_Options_SqlHasNoOrderByLimitOrJoin()
    {
        ApiQueryOptions<BlogPost> options = Options(
            ("$filter", "AuthorId eq 1"),
            ("$orderby", "Title desc"),
            ("$skip", "1"),
            ("$top", "1"),
            ("$expand", "Comments"));

        string sql = _db.Posts.ApplyFilter(options).ToQueryString();

        sql.Should().Contain("WHERE")
            .And.NotContain("ORDER BY")
            .And.NotContain("LIMIT")
            .And.NotContain("JOIN");
    }
}
