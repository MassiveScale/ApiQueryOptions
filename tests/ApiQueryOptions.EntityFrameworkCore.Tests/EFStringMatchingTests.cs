using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.EntityFrameworkCore.Tests;

/// <summary>
/// Runs <see cref="StringMatching"/> filters against a real SQLite database. The connection
/// stays open for the life of the test so the in-memory database keeps its data.
/// </summary>
[TestClass]
public sealed class EFStringMatchingTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly PersonContext _db;

    public EFStringMatchingTests()
    {
        _connection.Open();
        _db = new PersonContext(new DbContextOptionsBuilder<PersonContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.People.AddRange(
            new Person { Id = 1, Name = "Alice" },
            new Person { Id = 2, Name = "alice" },
            new Person { Id = 3, Name = null },
            new Person { Id = 4, Name = "Bob" });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static bool IsCommandLine(string line)
    {
        string trimmed = line.TrimStart();
        return !trimmed.StartsWith(".param", StringComparison.Ordinal) && !trimmed.StartsWith("--", StringComparison.Ordinal);
    }

    private IQueryable<Person> Filter(string filter, StringMatching matching = StringMatching.IgnoreCase)
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["$filter"] = filter });
        var settings = new ApiQueryOptionsSettings { StringMatching = matching };
        return _db.People.Apply(new ApiQueryOptions<Person>(query, settings));
    }

    private string Sql(string filter, StringMatching matching = StringMatching.IgnoreCase) =>
        string.Join('\n', Filter(filter, matching).ToQueryString().Split('\n').Where(IsCommandLine));

    [TestMethod]
    [DataRow("Name eq 'ALICE'", new[] { 1, 2 })]
    [DataRow("Name ne 'alice'", new[] { 3, 4 })]
    [DataRow("Name eq null", new[] { 3 })]
    [DataRow("Name ne null", new[] { 1, 2, 4 })]
    [DataRow("startswith(Name, 'AL')", new[] { 1, 2 })]
    [DataRow("endswith(Name, 'OB')", new[] { 4 })]
    [DataRow("contains(Name, 'LIC')", new[] { 1, 2 })]
    [DataRow("Name lt 'b'", new[] { 1, 2 })]
    [DataRow("Name ge 'B'", new[] { 4 })]
    public void IgnoreCase_Sqlite_MatchesAnyCase(string filter, int[] expected)
    {
        Filter(filter).Select(p => p.Id).OrderBy(id => id).ToList().Should().Equal(expected);
    }

    [TestMethod]
    [DataRow("Name eq 'alice'", new[] { 2 })]
    [DataRow("Name lt 'a'", new[] { 1, 4 })]
    [DataRow("Name eq null", new[] { 3 })]
    public void Database_Sqlite_UsesColumnCollation(string filter, int[] expected)
    {
        Filter(filter, StringMatching.Database).Select(p => p.Id).OrderBy(id => id).ToList().Should().Equal(expected);
    }

    [TestMethod]
    [DataRow("Name eq 'alice'")]
    [DataRow("startswith(Name, 'alice')")]
    [DataRow("Name lt 'alice'")]
    public void IgnoreCase_Sql_UpperCasesColumnAndSendsUpperCaseParameter(string filter)
    {
        string full = Filter(filter).ToQueryString();

        Sql(filter).Should().Contain("upper(\"p\".\"Name\")");
        full.Should().Contain("'ALICE").And.NotContain("alice");
    }

    [TestMethod]
    [DataRow(StringMatching.Database)]
    [DataRow(StringMatching.CaseSensitive)]
    public void PlainModes_Sql_HasNoUpper(StringMatching matching)
    {
        Sql("Name eq 'alice'", matching).Should().NotContainEquivalentOf("upper(");
        Sql("startswith(Name, 'al')", matching).Should().NotContainEquivalentOf("upper(");
        Sql("Name lt 'alice'", matching).Should().NotContainEquivalentOf("upper(").And.Contain("<");
    }

    [TestMethod]
    public void EqNull_Sql_IsNullWithoutUpper()
    {
        string sql = Sql("Name eq null");

        sql.Should().Contain("IS NULL").And.NotContainEquivalentOf("upper(");
    }

    [TestMethod]
    public void ApplyExpand_UnknownNavigation_ThrowsInvalidQueryOption()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["$expand"] = "Friends" });

        Action act = () => _db.People.Apply(new ApiQueryOptions<Person>(query));

        InvalidQueryOptionException ex = act.Should().Throw<InvalidQueryOptionException>().Which;
        ex.OptionName.Should().Be("expand");
        ex.Property.Should().Be("Friends");
        ex.Message.Should().Be("Property 'Friends' not found on type 'Person'.");
    }
}

public sealed class Person
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

public sealed class PersonContext(DbContextOptions<PersonContext> options) : DbContext(options)
{
    public DbSet<Person> People { get; set; } = null!;
}
