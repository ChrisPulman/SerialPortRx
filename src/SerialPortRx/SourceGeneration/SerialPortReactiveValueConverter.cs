// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CP.IO.Ports.SourceGeneration;

/// <summary>
/// Converts generated serial stream values into strongly typed reactive properties.
/// </summary>
public static class SerialPortReactiveValueConverter
{
    /// <summary>
    /// Tries to match and convert a serial stream value.
    /// </summary>
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
        if (text == null)
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

    private static bool TryConvert<T>(string value, out T result)
    {
        result = default!;
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        try
        {
            object? converted;
            if (targetType == typeof(string))
            {
                converted = value;
            }
            else if (targetType == typeof(char))
            {
                if (value.Length != 1)
                {
                    return false;
                }

                converted = value[0];
            }
            else if (targetType == typeof(bool))
            {
                converted = bool.TryParse(value, out var boolValue)
                    ? boolValue
                    : value switch
                    {
                        "1" => true,
                        "0" => false,
                        _ => throw new FormatException("Value is not a valid Boolean."),
                    };
            }
            else if (targetType.IsEnum)
            {
                converted = Enum.Parse(targetType, value, ignoreCase: true);
            }
            else if (targetType == typeof(Guid))
            {
                converted = Guid.Parse(value);
            }
            else if (targetType == typeof(DateTime))
            {
                converted = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            else if (targetType == typeof(DateTimeOffset))
            {
                converted = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            else
            {
                var converter = TypeDescriptor.GetConverter(targetType);
                converted = converter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
            }

            result = (T)converted!;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
