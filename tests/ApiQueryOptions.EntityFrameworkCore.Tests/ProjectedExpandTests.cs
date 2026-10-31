using System.Linq.Expressions;
using ApiQueryOptions.EntityFrameworkCore.Extensions;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using ApiQueryOptions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.EntityFrameworkCore.Tests;

public sealed class PostDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<string>? Comments { get; set; }
}

[TestClass]
public sealed class ProjectedExpandTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly BlogContext _sqlite;
    private readonly BlogContext _inMemory;

    public ProjectedExpandTests()
    {
        _connection.Open();
        _sqlite = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseSqlite(_connection).Options);
        _sqlite.Database.EnsureCreated();
        Seed(_sqlite);

        _inMemory = new BlogContext(new DbContextOptionsBuilder<BlogContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Seed(_inMemory);
    }

    public void Dispose()
    {
        _sqlite.Dispose();
        _inMemory.Dispose();
        _connection.Dispose();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Apply_ProjectedQueryWithExpand_AppliesOtherOptionsWithoutThrowing(bool sqlite)
    {
        BlogContext db = sqlite ? _sqlite : _inMemory;
        ApiQueryOptions<PostDto> options = Options(
            ("$filter", "Title ne 'Gamma Post'"), ("$orderby", "Title desc"), ("$top", "1"), ("$expand", "comments"));

        List<PostDto> result = Project(db, options).Apply(options).ToList();

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Beta Post");
        result[0].Comments.Should().BeEquivalentTo(["Comment 2", "Comment 3"]);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Apply_ProjectedQueryWithoutRequestedExpand_LeavesCollectionUnloaded(bool sqlite)
    {
        BlogContext db = sqlite ? _sqlite : _inMemory;
        ApiQueryOptions<PostDto> options = Options(("$expand", "Title"));

        List<PostDto> result = Project(db, options).Apply(options).ToList();

        result.Should().HaveCount(3).And.AllSatisfy(p => p.Comments.Should().BeNull());
    }

    [TestMethod]
    public void Apply_ProjectedQueryWithoutRequestedExpand_DoesNotQueryCollection()
    {
        ApiQueryOptions<PostDto> expanded = Options(("$expand", "Comments"));
        ApiQueryOptions<PostDto> plain = Options(("$top", "2"));

        Project(_sqlite, expanded).Apply(expanded).ToQueryString().Should().Contain("\"Comments\" AS");
        Project(_sqlite, plain).Apply(plain).ToQueryString().Should().NotContain("\"Comments\" AS");
    }

    [TestMethod]
    public void Apply_ProjectedQueryUnknownProperty_Throws()
    {
        ApiQueryOptions<PostDto> options = Options(("$expand", "Author"));

        Action act = () => Project(_sqlite, options).Apply(options);

        InvalidQueryOptionException ex = act.Should().Throw<InvalidQueryOptionException>().Which;
        ex.OptionName.Should().Be("expand");
        ex.Message.Should().Be("Property 'Author' not found on type 'PostDto'.");
    }

    [TestMethod]
    public void Apply_ProjectedQueryNextPage_CarriesExpandInSkipToken()
    {
        ApiQueryOptions<PostDto> page1 = Options(("$orderby", "Id"), ("$top", "1"), ("$expand", "Comments"));
        Project(_sqlite, page1).Apply(page1).ToList().Should().ContainSingle();

        ApiQueryOptions<PostDto> page2 = Options(("$skiptoken", page1.GetNextToken(resultCount: 1)!));
        List<PostDto> result = Project(_sqlite, page2).Apply(page2).ToList();

        page2.Expands("Comments").Should().BeTrue();
        result.Should().ContainSingle();
        result[0].Id.Should().Be(2);
        result[0].Comments.Should().HaveCount(2);
    }

    [TestMethod]
    public void Apply_EntityQueryWithExpand_IncludesNavigation()
    {
        ApiQueryOptions<BlogPost> options = Options<BlogPost>(("$expand", "author,comments"));

        string sql = _sqlite.Posts.Apply(options).ToQueryString();

        sql.Should().Contain("\"Authors\"").And.Contain("\"Comments\"");
    }

    [TestMethod]
    public void Apply_EntityQueryWithOtherOperators_IncludesNavigation()
    {
        ApiQueryOptions<BlogPost> options = Options<BlogPost>(("$filter", "Id lt 3"), ("$orderby", "Id"), ("$top", "1"), ("$expand", "Author"));

        List<BlogPost> result = _inMemory.Posts.AsNoTracking().Where(p => p.Id > 0).Apply(options).ToList();

        result.Should().ContainSingle().Which.Author!.Name.Should().Be("AuthorA");
    }

    [TestMethod]
    public void Apply_NavigationProjectionWithExpand_SkipsIncludeWithoutThrowing()
    {
        ApiQueryOptions<BlogPost> options = Options<BlogPost>(("$expand", "Author"));

        List<BlogPost> result = _sqlite.Comments.AsNoTracking().Select(c => c.Post!).Apply(options).ToList();

        result.Should().HaveCount(3).And.AllSatisfy(p => p.Author.Should().BeNull());
    }

    [TestMethod]
    [DataRow("Missing", "Property 'Missing' not found on type 'BlogPost'.")]
    [DataRow("title", "Property 'Title' on type 'BlogPost' is not a navigation property.")]
    [DataRow("Comments.Body", "Property 'Body' on type 'Comment' is not a navigation property.")]
    [DataRow("Author.", "Property '' not found on type 'Author'.")]
    public void Apply_EntityQueryInvalidPath_Throws(string path, string message)
    {
        ApiQueryOptions<BlogPost> options = Options<BlogPost>(("$expand", path));

        Action act = () => _sqlite.Posts.Apply(options);

        InvalidQueryOptionException ex = act.Should().Throw<InvalidQueryOptionException>().Which;
        ex.OptionName.Should().Be("expand");
        ex.Property.Should().Be(path);
        ex.Message.Should().Be(message);
    }

    [TestMethod]
    public void Apply_NonEfQueryWithExpand_ValidatesWithoutIncluding()
    {
        ApiQueryOptions<BlogPost> valid = Options<BlogPost>(("$expand", "Author"));
        ApiQueryOptions<BlogPost> invalid = Options<BlogPost>(("$expand", "Missing"));
        IQueryable<BlogPost> posts = new List<BlogPost> { new() { Id = 1 } }.AsQueryable();

        posts.Apply(valid).ToList().Should().ContainSingle();
        ((Action)(() => posts.Apply(invalid))).Should().Throw<InvalidQueryOptionException>();
    }

    [TestMethod]
    public void FindEntityType_EntityQuery_ReturnsEntityType()
    {
        EFQueryableExtensions.FindEntityType(_sqlite.Posts.Where(p => p.Id > 0))!.ClrType.Should().Be(typeof(BlogPost));
    }

    [TestMethod]
    public void FindEntityType_ProjectedQuery_ReturnsNull()
    {
        EFQueryableExtensions.FindEntityType(_sqlite.Posts.Select(p => new PostDto { Id = p.Id })).Should().BeNull();
    }

    [TestMethod]
    public void FindEntityType_SameTypeProjection_ReturnsNull()
    {
        EFQueryableExtensions.FindEntityType(_sqlite.Posts.Select(p => new BlogPost { Id = p.Id })).Should().BeNull();
    }

    [TestMethod]
    public void FindEntityType_ManyProjection_ReturnsNull()
    {
        EFQueryableExtensions.FindEntityType(_sqlite.Posts.SelectMany(p => p.Comments)).Should().BeNull();
    }

    [TestMethod]
    public void FindEntityType_ProjectionUnderSameTypeOperators_ReturnsNull()
    {
        IQueryable<BlogPost> query = _sqlite.Comments.Select(c => c.Post!).Where(p => p.Id > 0).OrderBy(p => p.Id);

        EFQueryableExtensions.FindEntityType(query).Should().BeNull();
    }

    [TestMethod]
    public void FindEntityType_FromSqlQuery_ReturnsEntityType()
    {
        IQueryable<BlogPost> query = _sqlite.Posts.FromSqlRaw("SELECT * FROM \"Posts\"");

        EFQueryableExtensions.FindEntityType(query)!.ClrType.Should().Be(typeof(BlogPost));
    }

    [TestMethod]
    public void FindEntityType_NonEfQuery_ReturnsNull()
    {
        EFQueryableExtensions.FindEntityType(new List<BlogPost>().AsQueryable()).Should().BeNull();
    }

    [TestMethod]
    public void FindEntityType_SourceIsNotAQuery_ReturnsNull()
    {
        IQueryable<BlogPost> fromList = ((IQueryable)_sqlite.Posts).Provider.CreateQuery<BlogPost>(
            Expression.Call(typeof(Queryable), nameof(Queryable.AsQueryable), [typeof(BlogPost)], Expression.Constant(new List<BlogPost>())));
        IQueryable<BlogPost> fromString = ((IQueryable)_sqlite.Posts).Provider.CreateQuery<BlogPost>(
            Expression.Call(typeof(ProjectedExpandTests), nameof(PostsNamed), null, Expression.Constant("Alpha")));

        EFQueryableExtensions.FindEntityType(fromList).Should().BeNull();
        EFQueryableExtensions.FindEntityType(fromString).Should().BeNull();
    }

    private static IQueryable<BlogPost> PostsNamed(string title) =>
        new List<BlogPost> { new() { Title = title } }.AsQueryable();

    private static IQueryable<PostDto> Project(BlogContext db, ApiQueryOptions<PostDto> options) =>
        db.Posts.Select(p => new PostDto
        {
            Id = p.Id,
            Title = p.Title,
            Comments = options.Expands(nameof(PostDto.Comments))
                ? p.Comments.OrderBy(c => c.Id).Select(c => c.Body).ToList()
                : null,
        });

    private static ApiQueryOptions<PostDto> Options(params (string Key, string Value)[] pairs) => Options<PostDto>(pairs);

    private static ApiQueryOptions<T> Options<T>(params (string Key, string Value)[] pairs) =>
        new(new QueryCollection(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Value))));

    private static void Seed(BlogContext db)
    {
        db.Authors.AddRange(new Author { Id = 1, Name = "AuthorA" }, new Author { Id = 2, Name = "AuthorB" });
        db.Posts.AddRange(
            new BlogPost { Id = 1, Title = "Alpha Post", AuthorId = 1, Comments = [new Comment { Id = 1, Body = "Comment 1" }] },
            new BlogPost
            {
                Id = 2,
                Title = "Beta Post",
                AuthorId = 1,
                Comments = [new Comment { Id = 2, Body = "Comment 2" }, new Comment { Id = 3, Body = "Comment 3" }],
            },
            new BlogPost { Id = 3, Title = "Gamma Post", AuthorId = 2 });
        db.SaveChanges();
    }
}

