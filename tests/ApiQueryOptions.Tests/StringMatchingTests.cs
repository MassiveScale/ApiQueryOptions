using System.Globalization;
using ApiQueryOptions.Extensions;
using ApiQueryOptions.Options;

namespace ApiQueryOptions.Tests;

[TestClass]
public class StringMatchingTests
{
    private static readonly MatchEntity[] Data =
    [
        new() { Id = 1, Name = "Alice" },
        new() { Id = 2, Name = "alice" },
        new() { Id = 3, Name = null },
        new() { Id = 4, Name = "Bob" },
        new() { Id = 5, Name = "Istanbul" },
        new() { Id = 6, Name = "ALICE B" },
    ];

    private static IQueryable<MatchEntity> InMemory(string filter, StringMatching matching = StringMatching.IgnoreCase) =>
        Data.AsQueryable().ApplyFilter(new FilterQueryOption(filter), new ApiQueryOptionsSettings { StringMatching = matching });

    private static IQueryable<MatchEntity> Remote(string filter, StringMatching matching = StringMatching.IgnoreCase) =>
        new RemoteQueryable<MatchEntity>().ApplyFilter(new FilterQueryOption(filter), new ApiQueryOptionsSettings { StringMatching = matching });

    private static List<int> Ids(IEnumerable<MatchEntity> rows) => rows.Select(r => r.Id).ToList();

    private static List<int> RunRemote(string filter, StringMatching matching = StringMatching.IgnoreCase) =>
        Ids(ExpressionInspector.Run(Remote(filter, matching), Data));

    private static void WithCulture(string name, Action action)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    [DataRow("Name eq 'ALICE'", new[] { 1, 2 })]
    [DataRow("Name ne 'alice'", new[] { 3, 4, 5, 6 })]
    [DataRow("Name eq null", new[] { 3 })]
    [DataRow("Name ne null", new[] { 1, 2, 4, 5, 6 })]
    [DataRow("startswith(Name, 'al')", new[] { 1, 2, 6 })]
    [DataRow("endswith(Name, 'CE')", new[] { 1, 2 })]
    [DataRow("contains(Name, 'LIC')", new[] { 1, 2, 6 })]
    [DataRow("Name lt 'b'", new[] { 1, 2, 6 })]
    [DataRow("Name le 'ALICE'", new[] { 1, 2 })]
    [DataRow("Name gt 'alice'", new[] { 4, 5, 6 })]
    [DataRow("Name ge 'B'", new[] { 4, 5 })]
    public void IgnoreCase_InMemory_MatchesAnyCaseAndSkipsNulls(string filter, int[] expected)
    {
        Ids(InMemory(filter)).Should().Equal(expected);
    }

