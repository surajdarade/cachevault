using System.Text;
using CacheVault.Persistence.Aof.Reading;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Types;

namespace CacheVault.UnitTests.Persistence.Aof.Reading;

public sealed class AofCommandReaderTests {
    [Fact]
    public void ReadCommand_CompleteCommand_ReturnsCommand() {
        var reader =
            new AofCommandReader(
                new RespParser());

        byte[] data =
            Encoding.UTF8.GetBytes(
                "*2\r\n$4\r\nPING\r\n$4\r\nTEST\r\n");

        using var stream =
            new MemoryStream(data);

        RespArray command =
            reader.ReadCommand(
                stream);

        Assert.NotNull(command);
        Assert.NotNull(command.Values);

        Assert.Equal(
            2,
            command.Values!.Count);

        RespBulkString commandName =
            Assert.IsType<RespBulkString>(
                command.Values[0]);

        RespBulkString argument =
            Assert.IsType<RespBulkString>(
                command.Values[1]);

        Assert.Equal(
            "PING",
            commandName.Value);

        Assert.Equal(
            "TEST",
            argument.Value);
    }

    [Fact]
    public void ReadCommand_CompleteCommandFollowedByEof_ReturnsNullOnSecondRead() {
        var reader =
            new AofCommandReader(
                new RespParser());

        byte[] data =
            Encoding.UTF8.GetBytes(
                "*2\r\n$4\r\nPING\r\n$4\r\nTEST\r\n");

        using var stream =
            new MemoryStream(data);

        RespArray firstCommand =
            reader.ReadCommand(
                stream);

        RespArray? secondCommand =
            reader.ReadCommand(
                stream);

        Assert.NotNull(firstCommand);
        Assert.Null(secondCommand);
    }

    [Fact]
    public void ReadCommand_EmptyStream_ReturnsNull() {
        var reader =
            new AofCommandReader(
                new RespParser());

        using var stream =
            new MemoryStream();

        RespArray? command =
            reader.ReadCommand(
                stream);

        Assert.Null(command);
    }

    [Fact]
    public void ReadCommand_TruncatedCommand_ThrowsInvalidDataException() {
        var reader =
            new AofCommandReader(
                new RespParser());

        byte[] data =
            Encoding.UTF8.GetBytes(
                "*2\r\n$4\r\nPING\r\n$4\r\nTE");

        using var stream =
            new MemoryStream(data);

        Assert.Throws<InvalidDataException>(
            () =>
                reader.ReadCommand(
                    stream));
    }
}