public abstract class Animal
{
    public int Id { get; set; }
}

public sealed class Dog : Animal
{
    public List<Toy> Toys { get; set; } = [];
}

public sealed class Toy
{
    public int Id { get; set; }
    public List<Dog> Dogs { get; set; } = [];
}

public sealed class KennelContext(DbContextOptions<KennelContext> options) : DbContext(options)
{
    public DbSet<Animal> Animals { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Dog>().HasMany(d => d.Toys).WithMany(t => t.Dogs);
    }
}

[TestClass]
public sealed class InheritedNavigationExpandTests : IDisposable
{
    private readonly KennelContext _db = new(
        new DbContextOptionsBuilder<KennelContext>().UseSqlite("Data Source=:memory:").Options);

    public void Dispose() => _db.Dispose();

    [TestMethod]
    [DataRow("toys")]
    [DataRow("Toys.dogs")]
    public void ApplyExpand_SkipNavigationOnDerivedType_IncludesWithDeclaredCasing(string path)
    {
        string sql = _db.Animals.ApplyExpand(new ExpandQueryOption(path), new ApiQueryOptionsSettings()).ToQueryString();

        sql.Should().Contain("\"Toy\"");
    }

    [TestMethod]
    public void FindEntityType_TypeChangingOperator_ReturnsNull()
    {
        EFQueryableExtensions.FindEntityType(_db.Animals.OfType<Dog>()).Should().BeNull();
    }
}