    [TestMethod]
    [DataRow("Name eq 'ALICE'")]
    [DataRow("startswith(Name, 'al')")]
    [DataRow("Name lt 'b'")]
    public void IgnoreCase_InMemory_UsesOrdinalIgnoreCaseWithoutToUpper(string filter)
    {
        List<string> methods = ExpressionInspector.MethodNames(InMemory(filter).Expression);

        methods.Should().NotContain(nameof(string.ToUpper));
        ExpressionInspector.Constants(InMemory(filter).Expression).Should().Contain(StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    [DataRow("Name eq 'istanbul'")]
    [DataRow("startswith(Name, 'ist')")]
    [DataRow("Name ge 'istanbul'")]
    public void IgnoreCase_TurkishCulture_MatchesInvariantly(string filter)
    {
        WithCulture("tr-TR", () =>
        {
            Ids(InMemory(filter)).Should().Equal(5);
            RunRemote(filter).Should().Equal(5);
        });
    }

    [TestMethod]
    [DataRow("Name eq 'ALICE'", new[] { 1, 2 })]
    [DataRow("Name ne 'alice'", new[] { 3, 4, 5, 6 })]
    [DataRow("startswith(Name, 'al')", new[] { 1, 2, 6 })]
    [DataRow("endswith(Name, 'ce')", new[] { 1, 2 })]
    [DataRow("contains(Name, 'lic')", new[] { 1, 2, 6 })]
    [DataRow("Name lt 'b'", new[] { 1, 2, 6 })]
    [DataRow("Name gt 'alice'", new[] { 4, 5, 6 })]
    public void IgnoreCase_Remote_IsNullSafeAndCaseInsensitive(string filter, int[] expected)
    {
        RunRemote(filter).Should().Equal(expected);
    }

    [TestMethod]
    [DataRow("Name eq 'alice'")]
    [DataRow("contains(Name, 'alice')")]
    [DataRow("Name lt 'alice'")]
    public void IgnoreCase_Remote_UpperCasesColumnAndParameter(string filter)
    {
        IQueryable<MatchEntity> query = Remote(filter);

        ExpressionInspector.MethodNames(query.Expression).Should().Contain(nameof(string.ToUpper));
        ExpressionInspector.ParameterValues(query.Expression).Should().Contain("ALICE").And.NotContain("alice");
        ExpressionInspector.Predicate(query).Body.ToString().Should().Contain("(x.Name != null)");
    }

    [TestMethod]
    [DataRow(StringMatching.IgnoreCase)]
    [DataRow(StringMatching.CaseSensitive)]
    [DataRow(StringMatching.Database)]
    public void EqNull_AnyMode_ComparesWithNullWithoutCalls(StringMatching matching)
    {
        IQueryable<MatchEntity> remote = Remote("Name eq null", matching);

        ExpressionInspector.MethodNames(remote.Expression).Should().Equal(nameof(Queryable.Where));
        ExpressionInspector.Predicate(remote).Body.ToString().Should().Be("(x.Name == null)");
        Ids(InMemory("Name eq null", matching)).Should().Equal(3);
    }

    [TestMethod]
    [DataRow("Name eq 'alice'", new[] { 2 })]
    [DataRow("Name ne 'alice'", new[] { 1, 3, 4, 5, 6 })]
    [DataRow("startswith(Name, 'Al')", new[] { 1 })]
    [DataRow("endswith(Name, 'e')", new[] { 1, 2 })]
    [DataRow("contains(Name, 'LIC')", new[] { 6 })]
    [DataRow("Name lt 'a'", new[] { 1, 4, 5, 6 })]
    [DataRow("Name ge 'a'", new[] { 2 })]
    public void CaseSensitive_InMemory_UsesOrdinal(string filter, int[] expected)
    {
        Ids(InMemory(filter, StringMatching.CaseSensitive)).Should().Equal(expected);
    }

    [TestMethod]
    public void CaseSensitive_InMemory_OrderingUsesCompareOrdinal()
    {
        ExpressionInspector.MethodNames(InMemory("Name lt 'a'", StringMatching.CaseSensitive).Expression)
            .Should().Contain(nameof(string.CompareOrdinal)).And.NotContain(nameof(string.ToUpper));
        ExpressionInspector.Constants(InMemory("startswith(Name, 'Al')", StringMatching.CaseSensitive).Expression)
            .Should().Contain(StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow(StringMatching.CaseSensitive)]
    [DataRow(StringMatching.Database)]
    public void PlainModes_Remote_EmitPlainCallsWithoutToUpper(StringMatching matching)
    {
        List<string> eq = ExpressionInspector.MethodNames(Remote("Name eq 'alice'", matching).Expression);
        List<string> starts = ExpressionInspector.MethodNames(Remote("startswith(Name, 'al')", matching).Expression);
        List<string> lt = ExpressionInspector.MethodNames(Remote("Name lt 'b'", matching).Expression);

        eq.Should().Equal(nameof(Queryable.Where));
        starts.Should().Equal(nameof(Queryable.Where), nameof(string.StartsWith));
        lt.Should().Equal(nameof(Queryable.Where), nameof(string.Compare));
        ExpressionInspector.Constants(Remote("startswith(Name, 'al')", matching).Expression)
            .Should().NotContain(c => c is StringComparison);
        RunRemote("Name eq 'alice'", matching).Should().Equal(2);
    }

    [TestMethod]
    [DataRow("Name eq 'alice'", new[] { 2 })]
    [DataRow("Name ne 'alice'", new[] { 1, 3, 4, 5, 6 })]
    [DataRow("contains(Name, 'lic')", new[] { 1, 2 })]
    public void Database_InMemory_UsesPlainDotNetBehavior(string filter, int[] expected)
    {
        IQueryable<MatchEntity> query = InMemory(filter, StringMatching.Database);

        Ids(query).Should().Equal(expected);
        ExpressionInspector.MethodNames(query.Expression).Should().NotContain(nameof(string.ToUpper)).And.NotContain(nameof(string.Equals));
    }

    [TestMethod]
    public void Database_InMemory_OrderingUsesStringCompare()
    {
        WithCulture("en-US", () =>
        {
            IQueryable<MatchEntity> query = InMemory("Name lt 'b'", StringMatching.Database);

            ExpressionInspector.MethodNames(query.Expression).Should().Contain(nameof(string.Compare));
            Ids(query).Should().Equal(1, 2, 6);
        });
    }

    [TestMethod]
    public void Attribute_StringMatchingSet_OverridesRegisteredValue()
    {
        var attribute = new ApiQueryOptionsAttribute { StringMatching = StringMatching.Database };

        ApiQueryOptionsSettings result = attribute.ApplyTo(new ApiQueryOptionsSettings { StringMatching = StringMatching.CaseSensitive });

        attribute.StringMatching.Should().Be(StringMatching.Database);
        result.StringMatching.Should().Be(StringMatching.Database);
    }

    [TestMethod]
    public void Attribute_StringMatchingSetToIgnoreCase_OverridesRegisteredValue()
    {
        var attribute = new ApiQueryOptionsAttribute { StringMatching = StringMatching.IgnoreCase };

        ApiQueryOptionsSettings result = attribute.ApplyTo(new ApiQueryOptionsSettings { StringMatching = StringMatching.Database });

        result.StringMatching.Should().Be(StringMatching.IgnoreCase);
    }

    [TestMethod]
    public void Attribute_StringMatchingNotSet_GetterReturnsIgnoreCase()
    {
        new ApiQueryOptionsAttribute().StringMatching.Should().Be(StringMatching.IgnoreCase);
    }

    [TestMethod]
    public void Builder_StringMatchingDefault_IsIgnoreCase()
    {
        new ApiQueryOptionsBuilder().StringMatching.Should().Be(StringMatching.IgnoreCase);
    }
}

internal sealed class MatchEntity
{
    public int Id { get; set; }

    public string? Name { get; set; }
}
