using System.Globalization;
using ApiQueryOptions.Exceptions;

namespace ApiQueryOptions.Filter;

/// <summary>
/// Converts a parsed <c>$filter</c> literal to the type of the property it is compared with.
/// Every conversion uses the invariant culture, so results never depend on the server's culture.
/// </summary>
internal static class FilterValueConverter
{
    private const NumberStyles NumberLiteralStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    /// <summary>
    /// Converts <paramref name="value"/> to <paramref name="targetType"/>.
    /// </summary>
    /// <param name="value">A parsed literal: a <see cref="string"/>, <see cref="int"/>, <see cref="long"/>, <see cref="decimal"/>, or <see cref="bool"/>.</param>
    /// <param name="targetType">The property type, with any <see cref="Nullable{T}"/> wrapper removed.</param>
    /// <param name="property">The property path, reported when the conversion fails.</param>
    /// <exception cref="InvalidQueryOptionException">When the value cannot be represented exactly as <paramref name="targetType"/>.</exception>
    public static object Convert(object value, Type targetType, string property)
    {
        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        object? converted = targetType.IsEnum
            ? ToEnum(value, targetType)
            : ToType(value, targetType);

        return converted ?? throw new InvalidQueryOptionException(
            "filter",
            property,
            $"The value '{System.Convert.ToString(value, CultureInfo.InvariantCulture)}' cannot be converted to type '{targetType.Name}' for property '{property}'.");
    }

    private static object? FromNumber(decimal number, Type targetType)
    {
        switch (Type.GetTypeCode(targetType))
        {
            case TypeCode.Decimal:
                return number;
            case TypeCode.Double:
                return (double)number;
            case TypeCode.Single:
                return (float)number;
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
                // Convert.ChangeType rounds, so fractions are rejected first instead of matching a neighbouring value.
                if (number != decimal.Truncate(number))
                {
                    return null;
                }

                try
                {
                    return System.Convert.ChangeType(number, targetType, CultureInfo.InvariantCulture);
                }
                catch (OverflowException)
                {
                    return null;
                }

            default:
                return null;
        }
    }

    private static object? FromString(string text, Type targetType)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;

        if (targetType == typeof(Guid))
        {
            return Guid.TryParse(text, out Guid guid) ? guid : null;
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.TryParse(text, invariant, DateTimeStyles.AssumeUniversal, out DateTimeOffset dateTimeOffset) ? dateTimeOffset : null;
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.TryParse(text, invariant, DateTimeStyles.RoundtripKind, out DateTime dateTime) ? dateTime : null;
        }

        if (targetType == typeof(DateOnly))
        {
            return DateOnly.TryParse(text, invariant, DateTimeStyles.None, out DateOnly date) ? date : null;
        }

        if (targetType == typeof(TimeOnly))
        {
            return TimeOnly.TryParse(text, invariant, DateTimeStyles.None, out TimeOnly time) ? time : null;
        }

        if (targetType == typeof(TimeSpan))
        {
            return TimeSpan.TryParse(text, invariant, out TimeSpan timeSpan) ? timeSpan : null;
        }

        return decimal.TryParse(text, NumberLiteralStyles, invariant, out decimal number)
            ? FromNumber(number, targetType)
            : null;
    }

    private static object? ToEnum(object value, Type enumType)
    {
        object? result = value switch
        {
            string text => Enum.TryParse(enumType, text, ignoreCase: true, out object? parsed) ? parsed : null,
            bool => null,
            _ => FromNumber(System.Convert.ToDecimal(value, CultureInfo.InvariantCulture), Enum.GetUnderlyingType(enumType)) is { } number
                ? Enum.ToObject(enumType, number)
                : null,
        };

        // Undefined values are rejected unless the enum is a set of flags, where combinations are expected.
        if (result is null || (!Enum.IsDefined(enumType, result) && !enumType.IsDefined(typeof(FlagsAttribute), inherit: false)))
        {
            return null;
        }

        return result;
    }

    private static object? ToType(object value, Type targetType)
    {
        return value switch
        {
            string text => FromString(text, targetType),
            bool => null,
            _ when targetType == typeof(string) => System.Convert.ToString(value, CultureInfo.InvariantCulture),
            _ => FromNumber(System.Convert.ToDecimal(value, CultureInfo.InvariantCulture), targetType),
        };
    }
}
