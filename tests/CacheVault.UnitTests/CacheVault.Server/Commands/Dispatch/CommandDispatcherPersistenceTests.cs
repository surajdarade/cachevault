using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Networking.Connections;

namespace CacheVault.UnitTests.Server.Commands.Dispatch;

public sealed class CommandDispatcherPersistenceTests {
    [Fact]
    public async Task DispatchAsync_Set_PersistsCommand() {
        var persistence =
            new RecordingCommandPersistence();

        var dispatcher =
            CreateDispatcher(
                persistence,
                new RecordingCommand("SET"));

        CommandContext context =
            CreateContext();

        RespValue result =
            await dispatcher.DispatchAsync(
                context,
                CreateRequest(
                    "SET",
                    "key",
                    "value"));

        Assert.Equal(
            new RespSimpleString("OK"),
            result);

        Assert.Single(
            persistence.Commands);

        Assert.Equal(
            "SET",
            persistence.Commands[0].CommandName);

        Assert.Equal(
            ["key", "value"],
            persistence.Commands[0].Arguments);
    }

    [Fact]
    public async Task DispatchAsync_Del_PersistsCommand() {
        var persistence =
            new RecordingCommandPersistence();

        var dispatcher =
            CreateDispatcher(
                persistence,
                new RecordingCommand("DEL"));

        CommandContext context =
            CreateContext();

        await dispatcher.DispatchAsync(
            context,
            CreateRequest(
                "DEL",
                "key"));

        Assert.Single(
            persistence.Commands);

        Assert.Equal(
            "DEL",
            persistence.Commands[0].CommandName);

        Assert.Equal(
            ["key"],
            persistence.Commands[0].Arguments);
    }

    [Fact]
    public async Task DispatchAsync_Incr_PersistsCommand() {
        var persistence =
            new RecordingCommandPersistence();

        var dispatcher =
            CreateDispatcher(
                persistence,
                new RecordingCommand("INCR"));

        CommandContext context =
            CreateContext();

        await dispatcher.DispatchAsync(
            context,
            CreateRequest(
                "INCR",
                "counter"));

        Assert.Single(
            persistence.Commands);

        Assert.Equal(
            "INCR",
            persistence.Commands[0].CommandName);

        Assert.Equal(
            ["counter"],
            persistence.Commands[0].Arguments);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("TTL")]
    [InlineData("PTTL")]
    [InlineData("PING")]
    [InlineData("ECHO")]
    [InlineData("MULTI")]
    [InlineData("EXEC")]
    [InlineData("WATCH")]
    [InlineData("UNWATCH")]
    [InlineData("DISCARD")]
    public async Task DispatchAsync_NonPersistentCommand_DoesNotPersist(
        string commandName) {
        var persistence =
            new RecordingCommandPersistence();

        var dispatcher =
            CreateDispatcher(
                persistence,
                new RecordingCommand(commandName));

        CommandContext context =
            CreateContext();

        await dispatcher.DispatchAsync(
            context,
            CreateRequest(
                commandName,
                "argument"));

        Assert.Empty(
            persistence.Commands);
    }

    [Fact]
    public async Task DispatchAsync_FailedPersistentCommand_DoesNotPersist() {
        var persistence =
            new RecordingCommandPersistence();

        var dispatcher =
            CreateDispatcher(
                persistence,
                new RecordingCommand(
                    "SET",
                    shouldFail: true));

        CommandContext context =
            CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await dispatcher.DispatchAsync(
                    context,
                    CreateRequest(
                        "SET",
                        "key",
                        "value")));

