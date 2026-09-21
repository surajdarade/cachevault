using System.Text;
using CacheVault.Protocol.Exceptions;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.UnitTests.Resp;

public sealed class RespParserTests {
    private readonly RespParser _parser = new();

    [Fact]
    public void Parse_SimpleString_ReturnsSimpleString() {
        RespValue result =
            _parser.Parse(
                "+PONG\r\n"u8);

        Assert.Equal(
            new RespSimpleString("PONG"),
            result);
    }

    [Fact]
    public void Parse_Error_ReturnsError() {
        RespValue result =
            _parser.Parse(
                "-ERR unknown command\r\n"u8);

        Assert.Equal(
            new RespError("ERR unknown command"),
            result);
    }

    [Fact]
    public void Parse_Integer_ReturnsInteger() {
        RespValue result =
            _parser.Parse(
                ":42\r\n"u8);

        Assert.Equal(
            new RespInteger(42),
            result);
    }

    [Fact]
    public void Parse_NegativeInteger_ReturnsInteger() {
        RespValue result =
            _parser.Parse(
                ":-42\r\n"u8);

        Assert.Equal(
            new RespInteger(-42),
            result);
    }

    [Fact]
    public void Parse_ZeroInteger_ReturnsInteger() {
        RespValue result =
            _parser.Parse(
                ":0\r\n"u8);

        Assert.Equal(
            new RespInteger(0),
            result);
    }

    [Fact]
    public void Parse_BulkString_ReturnsBulkString() {
        RespValue result =
            _parser.Parse(
                "$5\r\nhello\r\n"u8);

        Assert.Equal(
            new RespBulkString("hello"),
            result);
    }

    [Fact]
    public void Parse_EmptyBulkString_ReturnsEmptyString() {
        RespValue result =
            _parser.Parse(
                "$0\r\n\r\n"u8);

        Assert.Equal(
            new RespBulkString(string.Empty),
            result);
    }

    [Fact]
    public void Parse_NullBulkString_ReturnsNull() {
        RespValue result =
            _parser.Parse(
                "$-1\r\n"u8);

        Assert.Equal(
            new RespBulkString(null),
            result);
    }

    [Fact]
    public void Parse_Utf8BulkString_ReturnsCorrectValue() {
        string value = "héllo 世界";

        byte[] content =
            Encoding.UTF8.GetBytes(value);

        byte[] input =
            Encoding.UTF8.GetBytes(
                $"${content.Length}\r\n");

        byte[] suffix =
            "\r\n"u8.ToArray();

        byte[] data =
            [.. input, .. content, .. suffix];

        RespValue result =
            _parser.Parse(data);

        Assert.Equal(
            new RespBulkString(value),
            result);
    }

    [Fact]
    public void Parse_EmptyArray_ReturnsEmptyArray() {
        RespValue result =
            _parser.Parse(
                "*0\r\n"u8);

        Assert.Equal(
            new RespArray([]),
            result);
    }

    [Fact]
    public void Parse_NullArray_ReturnsNull() {
        RespValue result =
            _parser.Parse(
                "*-1\r\n"u8);

        Assert.Equal(
            new RespArray(null),
            result);
    }

    [Fact]
    public void Parse_Array_ReturnsElements() {
        RespValue result =
            _parser.Parse(
                Encoding.UTF8.GetBytes(
                    "*2\r\n" +
                    "$4\r\nPING\r\n" +
                    "$5\r\nhello\r\n"));

        RespArray array =
            AssertArray(
                result,
                2);

        Assert.Equal(
            new RespBulkString("PING"),
            array.Values![0]);

        Assert.Equal(
            new RespBulkString("hello"),
            array.Values[1]);
    }

    [Fact]
    public void Parse_ArrayWithMixedTypes_ReturnsElements() {
        RespValue result =
            _parser.Parse(
                Encoding.UTF8.GetBytes(
                    "*4\r\n" +
                    "+OK\r\n" +
                    ":42\r\n" +
                    "$5\r\nhello\r\n" +
                    "-ERR failed\r\n"));

        RespArray array =
            AssertArray(
                result,
                4);

        Assert.Equal(
            new RespSimpleString("OK"),
            array.Values![0]);

        Assert.Equal(
            new RespInteger(42),
            array.Values[1]);

        Assert.Equal(
            new RespBulkString("hello"),
            array.Values[2]);

        Assert.Equal(
            new RespError("ERR failed"),
            array.Values[3]);
    }

