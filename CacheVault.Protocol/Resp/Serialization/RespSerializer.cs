using System.Text;
using CacheVault.Protocol.Resp.Abstractions;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Protocol.Resp.Serialization;

public sealed class RespSerializer : IRespSerializer {
    private static readonly byte[] CrLf = "\r\n"u8.ToArray();

    public byte[] Serialize(RespValue value) {
        ArgumentNullException.ThrowIfNull(value);

        using var stream = new MemoryStream();

        WriteValue(stream, value);

        return stream.ToArray();
    }

    private static void WriteValue(
        Stream stream,
        RespValue value) {
        switch (value) {
            case RespSimpleString simpleString:
                WriteSimpleString(stream, simpleString);
                break;

            case RespError error:
                WriteError(stream, error);
                break;

            case RespInteger integer:
                WriteInteger(stream, integer);
                break;

            case RespBulkString bulkString:
                WriteBulkString(stream, bulkString);
                break;

            case RespArray array:
                WriteArray(stream, array);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unsupported RESP value type.");
        }
    }

    private static void WriteSimpleString(
        Stream stream,
        RespSimpleString value) {
        WriteAscii(stream, $"+{value.Value}\r\n");
    }

    private static void WriteError(
        Stream stream,
        RespError value) {
        WriteAscii(stream, $"-{value.Message}\r\n");
    }

    private static void WriteInteger(
        Stream stream,
        RespInteger value) {
        WriteAscii(stream, $":{value.Value}\r\n");
    }

    private static void WriteBulkString(
        Stream stream,
        RespBulkString value) {
        if (value.Value is null) {
            WriteAscii(stream, "$-1\r\n");
            return;
        }

        byte[] content = Encoding.UTF8.GetBytes(value.Value);

        WriteAscii(stream, $"${content.Length}\r\n");

        stream.Write(content);
        stream.Write(CrLf);
    }

    private static void WriteArray(
        Stream stream,
        RespArray value) {
        if (value.Values is null) {
            WriteAscii(stream, "*-1\r\n");
            return;
        }

        WriteAscii(stream, $"*{value.Values.Count}\r\n");

        foreach (RespValue item in value.Values) {
            ArgumentNullException.ThrowIfNull(item);

            WriteValue(stream, item);
        }
    }

    private static void WriteAscii(
        Stream stream,
        string value) {
        byte[] bytes = Encoding.ASCII.GetBytes(value);

        stream.Write(bytes);
    }
}