        Assert.Empty(
            persistence.Commands);
    }

    [Fact]
    public async Task ExecuteQueuedCommandAsync_PersistentCommand_PersistsAfterExecution() {
        var persistence =
            new RecordingCommandPersistence();

        var command =
            new RecordingCommand("SET");

        var dispatcher =
            CreateDispatcher(
                persistence,
                command);

        CommandContext context =
            CreateContext();

        var queuedCommand =
            new QueuedCommand(
                "SET",
                [
                    new RespBulkString("key"),
                    new RespBulkString("value")
                ]);

        await dispatcher.ExecuteQueuedCommandAsync(
            context,
            queuedCommand);

        Assert.True(
            command.WasExecuted);

        Assert.Single(
            persistence.Commands);

        Assert.Equal(
            "SET",
            persistence.Commands[0].CommandName);

        Assert.Equal(
            ["key", "value"],
            persistence.Commands[0].Arguments);
    }

    [Fact]
    public async Task ExecuteQueuedCommandAsync_FailedPersistentCommand_DoesNotPersist() {
        var persistence =
            new RecordingCommandPersistence();

        var dispatcher =
            CreateDispatcher(
                persistence,
                new RecordingCommand(
                    "SET",
                    shouldFail: true));

        CommandContext context =
            CreateContext();

        var queuedCommand =
            new QueuedCommand(
                "SET",
                [
                    new RespBulkString("key"),
                    new RespBulkString("value")
                ]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await dispatcher.ExecuteQueuedCommandAsync(
                    context,
                    queuedCommand));

        Assert.Empty(
            persistence.Commands);
    }

    [Fact]
    public async Task DispatchAsync_PersistenceFailure_IsPropagated() {
        var persistence =
            new RecordingCommandPersistence(
                shouldFail: true);

        var command =
            new RecordingCommand("SET");

        var dispatcher =
            CreateDispatcher(
                persistence,
                command);

        CommandContext context =
            CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await dispatcher.DispatchAsync(
                    context,
                    CreateRequest(
                        "SET",
                        "key",
                        "value")));

        Assert.True(
            command.WasExecuted);

        Assert.Single(
            persistence.Commands);
    }

    [Fact]
    public async Task DispatchAsync_ReplayContext_DoesNotPersist() {
        var persistence =
            new RecordingCommandPersistence();

        var dispatcher =
            CreateDispatcher(
                persistence,
                new RecordingCommand("SET"));

        CommandContext context =
            new(
                new ClientSession(),
                CancellationToken.None,
                isReplay: true);

        await dispatcher.DispatchAsync(
            context,
            CreateRequest(
                "SET",
                "key",
                "value"));

        Assert.Empty(
            persistence.Commands);
    }

    [Fact]
    public async Task ExecuteQueuedCommandAsync_ReplayContext_DoesNotPersist() {
        var persistence =
            new RecordingCommandPersistence();

        var dispatcher =
            CreateDispatcher(
                persistence,
                new RecordingCommand("SET"));

        CommandContext context =
            new(
                new ClientSession(),
                CancellationToken.None,
                isReplay: true);

        var queuedCommand =
            new QueuedCommand(
                "SET",
                [
                    new RespBulkString("key"),
                new RespBulkString("value")
                ]);

        await dispatcher.ExecuteQueuedCommandAsync(
            context,
            queuedCommand);

        Assert.Empty(
            persistence.Commands);
    }

    private static CommandDispatcher CreateDispatcher(
        ICommandPersistence persistence,
        params IRedisCommand[] commands) {
        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            commands);

        dispatcher.RegisterPersistence(
            persistence);

        return dispatcher;
    }

    private static CommandContext CreateContext() {
        return new CommandContext(
            new ClientSession(),
            CancellationToken.None);
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

        return new RespArray(values);
    }

    private sealed class RecordingCommand : IRedisCommand {
        private readonly bool _shouldFail;

        public RecordingCommand(
            string name,
            bool shouldFail = false) {
            Name = name;
            _shouldFail = shouldFail;
        }

        public string Name { get; }

        public bool WasExecuted { get; private set; }

        public ValueTask<RespValue> ExecuteAsync(
            CommandContext context,
            IReadOnlyList<RespValue> arguments) {
            WasExecuted = true;

            if (_shouldFail) {
                throw new InvalidOperationException(
                    "Command execution failed.");
            }

            return ValueTask.FromResult<RespValue>(
                new RespSimpleString("OK"));
        }
    }

    private sealed class RecordingCommandPersistence
        : ICommandPersistence {
        private readonly bool _shouldFail;

        public RecordingCommandPersistence(
            bool shouldFail = false) {
            _shouldFail = shouldFail;
        }

        public List<PersistedCommand> Commands { get; } = [];

        public ValueTask PersistAsync(
            string commandName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            Commands.Add(
                new PersistedCommand(
                    commandName,
                    arguments.ToArray()));

            if (_shouldFail) {
                throw new InvalidOperationException(
                    "Persistence failed.");
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed record PersistedCommand(
        string CommandName,
        IReadOnlyList<string> Arguments);
}