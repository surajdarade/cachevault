using CacheVault.Protocol.Resp.Types;
using CacheVault.Replication.Abstractions;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.CacheVault.Server.Commands.Dispatch;

public sealed class CommandDispatcherReplicationTests {
    [Fact]
    public async Task DispatchAsync_Set_ShouldReplicateSuccessfulMutation() {
        var replication =
            new TestReplicationManager();

        var command =
            new TestRedisCommand(
                "SET",
                new RespSimpleString("OK"));

        CommandDispatcher dispatcher =
            CreateDispatcher(
                replication,
                command);

        CommandContext context =
            CreateContext();

        RespValue response =
            await dispatcher.DispatchAsync(
                context,
                CreateRequest(
                    "SET",
                    "name",
                    "CacheVault"));

        var result =
            Assert.IsType<RespSimpleString>(
                response);

        Assert.Equal(
            "OK",
            result.Value);

        Assert.Single(
            replication.Calls);

        Assert.Equal(
            "SET",
            replication.Calls[0].CommandName);

        Assert.Equal(
            ["name", "CacheVault"],
            replication.Calls[0].Arguments);
    }

    [Fact]
    public async Task DispatchAsync_Del_ShouldReplicateSuccessfulMutation() {
        var replication =
            new TestReplicationManager();

        var command =
            new TestRedisCommand(
                "DEL",
                new RespInteger(1));

        CommandDispatcher dispatcher =
            CreateDispatcher(
                replication,
                command);

        CommandContext context =
            CreateContext();

        await dispatcher.DispatchAsync(
            context,
            CreateRequest(
                "DEL",
                "name"));

        Assert.Single(
            replication.Calls);

        Assert.Equal(
            "DEL",
            replication.Calls[0].CommandName);

        Assert.Equal(
            ["name"],
            replication.Calls[0].Arguments);
    }

    [Fact]
    public async Task DispatchAsync_Incr_ShouldReplicateSuccessfulMutation() {
        var replication =
            new TestReplicationManager();

        var command =
            new TestRedisCommand(
                "INCR",
                new RespInteger(1));

        CommandDispatcher dispatcher =
            CreateDispatcher(
                replication,
                command);

        CommandContext context =
            CreateContext();

        await dispatcher.DispatchAsync(
            context,
            CreateRequest(
                "INCR",
                "counter"));

        Assert.Single(
            replication.Calls);

        Assert.Equal(
            "INCR",
            replication.Calls[0].CommandName);

        Assert.Equal(
            ["counter"],
            replication.Calls[0].Arguments);
    }

    [Fact]
    public async Task DispatchAsync_Get_ShouldNotReplicate() {
        var replication =
            new TestReplicationManager();

        var command =
            new TestRedisCommand(
                "GET",
                new RespBulkString("value"));

        CommandDispatcher dispatcher =
            CreateDispatcher(
                replication,
                command);

        CommandContext context =
            CreateContext();

        await dispatcher.DispatchAsync(
            context,
            CreateRequest(
                "GET",
                "name"));

        Assert.Empty(
            replication.Calls);
    }

    [Fact]
    public async Task DispatchAsync_CommandExecutionFails_ShouldNotReplicate() {
        var replication =
            new TestReplicationManager();

        var command =
            new TestRedisCommand(
                "SET",
                new InvalidOperationException(
                    "Command execution failed."));

        CommandDispatcher dispatcher =
            CreateDispatcher(
                replication,
                command);

        CommandContext context =
            CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await dispatcher.DispatchAsync(
                    context,
                    CreateRequest(
                        "SET",
                        "name",
                        "value")));