    [Fact]
    public void Parse_NestedArray_ReturnsNestedStructure() {
        RespValue result =
            _parser.Parse(
                Encoding.UTF8.GetBytes(
                    "*2\r\n" +
                    "*2\r\n" +
                    ":1\r\n" +
                    ":2\r\n" +
                    "*2\r\n" +
                    "$3\r\nfoo\r\n" +
                    "$3\r\nbar\r\n"));

        RespArray outer =
            AssertArray(
                result,
                2);

        RespArray firstNested =
            AssertArray(
                outer.Values![0],
                2);

        Assert.Equal(
            new RespInteger(1),
            firstNested.Values![0]);

        Assert.Equal(
            new RespInteger(2),
            firstNested.Values[1]);

        RespArray secondNested =
            AssertArray(
                outer.Values[1],
                2);

        Assert.Equal(
            new RespBulkString("foo"),
            secondNested.Values![0]);

        Assert.Equal(
            new RespBulkString("bar"),
            secondNested.Values[1]);
    }

    [Fact]
    public void Parse_DeeplyNestedArray_ReturnsCorrectStructure() {
        RespValue result =
            _parser.Parse(
                Encoding.UTF8.GetBytes(
                    "*1\r\n" +
                    "*1\r\n" +
                    "*1\r\n" +
                    ":123\r\n"));

        RespArray level1 =
            AssertArray(
                result,
                1);

        RespArray level2 =
            AssertArray(
                level1.Values![0],
                1);

        RespArray level3 =
            AssertArray(
                level2.Values![0],
                1);

        Assert.Equal(
            new RespInteger(123),
            level3.Values![0]);
    }

    [Fact]
    public void Parse_PingCommand_ReturnsCommandArray() {
        RespValue result =
            _parser.Parse(
                "*1\r\n$4\r\nPING\r\n"u8);

        RespArray array =
            AssertArray(
                result,
                1);

        Assert.Equal(
            new RespBulkString("PING"),
            array.Values![0]);
    }

