using System.Reflection;
using ApiQueryOptions.Extensions;
using ApiQueryOptions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

/// <summary>
/// This test project does not reference <c>ApiQueryOptions.EntityFrameworkCore</c>, so these tests
/// cover the "package not installed" path. The EF Core test project covers the installed path.
/// </summary>
[TestClass]
public class ExpandApplierTests
{
    private static readonly ExpandEntity[] Data = [new() { Id = 1 }, new() { Id = 2 }];

    private static ApiQueryOptions<ExpandEntity> Options(params (string Key, string Value)[] pairs) =>
        new(new QueryCollection(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Value))));

    [TestMethod]
    public void Apply_ExpandWithoutEfPackage_IsIgnored()
    {
        IQueryable<ExpandEntity> withExpand = Data.AsQueryable().Apply(Options(("$top", "1"), ("$expand", "Owner")));
        IQueryable<ExpandEntity> withoutExpand = Data.AsQueryable().Apply(Options(("$top", "1")));

        withExpand.Expression.ToString().Should().Be(withoutExpand.Expression.ToString());
        withExpand.Select(e => e.Id).Should().Equal(1);
    }

    [TestMethod]
    public void Apply_NullOptions_ThrowsArgumentNull()
    {
        Action act = () => Data.AsQueryable().Apply<ExpandEntity>(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [TestMethod]
    public void ExpandApplier_EfPackageNotInstalled_ReturnsQueryUnchanged()
    {
        IQueryable<ExpandEntity> query = Data.AsQueryable();

        IQueryable<ExpandEntity> result = ExpandApplier.Apply(query, new ExpandQueryOption("Owner"), new ApiQueryOptionsSettings());

        result.Should().BeSameAs(query);
    }

    [TestMethod]
    public void FindApplyExpandMethod_EfPackageNotInstalled_ReturnsNull()
    {
        ExpandApplier.FindApplyExpandMethod(ExpandApplier.EntityFrameworkCoreTypeName).Should().BeNull();
    }

    [TestMethod]
    public void FindApplyExpandMethod_TypeWithoutMethod_ReturnsNull()
    {
        ExpandApplier.FindApplyExpandMethod(typeof(string).AssemblyQualifiedName!).Should().BeNull();
    }

    [TestMethod]
    public void FindApplyExpandMethod_OnlyNonMatchingOverloads_ReturnsNull()
    {
        ExpandApplier.FindApplyExpandMethod(typeof(NonMatchingExpandHost).AssemblyQualifiedName!).Should().BeNull();
    }

    [TestMethod]
    public void FindApplyExpandMethod_MatchingMethod_ReturnsIt()
    {
        MethodInfo? method = ExpandApplier.FindApplyExpandMethod(typeof(MatchingExpandHost).AssemblyQualifiedName!);

        method.Should().NotBeNull();
        method!.DeclaringType.Should().Be(typeof(MatchingExpandHost));
        method.GetParameters().Should().HaveCount(3);
    }
}

internal sealed class ExpandEntity
{
    public int Id { get; set; }
}

/// <summary>
/// Has methods named <c>ApplyExpand</c> that must not be picked: one is not generic, one has the wrong
/// parameter count, and a matching shape exists only under a different name.
/// </summary>
public static class NonMatchingExpandHost
{
    public static IQueryable<int> ApplyExpand(IQueryable<int> query, ExpandQueryOption expand, ApiQueryOptionsSettings settings) => query;

    public static IQueryable<T> ApplyExpand<T>(IQueryable<T> query, ExpandQueryOption expand) => query;

    public static IQueryable<T> ApplyOther<T>(IQueryable<T> query, ExpandQueryOption expand, ApiQueryOptionsSettings settings) => query;
}

/// <summary>
/// Has exactly one method with the shape <c>ExpandApplier</c> looks for.
/// </summary>
public static class MatchingExpandHost
{
    public static IQueryable<T> ApplyExpand<T>(IQueryable<T> query, ExpandQueryOption expand, ApiQueryOptionsSettings settings) => query;

    public static IQueryable<T> ApplyExpand<T>(IQueryable<T> query, ExpandQueryOption expand) => query;
}
