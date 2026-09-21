using System.Text;
using CacheVault.Protocol.Exceptions;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Protocol.Resp.Parsing;

public sealed class RespParser : IRespParser {
    private const byte SimpleStringPrefix = (byte)'+';
    private const byte ErrorPrefix = (byte)'-';
    private const byte IntegerPrefix = (byte)':';
    private const byte BulkStringPrefix = (byte)'$';
    private const byte ArrayPrefix = (byte)'*';

    private const byte CarriageReturn = (byte)'\r';
    private const byte LineFeed = (byte)'\n';

    public RespValue Parse(
        ReadOnlySpan<byte> data) {
        if (data.IsEmpty) {
            throw new FormatException(
                "RESP input is empty.");
        }

        int position = 0;

        RespValue value =
            ParseValue(
                data,
                ref position);

        if (position != data.Length) {
            throw new FormatException(
                "RESP input contains trailing data.");
        }

        return value;
    }

    public bool TryParse(
        ReadOnlySpan<byte> data,
        out RespValue? value,
        out int bytesConsumed) {
        value = null;
        bytesConsumed = 0;

        if (data.IsEmpty) {
            return false;
        }

        int position = 0;

        try {
            value = ParseValue(
                data,
                ref position);

            bytesConsumed = position;

            return true;
        }
        catch (RespIncompleteException) {
            value = null;
            bytesConsumed = 0;

            return false;
        }
    }

    private static RespValue ParseValue(
        ReadOnlySpan<byte> data,
        ref int position) {
        EnsureAvailable(
            data,
            position,
            1);

        byte prefix =
            data[position++];

        return prefix switch
        {
            SimpleStringPrefix =>
                ParseSimpleString(
                    data,
                    ref position),

            ErrorPrefix =>
                ParseError(
                    data,
                    ref position),

            IntegerPrefix =>
                ParseInteger(
                    data,
                    ref position),

            BulkStringPrefix =>
                ParseBulkString(
                    data,
                    ref position),

            ArrayPrefix =>
                ParseArray(
                    data,
                    ref position),

            _ => throw new FormatException(
                $"Unknown RESP type prefix: 0x{prefix:X2}.")
        };
    }

    private static RespSimpleString ParseSimpleString(
        ReadOnlySpan<byte> data,
        ref int position) {
        ReadOnlySpan<byte> content =
            ReadLine(
                data,
                ref position);

        ValidateSimpleString(content);

        return new RespSimpleString(
            Encoding.ASCII.GetString(content));
    }

    private static RespError ParseError(
        ReadOnlySpan<byte> data,
        ref int position) {
        ReadOnlySpan<byte> content =
            ReadLine(
                data,
                ref position);

        return new RespError(
            Encoding.ASCII.GetString(content));
    }

    private static RespInteger ParseInteger(
        ReadOnlySpan<byte> data,
        ref int position) {
        ReadOnlySpan<byte> content =
            ReadLine(
                data,
                ref position);

        if (content.IsEmpty) {
            throw new FormatException(
                "RESP integer cannot be empty.");
        }

        if (!long.TryParse(
                content,
                out long value)) {
            throw new FormatException(
                "Invalid RESP integer.");
        }

        return new RespInteger(value);
    }

    private static RespBulkString ParseBulkString(
        ReadOnlySpan<byte> data,
        ref int position) {
        ReadOnlySpan<byte> lengthData =
            ReadLine(
                data,
                ref position);

        if (lengthData.IsEmpty) {
            throw new FormatException(
                "RESP bulk string length cannot be empty.");
        }

        if (!int.TryParse(
                lengthData,
                out int length)) {
            throw new FormatException(
                "Invalid RESP bulk string length.");
        }

        if (length == -1) {
            return new RespBulkString(null);
        }

        if (length < -1) {
            throw new FormatException(
                "RESP bulk string length cannot be less than -1.");
        }

        EnsureAvailable(
            data,
            position,
            length + 2);

        ReadOnlySpan<byte> content =
            data.Slice(
                position,
                length);

        position += length;

        ValidateCarriageReturnLineFeed(
            data,
            ref position);

        return new RespBulkString(
            Encoding.UTF8.GetString(content));
    }

    private static RespArray ParseArray(
        ReadOnlySpan<byte> data,
        ref int position) {
        ReadOnlySpan<byte> countData =
            ReadLine(
                data,
                ref position);

        if (countData.IsEmpty) {
            throw new FormatException(
                "RESP array length cannot be empty.");
        }

        if (!int.TryParse(
                countData,
                out int count)) {
            throw new FormatException(
                "Invalid RESP array length.");
        }

        if (count == -1) {
            return new RespArray(null);
        }

        if (count < -1) {
            throw new FormatException(
                "RESP array length cannot be less than -1.");
        }

        if (count == 0) {
            return new RespArray([]);
        }

        var values =
            new RespValue[count];

        for (int i = 0; i < count; i++) {
            values[i] =
                ParseValue(
                    data,
                    ref position);
        }

        return new RespArray(values);
    }

    private static ReadOnlySpan<byte> ReadLine(
        ReadOnlySpan<byte> data,
        ref int position) {
        int lineFeedIndex =
            data[position..]
                .IndexOf(LineFeed);

        if (lineFeedIndex < 0) {
            throw new RespIncompleteException();
        }

        int end =
            position + lineFeedIndex;

        if (end == position ||
            data[end - 1] != CarriageReturn) {
            throw new FormatException(
                "RESP line must end with CRLF.");
        }

        ReadOnlySpan<byte> content =
            data.Slice(
                position,
                end - position - 1);

        position =
            end + 1;

        return content;
    }

    private static void ValidateCarriageReturnLineFeed(
        ReadOnlySpan<byte> data,
        ref int position) {
        EnsureAvailable(
            data,
            position,
            2);

        if (data[position] != CarriageReturn ||
            data[position + 1] != LineFeed) {
            throw new FormatException(
                "RESP bulk string must be followed by CRLF.");
        }

        position += 2;
    }

    private static void ValidateSimpleString(
        ReadOnlySpan<byte> content) {
        foreach (byte value in content) {
            if (value == CarriageReturn ||
                value == LineFeed) {
                throw new FormatException(
                    "RESP simple string cannot contain CR or LF.");
            }
        }
    }

    private static void EnsureAvailable(
        ReadOnlySpan<byte> data,
        int position,
        int requiredBytes) {
        if (requiredBytes < 0 ||
            position < 0 ||
            data.Length - position < requiredBytes) {
            throw new RespIncompleteException();
        }
    }
}