    [Fact]
    public void Parse_EmptyInput_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    ReadOnlySpan<byte>.Empty);
            });
    }

    [Fact]
    public void Parse_UnknownPrefix_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "?invalid\r\n"u8);
            });
    }

    [Fact]
    public void Parse_MissingCrLf_ThrowsIncompleteException() {
        Assert.Throws<RespIncompleteException>(
            () =>
            {
                _parser.Parse(
                    "+PONG"u8);
            });
    }

    [Fact]
    public void Parse_InvalidSimpleStringWithLineFeed_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "+PO\nNG\r\n"u8);
            });
    }

    [Fact]
    public void Parse_InvalidSimpleStringWithCarriageReturn_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "+PO\rNG\r\n"u8);
            });
    }

    [Fact]
    public void Parse_EmptyInteger_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    ":\r\n"u8);
            });
    }

    [Fact]
    public void Parse_InvalidInteger_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    ":abc\r\n"u8);
            });
    }

    [Fact]
    public void Parse_IncompleteInteger_ThrowsIncompleteException() {
        Assert.Throws<RespIncompleteException>(
            () =>
            {
                _parser.Parse(
                    ":42"u8);
            });
    }

    [Fact]
    public void Parse_EmptyBulkStringLength_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "$\r\n"u8);
            });
    }

    [Fact]
    public void Parse_InvalidBulkStringLength_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "$abc\r\n"u8);
            });
    }

    [Fact]
    public void Parse_BulkStringLengthLessThanMinusOne_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "$-2\r\n"u8);
            });
    }

    [Fact]
    public void Parse_IncompleteBulkString_ThrowsIncompleteException() {
        Assert.Throws<RespIncompleteException>(
            () =>
            {
                _parser.Parse(
                    "$5\r\nhel"u8);
            });
    }

    [Fact]
    public void Parse_BulkStringMissingTrailingCrLf_ThrowsIncompleteException() {
        Assert.Throws<RespIncompleteException>(
            () =>
            {
                _parser.Parse(
                    "$5\r\nhello\r"u8);
            });
    }

    [Fact]
    public void Parse_BulkStringWithInvalidTrailingCrLf_ThrowsFormatException() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "$5\r\nhelloXX"u8);
            });
    }

    [Fact]
    public void Parse_EmptyArrayLength_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "*\r\n"u8);
            });
    }

    [Fact]
    public void Parse_InvalidArrayLength_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "*abc\r\n"u8);
            });
    }

    [Fact]
    public void Parse_ArrayLengthLessThanMinusOne_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "*-2\r\n"u8);
            });
    }

    [Fact]
    public void Parse_IncompleteArray_ThrowsIncompleteException() {
        Assert.Throws<RespIncompleteException>(
            () =>
            {
                _parser.Parse(
                    "*2\r\n$4\r\nPING\r\n"u8);
            });
    }

    [Fact]
    public void Parse_ArrayWithInvalidElement_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "*1\r\n?invalid\r\n"u8);
            });
    }

    [Fact]
    public void Parse_TrailingData_Throws() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.Parse(
                    "+OK\r\n+PONG\r\n"u8);
            });
    }

    [Fact]
    public void TryParse_CompleteValue_ReturnsValueAndBytesConsumed() {
        byte[] data =
            Encoding.UTF8.GetBytes(
                "*1\r\n$4\r\nPING\r\n" +
                "*1\r\n$4\r\nPONG\r\n");

        bool parsed =
            _parser.TryParse(
                data,
                out RespValue? value,
                out int bytesConsumed);

        Assert.True(parsed);

        RespArray array =
            AssertArray(
                value,
                1);

        Assert.Equal(
            new RespBulkString("PING"),
            array.Values![0]);

        Assert.Equal(
            Encoding.UTF8.GetByteCount(
                "*1\r\n$4\r\nPING\r\n"),
            bytesConsumed);
    }

    [Fact]
    public void TryParse_IncompleteSimpleString_ReturnsFalse() {
        bool parsed =
            _parser.TryParse(
                "+PON"u8,
                out RespValue? value,
                out int bytesConsumed);

        Assert.False(parsed);
        Assert.Null(value);
        Assert.Equal(0, bytesConsumed);
    }

    [Fact]
    public void TryParse_IncompleteInteger_ReturnsFalse() {
        bool parsed =
            _parser.TryParse(
                ":42"u8,
                out RespValue? value,
                out int bytesConsumed);

        Assert.False(parsed);
        Assert.Null(value);
        Assert.Equal(0, bytesConsumed);
    }

    [Fact]
    public void TryParse_IncompleteBulkString_ReturnsFalse() {
        bool parsed =
            _parser.TryParse(
                "$5\r\nhel"u8,
                out RespValue? value,
                out int bytesConsumed);

        Assert.False(parsed);
        Assert.Null(value);
        Assert.Equal(0, bytesConsumed);
    }

    [Fact]
    public void TryParse_IncompleteArray_ReturnsFalse() {
        bool parsed =
            _parser.TryParse(
                "*2\r\n$4\r\nPING\r\n"u8,
                out RespValue? value,
                out int bytesConsumed);

        Assert.False(parsed);
        Assert.Null(value);
        Assert.Equal(0, bytesConsumed);
    }

    [Fact]
    public void TryParse_EmptyInput_ReturnsFalse() {
        bool parsed =
            _parser.TryParse(
                ReadOnlySpan<byte>.Empty,
                out RespValue? value,
                out int bytesConsumed);

        Assert.False(parsed);
        Assert.Null(value);
        Assert.Equal(0, bytesConsumed);
    }

    [Fact]
    public void TryParse_MalformedInput_ThrowsFormatException() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.TryParse(
                    "?invalid\r\n"u8,
                    out _,
                    out _);
            });
    }

    [Fact]
    public void TryParse_MalformedInteger_ThrowsFormatException() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.TryParse(
                    ":abc\r\n"u8,
                    out _,
                    out _);
            });
    }

    [Fact]
    public void TryParse_MalformedBulkStringLength_ThrowsFormatException() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.TryParse(
                    "$abc\r\n"u8,
                    out _,
                    out _);
            });
    }

    [Fact]
    public void TryParse_MalformedArrayLength_ThrowsFormatException() {
        Assert.Throws<FormatException>(
            () =>
            {
                _parser.TryParse(
                    "*abc\r\n"u8,
                    out _,
                    out _);
            });
    }

    [Fact]
    public void TryParse_NestedArray_ReturnsCorrectBytesConsumed() {
        string firstCommand =
            "*2\r\n" +
            "*2\r\n" +
            ":1\r\n" +
            ":2\r\n" +
            "$5\r\nhello\r\n";

        string secondCommand =
            "*1\r\n" +
            "$4\r\nPING\r\n";

        byte[] data =
            Encoding.UTF8.GetBytes(
                firstCommand +
                secondCommand);

        bool parsed =
            _parser.TryParse(
                data,
                out RespValue? value,
                out int bytesConsumed);

        Assert.True(parsed);

        RespArray outer =
            AssertArray(
                value,
                2);

        RespArray nested =
            AssertArray(
                outer.Values![0],
                2);

        Assert.Equal(
            new RespInteger(1),
            nested.Values![0]);

        Assert.Equal(
            new RespInteger(2),
            nested.Values[1]);

        Assert.Equal(
            new RespBulkString("hello"),
            outer.Values[1]);

        Assert.Equal(
            Encoding.UTF8.GetByteCount(
                firstCommand),
            bytesConsumed);
    }

    private static RespArray AssertArray(
    RespValue? value,
    int expectedCount) {
        var array =
            Assert.IsType<RespArray>(value);

        Assert.NotNull(array.Values);

        Assert.Equal(
            expectedCount,
            array.Values.Count);

        return array;
    }

    private static RespArray AssertNestedArray(
    RespValue? value,
    int expectedCount) {
        return AssertArray(
            value,
            expectedCount);
    }
}