        Assert.Empty(
            replication.Calls);
    }

    [Fact]
    public async Task DispatchAsync_Replay_ShouldNotReplicate() {
        var replication =
            new TestReplicationManager();

        var command =
            new TestRedisCommand(
                "SET",
                new RespSimpleString("OK"));

        CommandDispatcher dispatcher =
            CreateDispatcher(
                replication,
                command);

        CommandContext context =
            CreateContext(
                isReplay: true);

        await dispatcher.DispatchAsync(
            context,
            CreateRequest(
                "SET",
                "name",
                "value"));

        Assert.Empty(
            replication.Calls);
    }

    [Fact]
    public async Task ExecuteQueuedCommandAsync_Set_ShouldReplicateAfterExecution() {
        var replication =
            new TestReplicationManager();

        var command =
            new TestRedisCommand(
                "SET",
                new RespSimpleString("OK"));

        CommandDispatcher dispatcher =
            CreateDispatcher(
                replication,
                command);

        CommandContext context =
            CreateContext();

        var queuedCommand =
            new QueuedCommand(
                "SET",
                [
                    new RespBulkString("name"),
                    new RespBulkString("CacheVault")
                ]);

        RespValue response =
            await dispatcher.ExecuteQueuedCommandAsync(
                context,
                queuedCommand);

        var result =
            Assert.IsType<RespSimpleString>(
                response);

        Assert.Equal(
            "OK",
            result.Value);

        Assert.Single(
            replication.Calls);

        Assert.Equal(
            "SET",
            replication.Calls[0].CommandName);

        Assert.Equal(
            ["name", "CacheVault"],
            replication.Calls[0].Arguments);
    }

    [Fact]
    public async Task ExecuteQueuedCommandAsync_Replay_ShouldNotReplicate() {
        var replication =
            new TestReplicationManager();

        var command =
            new TestRedisCommand(
                "SET",
                new RespSimpleString("OK"));

        CommandDispatcher dispatcher =
            CreateDispatcher(
                replication,
                command);

        CommandContext context =
            CreateContext(
                isReplay: true);

        var queuedCommand =
            new QueuedCommand(
                "SET",
                [
                    new RespBulkString("name"),
                    new RespBulkString("CacheVault")
                ]);

        await dispatcher.ExecuteQueuedCommandAsync(
            context,
            queuedCommand);

        Assert.Empty(
            replication.Calls);
    }

    [Fact]
    public async Task DispatchAsync_CommandWithNullBulkArgument_ShouldNotReplicate() {
        var replication =
            new TestReplicationManager();

        var command =
            new TestRedisCommand(
                "SET",
                new RespSimpleString("OK"));

        CommandDispatcher dispatcher =
            CreateDispatcher(
                replication,
                command);

        CommandContext context =
            CreateContext();

        await Assert.ThrowsAsync<CommandArgumentException>(
            async () =>
                await dispatcher.DispatchAsync(
                    context,
                    new RespArray(
                    [
                        new RespBulkString("SET"),
                        new RespBulkString(null)
                    ])));

        Assert.Empty(
            replication.Calls);
    }

    [Fact]
    public void RegisterReplication_WithNullManager_ShouldThrow() {
        var dispatcher =
            new CommandDispatcher();

        Assert.Throws<ArgumentNullException>(
            () =>
                dispatcher.RegisterReplication(
                    null!));
    }

    [Fact]
    public void RegisterReplication_Twice_ShouldThrow() {
        var dispatcher =
            new CommandDispatcher();

        var first =
            new TestReplicationManager();

        var second =
            new TestReplicationManager();

        dispatcher.RegisterReplication(
            first);

        Assert.Throws<InvalidOperationException>(
            () =>
                dispatcher.RegisterReplication(
                    second));
    }

    [Fact]
    public async Task DispatchAsync_WithoutReplicationRegistered_ShouldStillExecute() {
        var dispatcher =
            new CommandDispatcher();

        var command =
            new TestRedisCommand(
                "SET",
                new RespSimpleString("OK"));

        dispatcher.RegisterCommands(
            [command]);

        CommandContext context =
            CreateContext();

        RespValue response =
            await dispatcher.DispatchAsync(
                context,
                CreateRequest(
                    "SET",
                    "name",
                    "value"));

        var result =
            Assert.IsType<RespSimpleString>(
                response);

        Assert.Equal(
            "OK",
            result.Value);
    }

    [Fact]
    public async Task DispatchAsync_ReplicationFailure_ShouldPropagateException() {
        var replication =
            new FailingReplicationManager();

        var command =
            new TestRedisCommand(
                "SET",
                new RespSimpleString("OK"));

        CommandDispatcher dispatcher =
            CreateDispatcher(
                replication,
                command);

        CommandContext context =
            CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await dispatcher.DispatchAsync(
                    context,
                    CreateRequest(
                        "SET",
                        "name",
                        "value")));
    }

    private static CommandDispatcher CreateDispatcher(
        IReplicationManager replication,
        params IRedisCommand[] commands) {
        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            commands);

        dispatcher.RegisterReplication(
            replication);

        return dispatcher;
    }

    private static CommandContext CreateContext(
        bool isReplay = false) {
        return new CommandContext(
            new ClientSession(),
            CancellationToken.None,
            isReplay);
    }

    private static RespArray CreateRequest(
        string commandName,
        params string[] arguments) {
        var values =
            new List<RespValue>
            {
                new RespBulkString(commandName)
            };

        foreach (string argument in arguments) {
            values.Add(
                new RespBulkString(argument));
        }

        return new RespArray(
            values);
    }

    private sealed class TestRedisCommand :
        IRedisCommand {
        private readonly RespValue? _response;
        private readonly Exception? _exception;

        public TestRedisCommand(
            string name,
            RespValue response) {
            Name = name;
            _response = response;
        }

        public TestRedisCommand(
            string name,
            Exception exception) {
            Name = name;
            _exception = exception;
        }

        public string Name { get; }

        public ValueTask<RespValue> ExecuteAsync(
            CommandContext context,
            IReadOnlyList<RespValue> arguments) {
            if (_exception is not null) {
                throw _exception;
            }

            return ValueTask.FromResult(
                _response!);
        }
    }

    private sealed class TestReplicationManager :
        IReplicationManager {
        public List<ReplicationCall> Calls { get; } =
            [];

        public long ReplicationOffset { get; private set; }

        public ValueTask ReplicateAsync(
            string commandName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            Calls.Add(
                new ReplicationCall(
                    commandName,
                    arguments.ToArray()));

            ReplicationOffset++;

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingReplicationManager :
        IReplicationManager {
        public long ReplicationOffset =>
            0;

        public ValueTask ReplicateAsync(
            string commandName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default) {
            throw new InvalidOperationException(
                "Replication failed.");
        }

        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }
    }

    private sealed record ReplicationCall(
        string CommandName,
        IReadOnlyList<string> Arguments);
}