using ApiQueryOptions.Options;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

[TestClass]
public class ExpandIncludesTests
{
    [TestMethod]
    [DataRow("Orders", "Orders")]
    [DataRow("orders", "ORDERS")]
    [DataRow("Orders.Items", "Orders")]
    [DataRow("orders.items", "Orders.Items")]
    [DataRow("Owner, orders.items.product", "Orders.Items")]
    [DataRow("Owner,Orders", "Owner")]
    public void Includes_RequestedPathOrParent_ReturnsTrue(string expand, string propertyPath)
    {
        new ExpandQueryOption(expand).Includes(propertyPath).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("Orders", "Order")]
    [DataRow("Order", "Orders")]
    [DataRow("OrdersArchive", "Orders")]
    [DataRow("Orders", "Orders.Items")]
    [DataRow("Orders.Items", "Items")]
    [DataRow("Pets.Toys", "Cats")]
    [DataRow("", "Orders")]
    public void Includes_PathNotRequested_ReturnsFalse(string expand, string propertyPath)
    {
        new ExpandQueryOption(expand).Includes(propertyPath).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    public void Includes_BlankPath_Throws(string propertyPath)
    {
        Action act = () => new ExpandQueryOption("Orders").Includes(propertyPath);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Includes_NullPath_Throws()
    {
        Action act = () => new ExpandQueryOption("Orders").Includes(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Expands_RequestedPath_ReturnsTrue()
    {
        Options(new ApiQueryOptionsSettings(), ("$expand", "owner")).Expands("Owner").Should().BeTrue();
    }

    [TestMethod]
    public void Expands_PathNotRequested_ReturnsFalse()
    {
        Options(new ApiQueryOptionsSettings(), ("$expand", "owner")).Expands("Orders").Should().BeFalse();
    }

    [TestMethod]
    public void Expands_NoExpand_ReturnsFalse()
    {
        Options(new ApiQueryOptionsSettings()).Expands("Owner").Should().BeFalse();
    }

    [TestMethod]
    public void Expands_ExpandDisabled_ReturnsFalse()
    {
        Options(new ApiQueryOptionsSettings { ExpandEnabled = false }, ("$expand", "owner")).Expands("Owner").Should().BeFalse();
    }

    [TestMethod]
    public void Expands_BlankPathWithoutExpand_Throws()
    {
        Action act = () => Options(new ApiQueryOptionsSettings()).Expands(" ");

        act.Should().Throw<ArgumentException>();
    }

    private static ApiQueryOptions<SampleEntity> Options(ApiQueryOptionsSettings settings, params (string Key, string Value)[] pairs) =>
        new(new QueryCollection(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Value))), settings);
}
