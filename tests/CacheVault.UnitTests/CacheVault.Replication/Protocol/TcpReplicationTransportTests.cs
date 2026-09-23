using System.Net;
using System.Net.Sockets;
using System.Text;
using CacheVault.Replication.Protocol;

namespace CacheVault.UnitTests.Replication.Protocol;

public sealed class TcpReplicationTransportTests {
    [Fact]
    public async Task WriteAsync_ShouldWriteBytesToNetworkStream() {
        using var listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        listener.Start();

        int port =
            ((IPEndPoint)listener.LocalEndpoint)
            .Port;

        using var client =
            new TcpClient();

        Task connectTask =
            client.ConnectAsync(
                IPAddress.Loopback,
                port);

        using TcpClient serverClient =
            await listener.AcceptTcpClientAsync();

        await connectTask;

        await using var transport =
            new TcpReplicationTransport(
                client.GetStream());

        byte[] payload =
            Encoding.UTF8.GetBytes(
                "PING");

        await transport.WriteAsync(
            payload);

        byte[] buffer =
            new byte[payload.Length];

        int bytesRead =
            await serverClient.GetStream()
                .ReadAsync(buffer);

        Assert.Equal(
            payload.Length,
            bytesRead);

        Assert.Equal(
            payload,
            buffer);

        listener.Stop();
    }

    [Fact]
    public async Task ReadAsync_ShouldReadBytesFromNetworkStream() {
        using var listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        listener.Start();

        int port =
            ((IPEndPoint)listener.LocalEndpoint)
            .Port;

        using var client =
            new TcpClient();

        Task connectTask =
            client.ConnectAsync(
                IPAddress.Loopback,
                port);

        using TcpClient serverClient =
            await listener.AcceptTcpClientAsync();

        await connectTask;

        await using var transport =
            new TcpReplicationTransport(
                serverClient.GetStream());

        byte[] payload =
            Encoding.UTF8.GetBytes(
                "REPLCONF ACK 100");

        await client.GetStream()
            .WriteAsync(payload);

        byte[] buffer =
            new byte[payload.Length];

        int bytesRead =
            await transport.ReadAsync(
                buffer);

        Assert.Equal(
            payload.Length,
            bytesRead);

        Assert.Equal(
            payload,
            buffer);

        listener.Stop();
    }

    [Fact]
    public async Task WriteAsync_ShouldSupportEmptyPayload() {
        using var listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        listener.Start();

        int port =
            ((IPEndPoint)listener.LocalEndpoint)
            .Port;

        using var client =
            new TcpClient();

        Task connectTask =
            client.ConnectAsync(
                IPAddress.Loopback,
                port);

        using TcpClient serverClient =
            await listener.AcceptTcpClientAsync();

        await connectTask;

        await using var transport =
            new TcpReplicationTransport(
                client.GetStream());

        await transport.WriteAsync(
            ReadOnlyMemory<byte>.Empty);

        listener.Stop();
    }
}