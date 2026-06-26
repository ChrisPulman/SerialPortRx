// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.SourceGeneration;

/// <summary>Converts generated serial stream values into strongly typed reactive properties.</summary>
public static class SerialPortReactiveValueConverter
{
    /// <summary>Tries to match and convert a serial stream value.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="value">The raw stream value.</param>
    /// <param name="pattern">The optional regular expression pattern.</param>
    /// <param name="groupName">The optional named group to convert.</param>
    /// <param name="groupNumber">The fallback group number to convert.</param>
    /// <param name="ignoreCase">A value indicating whether the match ignores case.</param>
    /// <param name="result">The converted value.</param>
    /// <returns><c>true</c> when a value was matched and converted; otherwise, <c>false</c>.</returns>
    public static bool TryConvertMatch<T>(
        object? value,
        string? pattern,
        string? groupName,
        int groupNumber,
        bool ignoreCase,
        out T result)
    {
        result = default!;

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (text is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(pattern))
        {
            var options = RegexOptions.CultureInvariant;
            if (ignoreCase)
            {
                options |= RegexOptions.IgnoreCase;
            }

            var match = Regex.Match(text, pattern!, options);
            if (!match.Success)
            {
                return false;
            }

            text = GetMatchValue(match, groupName, groupNumber);
        }

        return TryConvert(text, out result);
    }

    /// <summary>Gets the value selected by a regular expression match.</summary>
    /// <param name="match">The regular expression match.</param>
    /// <param name="groupName">The optional named group to convert.</param>
    /// <param name="groupNumber">The fallback group number to convert.</param>
    /// <returns>The selected match value.</returns>
    private static string GetMatchValue(Match match, string? groupName, int groupNumber)
    {
        if (!string.IsNullOrWhiteSpace(groupName))
        {
            var namedGroup = match.Groups[groupName!];
            if (namedGroup.Success)
            {
                return namedGroup.Value;
            }
        }

        if (groupNumber >= 0 && groupNumber < match.Groups.Count)
        {
            var numberedGroup = match.Groups[groupNumber];
            if (numberedGroup.Success)
            {
                return numberedGroup.Value;
            }
        }

        return match.Value;
    }

    /// <summary>Tries to convert text into the target type.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="value">The text value.</param>
    /// <param name="result">The converted result.</param>
    /// <returns><see langword="true"/> when conversion succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvert<T>(string value, out T result)
    {
        result = default!;
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        try
        {
            result = (T)ConvertValue(value, targetType)!;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Converts text into the specified target type.</summary>
    /// <param name="value">The text value.</param>
    /// <param name="targetType">The target type.</param>
    /// <returns>The converted value.</returns>
    private static object? ConvertValue(string value, Type targetType) =>
        targetType switch
        {
            _ when targetType == typeof(string) => value,
            _ when targetType == typeof(char) => ConvertChar(value),
            _ when targetType == typeof(bool) => ConvertBoolean(value),
            _ => ConvertKnownValue(value, targetType),
        };

    /// <summary>Converts text into a character.</summary>
    /// <param name="value">The text value.</param>
    /// <returns>The converted character.</returns>
    private static char ConvertChar(string value)
    {
        if (value.Length != 1)
        {
            throw new FormatException("Value is not a valid Char.");
        }

        return value[0];
    }

    /// <summary>Converts text into a Boolean value.</summary>
    /// <param name="value">The text value.</param>
    /// <returns>The converted Boolean value.</returns>
    private static bool ConvertBoolean(string value) =>
        bool.TryParse(value, out var boolValue)
            ? boolValue
            : value switch
            {
                "1" => true,
                "0" => false,
                _ => throw new FormatException("Value is not a valid Boolean."),
            };

    /// <summary>Converts text into known primitive, framework, or type-converter values.</summary>
    /// <param name="value">The text value.</param>
    /// <param name="targetType">The target type.</param>
    /// <returns>The converted value.</returns>
    private static object? ConvertKnownValue(string value, Type targetType)
    {
        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, ignoreCase: true);
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        var converter = TypeDescriptor.GetConverter(targetType);
        return converter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
    }
}
