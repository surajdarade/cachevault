using System.Text;
using CacheVault.Protocol.Resp.Serialization;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.UnitTests.CacheVault.Protocol.Resp.Serialization;

public sealed class RespSerializerTests {
    private readonly RespSerializer _serializer = new();

    [Fact]
    public void Serialize_SimpleString_ReturnsCorrectResp() {
        var value = new RespSimpleString("PONG");

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "+PONG\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_Error_ReturnsCorrectResp() {
        var value = new RespError("ERR unknown command");

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "-ERR unknown command\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_Integer_ReturnsCorrectResp() {
        var value = new RespInteger(42);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            ":42\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_NegativeInteger_ReturnsCorrectResp() {
        var value = new RespInteger(-123);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            ":-123\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_ZeroInteger_ReturnsCorrectResp() {
        var value = new RespInteger(0);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            ":0\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_LargeInteger_ReturnsCorrectResp() {
        var value = new RespInteger(long.MaxValue);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            $":{long.MaxValue}\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_NegativeLargeInteger_ReturnsCorrectResp() {
        var value = new RespInteger(long.MinValue);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            $":{long.MinValue}\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_BulkString_ReturnsCorrectResp() {
        var value = new RespBulkString("hello");

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "$5\r\nhello\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_NullBulkString_ReturnsNullResp() {
        var value = new RespBulkString(null);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "$-1\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_EmptyBulkString_ReturnsZeroLengthBulkString() {
        var value = new RespBulkString(string.Empty);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "$0\r\n\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_Utf8BulkString_UsesByteLength() {
        var value = new RespBulkString("é");

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "$2\r\né\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_MultiByteUtf8BulkString_UsesCorrectByteLength() {
        var value = new RespBulkString("こんにちは");

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "$15\r\nこんにちは\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_EmptyArray_ReturnsEmptyArray() {
        var value = new RespArray([]);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*0\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_NullArray_ReturnsNullArray() {
        var value = new RespArray(null);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*-1\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_CommandArray_ReturnsCorrectResp() {
        var value = new RespArray(
        [
            new RespBulkString("GET"),
            new RespBulkString("name")
        ]);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*2\r\n" +
            "$3\r\n" +
            "GET\r\n" +
            "$4\r\n" +
            "name\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_SetCommandArray_ReturnsCorrectResp() {
        var value = new RespArray(
        [
            new RespBulkString("SET"),
            new RespBulkString("name"),
            new RespBulkString("Suraj")
        ]);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*3\r\n" +
            "$3\r\n" +
            "SET\r\n" +
            "$4\r\n" +
            "name\r\n" +
            "$5\r\n" +
            "Suraj\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_ArrayContainingDifferentRespTypes_ReturnsCorrectResp() {
        var value = new RespArray(
        [
            new RespSimpleString("OK"),
            new RespError("ERR test"),
            new RespInteger(42),
            new RespBulkString("hello")
        ]);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*4\r\n" +
            "+OK\r\n" +
            "-ERR test\r\n" +
            ":42\r\n" +
            "$5\r\n" +
            "hello\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_NestedArray_ReturnsCorrectResp() {
        var value = new RespArray(
        [
            new RespInteger(1),
            new RespArray(
            [
                new RespBulkString("hello"),
                new RespBulkString("world")
            ])
        ]);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*2\r\n" +
            ":1\r\n" +
            "*2\r\n" +
            "$5\r\n" +
            "hello\r\n" +
            "$5\r\n" +
            "world\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_DeeplyNestedArray_ReturnsCorrectResp() {
        var value = new RespArray(
        [
            new RespArray(
            [
                new RespArray(
                [
                    new RespBulkString("deep")
                ])
            ])
        ]);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*1\r\n" +
            "*1\r\n" +
            "*1\r\n" +
            "$4\r\n" +
            "deep\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_ArrayContainingNullBulkString_ReturnsCorrectResp() {
        var value = new RespArray(
        [
            new RespBulkString("hello"),
            new RespBulkString(null),
            new RespBulkString("world")
        ]);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*3\r\n" +
            "$5\r\n" +
            "hello\r\n" +
            "$-1\r\n" +
            "$5\r\n" +
            "world\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_ArrayContainingEmptyBulkString_ReturnsCorrectResp() {
        var value = new RespArray(
        [
            new RespBulkString(string.Empty)
        ]);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*1\r\n" +
            "$0\r\n" +
            "\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_ArrayContainingEmptyArray_ReturnsCorrectResp() {
        var value = new RespArray(
        [
            new RespArray([])
        ]);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*1\r\n" +
            "*0\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_ArrayContainingNullArray_ReturnsCorrectResp() {
        var value = new RespArray(
        [
            new RespArray(null)
        ]);

        byte[] result = _serializer.Serialize(value);

        Assert.Equal(
            "*1\r\n" +
            "*-1\r\n",
            Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Serialize_NullValue_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(
            () => _serializer.Serialize(null!));
    }
}