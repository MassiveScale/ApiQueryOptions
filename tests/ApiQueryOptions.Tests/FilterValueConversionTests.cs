using System.Globalization;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using ApiQueryOptions.Filter;
using ApiQueryOptions.Options;

namespace ApiQueryOptions.Tests;

[TestClass]
public class FilterValueConversionTests
{
    private static readonly Guid KnownGuid = Guid.Parse("5f0c2b8e-8f3a-4c51-9d6e-2a7b1c0d9e4f");

    private static readonly TypedEntity[] Data =
    [
        new()
        {
            Id = 1,
            Age = 2,
            BigNumber = 5_000_000_000,
            Color = Color.Red,
            CreatedAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
            Day = new DateOnly(2024, 1, 15),
            Duration = TimeSpan.FromMinutes(90),
            Key = KnownGuid,
            Permissions = Permissions.Read | Permissions.Write,
            Price = 9.99m,
            Ratio = 0.5,
            Small = 200,
            StartTime = new TimeOnly(9, 0),
            UpdatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
        },
        new()
        {
            Id = 2,
            Age = 3,
            BigNumber = 7,
            Color = Color.Green,
            CreatedAt = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Day = new DateOnly(2025, 6, 1),
            Duration = TimeSpan.FromMinutes(30),
            Key = Guid.Empty,
            NullableAge = 4,
            Permissions = Permissions.Read,
            Price = 20m,
            Ratio = 1.5,
            Small = 1,
            StartTime = new TimeOnly(17, 30),
            UpdatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        },
    ];

    private static List<int> Filter(string filter) =>
        Data.AsQueryable()
            .ApplyFilter(new FilterQueryOption(filter), new ApiQueryOptionsSettings())
            .Select(e => e.Id)
            .ToList();

    private static InvalidQueryOptionException FilterFails(string filter)
    {
        Action act = () => Filter(filter);
        return act.Should().Throw<InvalidQueryOptionException>().Which;
    }

    [TestMethod]
    [DataRow("Age eq 2", 1)]
    [DataRow("Age eq '3'", 2)]
    [DataRow("Age eq 2.0", 1)]
    [DataRow("BigNumber eq 5000000000", 1)]
    [DataRow("BigNumber lt 10", 2)]
    [DataRow("Small eq 200", 1)]
    [DataRow("Price eq 9.99", 1)]
    [DataRow("Price eq 20", 2)]
    [DataRow("Ratio eq 1.5", 2)]
    [DataRow("Ratio lt 1", 1)]
    [DataRow("NullableAge eq 4", 2)]
    [DataRow("NullableAge eq null", 1)]
    [DataRow("Color eq 'green'", 2)]
    [DataRow("Color eq 'Red'", 1)]
    [DataRow("Color eq 1", 2)]
    [DataRow("Color eq '1'", 2)]
    [DataRow("Permissions eq 'Read, Write'", 1)]
    [DataRow("Permissions eq 3", 1)]
    [DataRow("Key eq '5f0c2b8e-8f3a-4c51-9d6e-2a7b1c0d9e4f'", 1)]
    [DataRow("CreatedAt eq '2024-01-15T10:30:00Z'", 1)]
    [DataRow("CreatedAt ge '2025-01-01'", 2)]
    [DataRow("UpdatedAt eq '2024-01-15T10:30:00Z'", 1)]
    [DataRow("Day eq '2025-06-01'", 2)]
    [DataRow("StartTime lt '12:00'", 1)]
    [DataRow("Duration gt '01:00:00'", 1)]
    public void ApplyFilter_ConvertibleValue_Matches(string filter, int expectedId)
    {
        Filter(filter).Should().Equal(expectedId);
    }

    [TestMethod]
    public void ApplyFilter_DateTimeWithUtcDesignator_KeepsUtcKind()
    {
        object converted = FilterValueConverter.Convert("2024-01-15T10:30:00Z", typeof(DateTime), "UpdatedAt");

        converted.Should().BeOfType<DateTime>().Which.Kind.Should().Be(DateTimeKind.Utc);
    }

