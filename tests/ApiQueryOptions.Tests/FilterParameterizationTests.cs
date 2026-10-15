using System.Linq.Expressions;
using ApiQueryOptions.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.Tests;

/// <summary>
/// Filter values must not be embedded as <see cref="ConstantExpression"/> values, otherwise
/// EF Core inlines them as SQL literals. These tests inspect the expression tree directly;
/// the EF Core test project verifies the generated SQL.
/// </summary>
[TestClass]
public class FilterParameterizationTests
{
    private static readonly ParamEntity[] Data =
    [
        new() { Id = 1, Name = "Rex", Age = 5, Price = 9.5m, IsActive = true },
        new() { Id = 2, Name = "Max", Age = null, Price = 20m, IsActive = false },
        new() { Id = 3, Name = "Bella", Age = 7, Price = 15m, IsActive = true },
    ];

    private static IQueryable<ParamEntity> Filter(string filter, ApiQueryOptionsSettings? settings = null)
    {
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["$filter"] = filter });
        return Data.AsQueryable().Apply(new ApiQueryOptions<ParamEntity>(query, settings));
    }

    private static List<object?> ConstantValues(Expression expression)
    {
        var collector = new ConstantCollector();
        collector.Visit(expression);
        return collector.Values;
    }

    [TestMethod]
    [DataRow("Id eq 2", 2)]
    [DataRow("Name eq 'Max'", "Max")]
    [DataRow("Price gt 10.5", 10.5)]
    [DataRow("IsActive eq true", true)]
    [DataRow("contains(Name, 'ell')", "ell")]
    [DataRow("startswith(Name, 'Be')", "Be")]
    public void Apply_FilterValue_IsNotAConstant(string filter, object value)
    {
        IQueryable<ParamEntity> query = Filter(filter);

        List<object?> constants = ConstantValues(query.Expression);

        constants.Where(c => Equals(c, value) || Equals(c?.ToString(), value.ToString())).Should().BeEmpty();
    }

    [TestMethod]
    public void Apply_CaseSensitiveFunction_ValueIsNotAConstant()
    {
        var settings = new ApiQueryOptionsSettings { StringComparison = StringComparison.Ordinal };

        IQueryable<ParamEntity> query = Filter("endswith(Name, 'la')", settings);

        ConstantValues(query.Expression).Should().NotContain("la");
        query.Select(p => p.Id).Should().Equal(3);
    }

    [TestMethod]
    public void Apply_NullValue_StaysAConstantNull()
    {
        IQueryable<ParamEntity> query = Filter("Age eq null");

        query.Select(p => p.Id).Should().Equal(2);
    }

    [TestMethod]
    public void Apply_NullableProperty_ParameterizedValueStillFilters()
    {
        Filter("Age eq 7").Select(p => p.Id).Should().Equal(3);
        Filter("Age ge 5").Select(p => p.Id).Should().Equal(1, 3);
    }

    [TestMethod]
    public void Apply_ParameterizedValues_StillFilterCorrectly()
    {
        Filter("Id eq 2").Select(p => p.Id).Should().Equal(2);
        Filter("Name eq 'rex'").Select(p => p.Id).Should().Equal(1);
        Filter("Name ne 'rex'").Select(p => p.Id).Should().Equal(2, 3);
        Filter("Price gt 10.5").Select(p => p.Id).Should().Equal(2, 3);
        Filter("IsActive eq false").Select(p => p.Id).Should().Equal(2);
        Filter("contains(Name, 'ELL')").Select(p => p.Id).Should().Equal(3);
    }

    [TestMethod]
    public void Apply_SameShapeDifferentValues_ProduceStructurallyEqualTrees()
    {
        string first = Filter("Id eq 1").Expression.ToString();
        string second = Filter("Id eq 2").Expression.ToString();

        first.Should().Be(second);
    }

    private sealed class ConstantCollector : ExpressionVisitor
    {
        public List<object?> Values { get; } = [];

        protected override Expression VisitConstant(ConstantExpression node)
        {
            Values.Add(node.Value);
            return base.VisitConstant(node);
        }
    }
}

internal sealed class ParamEntity
{
    public int? Age { get; set; }
    public int Id { get; set; }
    public bool IsActive { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
