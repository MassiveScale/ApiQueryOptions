using System.Collections;
using ApiQueryOptions.EntityFrameworkCore.Extensions;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Options;
using Microsoft.EntityFrameworkCore;

namespace ApiQueryOptions.EntityFrameworkCore.Tests;

[TestClass]
public sealed class ExpandValidationTests : IDisposable
{
    private readonly BlogContext _db = new(
        new DbContextOptionsBuilder<BlogContext>().UseSqlite("Data Source=:memory:").Options);

    public void Dispose() => _db.Dispose();

    [TestMethod]
    [DataRow("author", "Author")]
    [DataRow("COMMENTS", "Comments")]
    [DataRow("comments.post.author", "Comments.Post.Author")]
    public void ResolveNavigationPath_AnyCase_ReturnsDeclaredCasing(string path, string expected)
    {
        EFQueryableExtensions.ResolveNavigationPath(typeof(BlogPost), path).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("Tags.Length", "Tags.Length")]
    [DataRow("Labels.length", "Labels.Length")]
    [DataRow("History.Count", "History.Count")]
    [DataRow("Items.length", "Items.Length")]
    [DataRow("Title.Length", "Title.Length")]
    public void ResolveNavigationPath_CollectionTypes_StepIntoElementType(string path, string expected)
    {
        EFQueryableExtensions.ResolveNavigationPath(typeof(ShapedEntity), path).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("Missing", "Property 'Missing' not found on type 'BlogPost'.")]
    [DataRow("Author.Missing", "Property 'Missing' not found on type 'Author'.")]
    [DataRow("Comments.Missing", "Property 'Missing' not found on type 'Comment'.")]
    [DataRow("Author.", "Property '' not found on type 'Author'.")]
    public void ResolveNavigationPath_UnknownSegment_Throws(string path, string message)
    {
        Action act = () => EFQueryableExtensions.ResolveNavigationPath(typeof(BlogPost), path);

        InvalidQueryOptionException ex = act.Should().Throw<InvalidQueryOptionException>().Which;
        ex.OptionName.Should().Be("expand");
        ex.Property.Should().Be(path);
        ex.Message.Should().Be(message);
    }

    [TestMethod]
    [DataRow("WriteOnly")]
    [DataRow("Item")]
    public void ResolveNavigationPath_UnreadableOrIndexer_Throws(string path)
    {
        Action act = () => EFQueryableExtensions.ResolveNavigationPath(typeof(ShapedEntity), path);

        act.Should().Throw<InvalidQueryOptionException>();
    }

    [TestMethod]
    public void ResolveNavigationPath_ExactCaseMatch_IsPreferred()
    {
        EFQueryableExtensions.ResolveNavigationPath(typeof(CasedNavigations), "owner").Should().Be("owner");
        EFQueryableExtensions.ResolveNavigationPath(typeof(CasedNavigations), "OWNER").Should().Be("Owner");
    }

    [TestMethod]
    public void ApplyExpand_LowerCasePath_IncludesNavigation()
    {
        string sql = _db.Posts.ApplyExpand(new ExpandQueryOption("author,comments"), new ApiQueryOptionsSettings()).ToQueryString();

        sql.Should().Contain("\"Authors\"").And.Contain("\"Comments\"");
    }
}

internal sealed class ShapedEntity
{
    public List<string> Items { get; set; } = [];

    public ArrayList History { get; set; } = [];

    public IEnumerable<string> Labels { get; set; } = [];

    public string[] Tags { get; set; } = [];

    public string Title { get; set; } = string.Empty;

    public string WriteOnly { set { } }

    public string this[int index] => Title;
}

internal sealed class CasedNavigations
{
    public Author? Owner { get; set; }

#pragma warning disable IDE1006 // Two properties that differ only by case are the point of this test.
    public Author? owner { get; set; }
#pragma warning restore IDE1006
}
