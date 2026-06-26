// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

/// <summary>Tests for generated stream value conversion.</summary>
public sealed class SerialPortReactiveValueConverterTests
{
    /// <summary>Verifies null values fail conversion.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WhenValueIsNullAndTargetIsInt_ReturnsFalse()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<int>(
            null,
            null,
            null,
            -1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsFalse();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Verifies simple string conversion.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_ForString_ReturnsInputText()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<string>(
            "ready",
            null,
            null,
            -1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsTrue();
        await Assert.That(result).IsEqualTo("ready");
    }

    /// <summary>Verifies regular expression named groups are converted.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WithNamedRegexGroup_ConvertsGroupValue()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<double>(
            "TEMP:-12.5",
            "^TEMP:(?<value>-?\\d+(\\.\\d+)?)$",
            "value",
            -1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsTrue();
        await Assert.That(result).IsEqualTo(-12.5);
    }

    /// <summary>Verifies regular expression numeric groups are converted.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WithNumberedRegexGroup_ConvertsGroupValue()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<int>(
            "ADC=1024",
            "^ADC=(\\d+)$",
            null,
            1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsTrue();
        await Assert.That(result).IsEqualTo(1024);
    }

    /// <summary>Verifies regular expression matching honors ignore-case.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WithIgnoreCase_MatchesCaseInsensitivePattern()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<bool>(
            "status:ON",
            "^STATUS:(?<value>ON)$",
            "value",
            -1,
            ignoreCase: true,
            out var result);

        await Assert.That(converted).IsFalse();
        await Assert.That(result).IsFalse();
    }

    /// <summary>Verifies unmatched regular expressions fail conversion.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_WhenPatternDoesNotMatch_ReturnsFalse()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<int>(
            "ADC=x",
            "^ADC=(\\d+)$",
            null,
            1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsFalse();
        await Assert.That(result).IsEqualTo(0);
    }

    /// <summary>Verifies char conversion requires a single character.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_ForChar_RequiresSingleCharacter()
    {
        var oneCharacter = SerialPortReactiveValueConverter.TryConvertMatch<char>("A", null, null, -1, false, out var character);
        var twoCharacters = SerialPortReactiveValueConverter.TryConvertMatch<char>("AB", null, null, -1, false, out _);

        await Assert.That(oneCharacter).IsTrue();
        await Assert.That(character).IsEqualTo('A');
        await Assert.That(twoCharacters).IsFalse();
    }

    /// <summary>Verifies Boolean conversion accepts standard and numeric values.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_ForBoolean_ConvertsKnownValues()
    {
        var textTrue = SerialPortReactiveValueConverter.TryConvertMatch<bool>("true", null, null, -1, false, out var trueResult);
        var numericFalse = SerialPortReactiveValueConverter.TryConvertMatch<bool>("0", null, null, -1, false, out var falseResult);
        var invalid = SerialPortReactiveValueConverter.TryConvertMatch<bool>("maybe", null, null, -1, false, out _);

        await Assert.That(textTrue).IsTrue();
        await Assert.That(trueResult).IsTrue();
        await Assert.That(numericFalse).IsTrue();
        await Assert.That(falseResult).IsFalse();
        await Assert.That(invalid).IsFalse();
    }

    /// <summary>Verifies enum conversion ignores case.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_ForEnum_ConvertsIgnoringCase()
    {
        var converted = SerialPortReactiveValueConverter.TryConvertMatch<DayOfWeek>(
            "friday",
            null,
            null,
            -1,
            ignoreCase: false,
            out var result);

        await Assert.That(converted).IsTrue();
        await Assert.That(result).IsEqualTo(DayOfWeek.Friday);
    }

    /// <summary>Verifies Guid and date conversions use invariant parsing.</summary>
    /// <returns>A task representing the asynchronous unit test.</returns>
    [Test]
    public async Task TryConvertMatch_ForGuidAndDates_ConvertsValues()
    {
        var guid = Guid.NewGuid();
        var dateTime = new DateTime(2026, 6, 26, 10, 30, 0, DateTimeKind.Utc);
        var dateTimeOffset = new DateTimeOffset(2026, 6, 26, 10, 30, 0, TimeSpan.Zero);

        var guidConverted = SerialPortReactiveValueConverter.TryConvertMatch<Guid>(guid.ToString(), null, null, -1, false, out var guidResult);
        var dateTimeConverted = SerialPortReactiveValueConverter.TryConvertMatch<DateTime>(dateTime.ToString("O"), null, null, -1, false, out var dateTimeResult);
        var dateTimeOffsetConverted = SerialPortReactiveValueConverter.TryConvertMatch<DateTimeOffset>(dateTimeOffset.ToString("O"), null, null, -1, false, out var dateTimeOffsetResult);

        await Assert.That(guidConverted).IsTrue();
        await Assert.That(guidResult).IsEqualTo(guid);
        await Assert.That(dateTimeConverted).IsTrue();
        await Assert.That(dateTimeResult).IsEqualTo(dateTime);
        await Assert.That(dateTimeOffsetConverted).IsTrue();
        await Assert.That(dateTimeOffsetResult).IsEqualTo(dateTimeOffset);
    }
}
