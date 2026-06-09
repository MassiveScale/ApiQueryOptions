using ApiQueryOptions;
using ApiQueryOptions.EntityFrameworkCore.Extensions;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.EntityFrameworkCore.Tests;

public sealed class BlogPost
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<Comment> Comments { get; set; } = [];
    public Author? Author { get; set; }
    public int AuthorId { get; set; }
}

public sealed class Comment
{
    public int Id { get; set; }
    public string Body { get; set; } = string.Empty;
    public int BlogPostId { get; set; }
    public BlogPost? Post { get; set; }
}

public sealed class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<BlogPost> Posts { get; set; } = [];
}

public sealed class BlogContext(DbContextOptions<BlogContext> options) : DbContext(options)
{
    public DbSet<BlogPost> Posts { get; set; } = null!;
    public DbSet<Comment> Comments { get; set; } = null!;
    public DbSet<Author> Authors { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<BlogPost>()
          .HasMany(p => p.Comments)
          .WithOne(c => c.Post)
          .HasForeignKey(c => c.BlogPostId);

        mb.Entity<BlogPost>()
          .HasOne(p => p.Author)
          .WithMany(a => a.Posts)
          .HasForeignKey(p => p.AuthorId);
    }
}

[TestClass]
public class EFQueryableExtensionTests : IDisposable
{
    private readonly BlogContext _db;

    public EFQueryableExtensionTests()
    {
        DbContextOptions<BlogContext> options = new DbContextOptionsBuilder<BlogContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new BlogContext(options);
        SeedDatabase(_db);
    }

    private static void SeedDatabase(BlogContext db)
    {
        var author1 = new Author { Id = 1, Name = "AuthorA" };
        var author2 = new Author { Id = 2, Name = "AuthorB" };
        db.Authors.AddRange(author1, author2);

        db.Posts.AddRange(
            new BlogPost
            {
                Id = 1,
                Title = "Alpha Post",
                AuthorId = 1,
                Comments = [new Comment { Id = 1, Body = "Comment 1" }]
            },
            new BlogPost
            {
                Id = 2,
                Title = "Beta Post",
                AuthorId = 1,
                Comments = [new Comment { Id = 2, Body = "Comment 2" }, new Comment { Id = 3, Body = "Comment 3" }]
            },
            new BlogPost
            {
                Id = 3,
                Title = "Gamma Post",
                AuthorId = 2,
                Comments = []
            }
        );
        db.SaveChanges();
    }

    private static ApiQueryOptions<BlogPost> Options(
        params (string key, string value)[] pairs)
    {
        Dictionary<string, StringValues> dict = pairs.ToDictionary(
            p => p.key,
            p => new StringValues(p.value));
        return new ApiQueryOptions<BlogPost>(new QueryCollection(dict));
    }


    [TestMethod]
    public void ApplyExpand_SingleProperty_LoadsNavigation()
    {
        var result = _db.Posts
            .ApplyExpand(new ExpandQueryOption("Comments"), new ApiQueryOptionsSettings())
            .ToList();

        result.Should().HaveCount(3);
        result.Find(p => p.Id == 2)!.Comments.Should().HaveCount(2);
    }

    [TestMethod]
    public void ApplyExpand_MultipleProperties_LoadsBoth()
    {
        var result = _db.Posts
            .ApplyExpand(new ExpandQueryOption("Comments,Author"), new ApiQueryOptionsSettings())
            .ToList();

        result.Find(p => p.Id == 1)!.Comments.Should().HaveCount(1);
        result.Find(p => p.Id == 1)!.Author!.Name.Should().Be("AuthorA");
    }

    [TestMethod]
    public void ApplyExpand_Disabled_ThrowsQueryOptionDisabledException()
    {
        var settings = new ApiQueryOptionsSettings { ExpandEnabled = false };
        Func<List<BlogPost>> act = () => _db.Posts
            .ApplyExpand(new ExpandQueryOption("Comments"), settings)
            .ToList();

        act.Should().Throw<QueryOptionDisabledException>()
            .Which.OptionName.Should().Be("expand");
    }

    [TestMethod]
    public void ApplyExpand_Empty_ReturnsSameQuery()
    {
        var result = _db.Posts
            .ApplyExpand(new ExpandQueryOption(string.Empty), new ApiQueryOptionsSettings())
            .ToList();
        result.Should().HaveCount(3);
    }


    [TestMethod]
    public void Apply_FilterAndExpand_ComposedCorrectly()
    {
        ApiQueryOptions<BlogPost> opts = Options(("$filter", "Title eq 'Alpha Post'"), ("$expand", "Comments"));
        var result = _db.Posts.Apply(opts).ToList();

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Alpha Post");
        result[0].Comments.Should().HaveCount(1);
    }

    [TestMethod]
    public void Apply_TopAndExpand_ComposedCorrectly()
    {
        ApiQueryOptions<BlogPost> opts = Options(("$top", "2"), ("$expand", "Author"));
        var result = _db.Posts.Apply(opts).ToList();

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.Author.Should().NotBeNull());
    }

    [TestMethod]
    public void Apply_NoExpandOption_ExpandNotApplied()
    {
        ApiQueryOptions<BlogPost> opts = Options(("$top", "1"));
        // Should not throw even though Expand is null
        var result = _db.Posts.Apply(opts).ToList();
        result.Should().HaveCount(1);
    }

    public void Dispose() => _db.Dispose();
}
