namespace ApiQueryOptions.Tests;

[TestClass]
public sealed class PagedResponseTests
{
    [TestMethod]
    public void Constructor_ValueOnly_SetsValueAndNullDefaults()
    {
        string[] items = ["a", "b", "c"];

        var response = new PagedResponse<string>(items);

        response.Value.Should().Equal(items);
        response.NextLink.Should().BeNull();
        response.Count.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_WithNextLink_SetsNextLink()
    {
        int[] items = [1, 2, 3];
        string link = "https://example.com/items?$skiptoken=abc123";

        var response = new PagedResponse<int>(items, nextLink: link);

        response.NextLink.Should().Be(link);
        response.Count.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_WithCount_SetsCount()
    {
        int[] items = [1, 2];

        var response = new PagedResponse<int>(items, count: 42);

        response.Count.Should().Be(42);
        response.NextLink.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_WithAllParameters_SetsAll()
    {
        string[] items = ["x"];
        string link = "https://example.com/items?$skiptoken=next";

        var response = new PagedResponse<string>(items, nextLink: link, count: 100);

        response.Value.Should().Equal(items);
        response.NextLink.Should().Be(link);
        response.Count.Should().Be(100);
    }

    [TestMethod]
    public void Constructor_EmptyCollection_IsValid()
    {
        var response = new PagedResponse<string>([]);

        response.Value.Should().BeEmpty();
        response.NextLink.Should().BeNull();
        response.Count.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_CountZero_IsValid()
    {
        var response = new PagedResponse<string>([], count: 0);

        response.Count.Should().Be(0);
    }
}