    [TestMethod]
    public void ApplyFilter_DateTimeOffsetWithoutOffset_AssumesUtc()
    {
        object converted = FilterValueConverter.Convert("2024-01-15T10:30:00", typeof(DateTimeOffset), "CreatedAt");

        converted.Should().Be(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero));
    }

    [TestMethod]
    [DataRow("Age eq 1.5", "Age")]
    [DataRow("Age eq 'abc'", "Age")]
    [DataRow("Age eq true", "Age")]
    [DataRow("Small eq 300", "Small")]
    [DataRow("Small eq -1", "Small")]
    [DataRow("Age eq 5000000000", "Age")]
    [DataRow("Color eq 'Purple'", "Color")]
    [DataRow("Color eq 99", "Color")]
    [DataRow("Color eq 1.5", "Color")]
    [DataRow("Color eq true", "Color")]
    [DataRow("Key eq 'not-a-guid'", "Key")]
    [DataRow("Key eq 5", "Key")]
    [DataRow("CreatedAt eq 'yesterday'", "CreatedAt")]
    [DataRow("UpdatedAt eq 'soon'", "UpdatedAt")]
    [DataRow("Day eq '2025-13-01'", "Day")]
    [DataRow("StartTime eq '25:00'", "StartTime")]
    [DataRow("Duration eq 'long'", "Duration")]
    [DataRow("IsActive eq 1", "IsActive")]
    [DataRow("IsActive eq 'true'", "IsActive")]
    [DataRow("Name eq true", "Name")]
    [DataRow("Website eq 'https://example.com'", "Website")]
    public void ApplyFilter_UnconvertibleValue_ThrowsInvalidQueryOption(string filter, string property)
    {
        InvalidQueryOptionException ex = FilterFails(filter);

        ex.OptionName.Should().Be("filter");
        ex.Property.Should().Be(property);
        ex.Message.Should().StartWith("The value '").And.Contain($"for property '{property}'");
    }

    [TestMethod]
    public void ApplyFilter_FractionalValueForInteger_DoesNotRoundToNeighbour()
    {
        Action act = () => Filter("Age eq 2.5");

        act.Should().Throw<InvalidQueryOptionException>()
            .WithMessage("The value '2.5' cannot be converted to type 'Int32' for property 'Age'.");
    }

    [TestMethod]
    public void ApplyFilter_NumberForStringProperty_ComparesInvariantText()
    {
        var data = new[] { new TypedEntity { Id = 1, Name = "1.5" }, new TypedEntity { Id = 2, Name = "15" } };

        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            data.AsQueryable()
                .ApplyFilter(new FilterQueryOption("Name eq 1.5"), new ApiQueryOptionsSettings())
                .Select(e => e.Id)
                .Should().Equal(1);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }


    [TestMethod]
    [DataRow(typeof(sbyte), 5, (sbyte)5)]
    [DataRow(typeof(byte), 5, (byte)5)]
    [DataRow(typeof(short), 5, (short)5)]
    [DataRow(typeof(ushort), 5, (ushort)5)]
    [DataRow(typeof(uint), 5, 5u)]
    [DataRow(typeof(ulong), 5, 5ul)]
    [DataRow(typeof(float), 5, 5f)]
    [DataRow(typeof(double), 5, 5d)]
    public void Convert_IntToNumericType_ReturnsTargetType(Type target, int value, object expected)
    {
        FilterValueConverter.Convert(value, target, "P").Should().Be(expected);
    }

    [TestMethod]
    public void Convert_IntToDecimal_ReturnsDecimal()
    {
        FilterValueConverter.Convert(5, typeof(decimal), "P").Should().Be(5m);
    }

    [TestMethod]
    public void Convert_LongToLong_ReturnsSameValue()
    {
        FilterValueConverter.Convert(5_000_000_000L, typeof(long), "P").Should().Be(5_000_000_000L);
    }

    [TestMethod]
    public void Convert_UlongMaxDecimal_ReturnsUlong()
    {
        FilterValueConverter.Convert(18446744073709551615m, typeof(ulong), "P").Should().Be(ulong.MaxValue);
    }

    [TestMethod]
    public void Convert_UndefinedFlagsCombination_IsAccepted()
    {
        FilterValueConverter.Convert(7, typeof(Permissions), "P").Should().Be((Permissions)7);
    }

    [TestMethod]
    public void Convert_EnumWithLongUnderlyingType_ConvertsNumber()
    {
        FilterValueConverter.Convert(5_000_000_000L, typeof(BigEnum), "P").Should().Be(BigEnum.Large);
    }

    [TestMethod]
    public void Convert_DecimalToInt_IntegralValue_Converts()
    {
        FilterValueConverter.Convert(42.000m, typeof(int), "P").Should().Be(42);
    }

    [TestMethod]
    public void Convert_StringWithThousandsSeparator_IsRejected()
    {
        Action act = () => FilterValueConverter.Convert("1,000", typeof(int), "P");

        act.Should().Throw<InvalidQueryOptionException>().Which.Property.Should().Be("P");
    }

    [TestMethod]
    public void Convert_NumericStringUnderGermanCulture_UsesInvariantDecimalPoint()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            FilterValueConverter.Convert("1.5", typeof(decimal), "P").Should().Be(1.5m);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}

internal enum Color
{
    Red = 0,
    Green = 1,
}

[Flags]
internal enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Delete = 4,
}

internal enum BigEnum : long
{
    Large = 5_000_000_000,
}

internal sealed class TypedEntity
{
    public int Id { get; set; }

    public int Age { get; set; }

    public long BigNumber { get; set; }

    public Color Color { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateOnly Day { get; set; }

    public TimeSpan Duration { get; set; }

    public bool IsActive { get; set; }

    public Guid Key { get; set; }

    public string? Name { get; set; }

    public int? NullableAge { get; set; }

    public Permissions Permissions { get; set; }

    public decimal Price { get; set; }

    public double Ratio { get; set; }

    public byte Small { get; set; }

    public TimeOnly StartTime { get; set; }


    public DateTime UpdatedAt { get; set; }

    public Uri? Website { get; set; }
}
