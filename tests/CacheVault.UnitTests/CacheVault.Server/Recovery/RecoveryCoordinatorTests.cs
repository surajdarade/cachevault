using System.Text;
using CacheVault.Core.Abstractions;
using CacheVault.Core.Models;
using CacheVault.Persistence.Aof.Reading;
using CacheVault.Persistence.Rdb.Format;
using CacheVault.Persistence.Rdb.Reading;
using CacheVault.Persistence.Recovery;
using CacheVault.Persistence.Rdb.Writing;
using CacheVault.Protocol.Resp.Parsing;
using CacheVault.Protocol.Resp.Types;
using CacheVault.Server.Commands.Abstractions;
using CacheVault.Server.Commands.Dispatch;
using CacheVault.Server.Commands.Replay;
using CacheVault.Server.Configuration;
using CacheVault.Server.Recovery;

namespace CacheVault.UnitTests.CacheVault.Server.Recovery;

public sealed class RecoveryCoordinatorTests {
    [Fact]
    public async Task RecoverAsync_WithRdbAndAof_ReplaysOnlyCommandsAfterRdbBoundary() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshot =
            new[]
            {
                new PersistentKeyValue(
                    "name",
                    "before")
            };

        var snapshotStore =
            new TestSnapshotStore(
                snapshot);

        var recordingCommand =
            new RecordingCommand(
                "SET");

        var dispatcher =
            CreateDispatcher(
                recordingCommand);

        var replayer =
            CreateReplayer(
                dispatcher);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        string directory =
            CreateTemporaryDirectory();

        try {
            string rdbPath =
                Path.Combine(
                    directory,
                    "cachevault.rdb");

            string aofPath =
                Path.Combine(
                    directory,
                    "cachevault.aof");

            byte[] firstCommand =
                CreateSetCommand(
                    "name",
                    "before");

            byte[] secondCommand =
                CreateSetCommand(
                    "name",
                    "after");

            byte[] aof =
                firstCommand
                    .Concat(secondCommand)
                    .ToArray();

            await File.WriteAllBytesAsync(
                aofPath,
                aof);

            await using (
                FileStream rdbStream =
                    new(
                        rdbPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None)) {
                snapshotWriter.Write(
                    rdbStream,
                    now,
                    firstCommand.Length);
            }

            var options =
                new ServerOptions
                {
                    EnableRdb = true,
                    RdbFilePath = rdbPath,
                    EnableAof = true,
                    AofFilePath = aofPath
                };

            var coordinator =
                CreateCoordinator(
                    replayer,
                    snapshotStore);

            RecoveryResult result =
                await coordinator.RecoverAsync(
                    options,
                    now);

            Assert.Equal(
                1,
                result.RdbRecordsLoaded);

            Assert.Equal(
                1,
                result.AofCommandsReplayed);

            Assert.Equal(
                ["name"],
                recordingCommand.Keys);

            Assert.Equal(
                ["after"],
                recordingCommand.Values);
        }
        finally {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public async Task RecoverAsync_WithRdbBoundaryAtEndOfAof_DoesNotReplayCommands() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshotStore =
            new TestSnapshotStore(
                [
                    new PersistentKeyValue(
                        "name",
                        "value")
                ]);

        var recordingCommand =
            new RecordingCommand(
                "SET");

        var dispatcher =
            CreateDispatcher(
                recordingCommand);

        var replayer =
            CreateReplayer(
                dispatcher);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        string directory =
            CreateTemporaryDirectory();

        try {
            string rdbPath =
                Path.Combine(
                    directory,
                    "cachevault.rdb");

            string aofPath =
                Path.Combine(
                    directory,
                    "cachevault.aof");

            byte[] aof =
                CreateSetCommand(
                    "name",
                    "value");

            await File.WriteAllBytesAsync(
                aofPath,
                aof);

            await using (
                FileStream rdbStream =
                    new(
                        rdbPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None)) {
                snapshotWriter.Write(
                    rdbStream,
                    now,
                    aof.Length);
            }

            var options =
                new ServerOptions
                {
                    EnableRdb = true,
                    RdbFilePath = rdbPath,
                    EnableAof = true,
                    AofFilePath = aofPath
                };

            var coordinator =
                CreateCoordinator(
                    replayer,
                    snapshotStore);

            RecoveryResult result =
                await coordinator.RecoverAsync(
                    options,
                    now);

            Assert.Equal(
                1,
                result.RdbRecordsLoaded);

            Assert.Equal(
                0,
                result.AofCommandsReplayed);

            Assert.Empty(
                recordingCommand.Keys);

            Assert.Empty(
                recordingCommand.Values);
        }
        finally {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public async Task RecoverAsync_WithAofOnly_ReplaysEntireAof() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshotStore =
            new TestSnapshotStore([]);

        var recordingCommand =
            new RecordingCommand(
                "SET");

        var dispatcher =
            CreateDispatcher(
                recordingCommand);

        var replayer =
            CreateReplayer(
                dispatcher);

        string directory =
            CreateTemporaryDirectory();

        try {
            string aofPath =
                Path.Combine(
                    directory,
                    "cachevault.aof");

            byte[] firstCommand =
                CreateSetCommand(
                    "first",
                    "one");

            byte[] secondCommand =
                CreateSetCommand(
                    "second",
                    "two");

            await File.WriteAllBytesAsync(
                aofPath,
                firstCommand
                    .Concat(secondCommand)
                    .ToArray());

            var options =
                new ServerOptions
                {
                    EnableRdb = false,
                    EnableAof = true,
                    AofFilePath = aofPath
                };

            var coordinator =
                CreateCoordinator(
                    replayer,
                    snapshotStore);

            RecoveryResult result =
                await coordinator.RecoverAsync(
                    options,
                    now);

            Assert.Equal(
                0,
                result.RdbRecordsLoaded);

            Assert.Equal(
                2,
                result.AofCommandsReplayed);

            Assert.Equal(
                ["first", "second"],
                recordingCommand.Keys);

            Assert.Equal(
                ["one", "two"],
                recordingCommand.Values);
        }
        finally {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public async Task RecoverAsync_WithRdbOnly_LoadsRdbWithoutAofReplay() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshotStore =
            new TestSnapshotStore(
                [
                    new PersistentKeyValue(
                        "name",
                        "CacheVault")
                ]);

        var recordingCommand =
            new RecordingCommand(
                "SET");

        var dispatcher =
            CreateDispatcher(
                recordingCommand);

        var replayer =
            CreateReplayer(
                dispatcher);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        string directory =
            CreateTemporaryDirectory();

        try {
            string rdbPath =
                Path.Combine(
                    directory,
                    "cachevault.rdb");

            await using (
                FileStream rdbStream =
                    new(
                        rdbPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None)) {
                snapshotWriter.Write(
                    rdbStream,
                    now);
            }

            var options =
                new ServerOptions
                {
                    EnableRdb = true,
                    RdbFilePath = rdbPath,
                    EnableAof = true,
                    AofFilePath =
                        Path.Combine(
                            directory,
                            "missing.aof")
                };

            var coordinator =
                CreateCoordinator(
                    replayer,
                    snapshotStore);

            RecoveryResult result =
                await coordinator.RecoverAsync(
                    options,
                    now);

            Assert.Equal(
                1,
                result.RdbRecordsLoaded);

            Assert.Equal(
                0,
                result.AofCommandsReplayed);

            Assert.Empty(
                recordingCommand.Keys);
        }
        finally {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public async Task RecoverAsync_WithMissingRdb_ReplaysAofFromBeginning() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshotStore =
            new TestSnapshotStore([]);

        var recordingCommand =
            new RecordingCommand(
                "SET");

        var dispatcher =
            CreateDispatcher(
                recordingCommand);

        var replayer =
            CreateReplayer(
                dispatcher);

        string directory =
            CreateTemporaryDirectory();

        try {
            string aofPath =
                Path.Combine(
                    directory,
                    "cachevault.aof");

            byte[] firstCommand =
                CreateSetCommand(
                    "first",
                    "one");

            byte[] secondCommand =
                CreateSetCommand(
                    "second",
                    "two");

            await File.WriteAllBytesAsync(
                aofPath,
                firstCommand
                    .Concat(secondCommand)
                    .ToArray());

            var options =
                new ServerOptions
                {
                    EnableRdb = true,
                    RdbFilePath =
                        Path.Combine(
                            directory,
                            "missing.rdb"),
                    EnableAof = true,
                    AofFilePath = aofPath
                };

            var coordinator =
                CreateCoordinator(
                    replayer,
                    snapshotStore);

            RecoveryResult result =
                await coordinator.RecoverAsync(
                    options,
                    now);

            Assert.Equal(
                0,
                result.RdbRecordsLoaded);

            Assert.Equal(
                2,
                result.AofCommandsReplayed);

            Assert.Equal(
                ["first", "second"],
                recordingCommand.Keys);
        }
        finally {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public async Task RecoverAsync_WithMissingRdbAndAof_ReturnsEmptyRecoveryResult() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshotStore =
            new TestSnapshotStore([]);

        var recordingCommand =
            new RecordingCommand(
                "SET");

        var dispatcher =
            CreateDispatcher(
                recordingCommand);

        var replayer =
            CreateReplayer(
                dispatcher);

        string directory =
            CreateTemporaryDirectory();

        try {
            var options =
                new ServerOptions
                {
                    EnableRdb = true,
                    RdbFilePath =
                        Path.Combine(
                            directory,
                            "missing.rdb"),
                    EnableAof = true,
                    AofFilePath =
                        Path.Combine(
                            directory,
                            "missing.aof")
                };

            var coordinator =
                CreateCoordinator(
                    replayer,
                    snapshotStore);

            RecoveryResult result =
                await coordinator.RecoverAsync(
                    options,
                    now);

            Assert.Equal(
                0,
                result.RdbRecordsLoaded);

            Assert.Equal(
                0,
                result.AofCommandsReplayed);

            Assert.Empty(
                recordingCommand.Keys);
        }
        finally {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    [Fact]
    public async Task RecoverAsync_WithRdbOffsetBeyondAofLength_ThrowsArgumentOutOfRangeException() {
        DateTimeOffset now =
            CreateUtcDateTime();

        var snapshotStore =
            new TestSnapshotStore(
                [
                    new PersistentKeyValue(
                        "name",
                        "value")
                ]);

        var recordingCommand =
            new RecordingCommand(
                "SET");

        var dispatcher =
            CreateDispatcher(
                recordingCommand);

        var replayer =
            CreateReplayer(
                dispatcher);

        var snapshotWriter =
            new RdbSnapshotWriter(
                snapshotStore);

        string directory =
            CreateTemporaryDirectory();

        try {
            string rdbPath =
                Path.Combine(
                    directory,
                    "cachevault.rdb");

            string aofPath =
                Path.Combine(
                    directory,
                    "cachevault.aof");

            byte[] aof =
                CreateSetCommand(
                    "name",
                    "value");

            await File.WriteAllBytesAsync(
                aofPath,
                aof);

            await using (
                FileStream rdbStream =
                    new(
                        rdbPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None)) {
                snapshotWriter.Write(
                    rdbStream,
                    now,
                    aof.Length + 1);
            }

            var options =
                new ServerOptions
                {
                    EnableRdb = true,
                    RdbFilePath = rdbPath,
                    EnableAof = true,
                    AofFilePath = aofPath
                };

            var coordinator =
                CreateCoordinator(
                    replayer,
                    snapshotStore);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                async () =>
                    await coordinator.RecoverAsync(
                        options,
                        now));
        }
        finally {
            DeleteTemporaryDirectory(
                directory);
        }
    }

    private static RecoveryCoordinator CreateCoordinator(
        AofCommandReplayer replayer,
        IKeyValueStoreSnapshot snapshotStore) {
        var rdbSnapshotReader =
            new RdbSnapshotReader();

        var rdbSnapshotLoader =
            new RdbSnapshotLoader(
                new TestKeyValueStore(
                    snapshotStore.GetSnapshot()),
                rdbSnapshotReader);

        return new RecoveryCoordinator(
            rdbSnapshotLoader,
            replayer);
    }

    private static AofCommandReplayer CreateReplayer(
        CommandDispatcher dispatcher) {
        var parser =
            new RespParser();

        var reader =
            new AofCommandReader(
                parser);

        return new AofCommandReplayer(
            reader,
            dispatcher);
    }

    private static CommandDispatcher CreateDispatcher(
        RecordingCommand command) {
        var dispatcher =
            new CommandDispatcher();

        dispatcher.RegisterCommands(
            [
                command
            ]);

        return dispatcher;
    }

    private static byte[] CreateSetCommand(
        string key,
        string value) {
        return Encoding.UTF8.GetBytes(
            "*3\r\n" +
            "$3\r\nSET\r\n" +
            $"${Encoding.UTF8.GetByteCount(key)}\r\n" +
            $"{key}\r\n" +
            $"${Encoding.UTF8.GetByteCount(value)}\r\n" +
            $"{value}\r\n");
    }

    private static string CreateTemporaryDirectory() {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "CacheVault",
                Guid.NewGuid().ToString(
                    "N"));

        Directory.CreateDirectory(
            directory);

        return directory;
    }

    private static void DeleteTemporaryDirectory(
        string directory) {
        if (Directory.Exists(directory)) {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }

    private static DateTimeOffset CreateUtcDateTime() {
        return new DateTimeOffset(
            2026,
            9,
            23,
            10,
            0,
            0,
            TimeSpan.Zero);
    }

    private sealed class RecordingCommand :
        IRedisCommand {
        public RecordingCommand(
            string name) {
            Name = name;
        }

        public string Name { get; }

        public List<string> Keys { get; } = [];

        public List<string> Values { get; } = [];

        public ValueTask<RespValue> ExecuteAsync(
            CommandContext context,
            IReadOnlyList<RespValue> arguments) {
            if (arguments.Count >= 2 &&
                arguments[0] is RespBulkString key &&
                key.Value is not null &&
                arguments[1] is RespBulkString value &&
                value.Value is not null) {
                Keys.Add(
                    key.Value);

                Values.Add(
                    value.Value);
            }

            return ValueTask.FromResult<RespValue>(
                new RespSimpleString("OK"));
        }
    }

    private sealed class TestKeyValueStore :
        IKeyValueStore,
        IKeyValueStoreSnapshot {
        private readonly List<PersistentKeyValue> _snapshot;

        public TestKeyValueStore(
            IReadOnlyList<PersistentKeyValue> snapshot) {
            _snapshot =
                snapshot.ToList();
        }

        public IReadOnlyList<PersistentKeyValue> GetSnapshot() {
            return _snapshot;
        }

        public bool TryGet(
            string key,
            out StoredValue? value) {
            value = null;

            return false;
        }

        public void Set(
            string key,
            string value,
            DateTimeOffset? expiresAt = null) {
            int index =
                _snapshot.FindIndex(
                    entry =>
                        entry.Key.Equals(
                            key,
                            StringComparison.Ordinal));

            var persistentValue =
                new PersistentKeyValue(
                    key,
                    value,
                    expiresAt);

            if (index >= 0) {
                _snapshot[index] =
                    persistentValue;
            }
            else {
                _snapshot.Add(
                    persistentValue);
            }
        }

        public bool Remove(
            string key) {
            int removed =
                _snapshot.RemoveAll(
                    entry =>
                        entry.Key.Equals(
                            key,
                            StringComparison.Ordinal));

            return removed > 0;
        }

        public bool Contains(
            string key) {
            return _snapshot.Any(
                entry =>
                    entry.Key.Equals(
                        key,
                        StringComparison.Ordinal));
        }

        public long Increment(
            string key,
            long amount = 1) {
            throw new NotSupportedException();
        }

        public long GetVersion(
            string key) {
            return 0;
        }
    }

    private sealed class TestSnapshotStore :
        IKeyValueStoreSnapshot {
        private readonly IReadOnlyList<PersistentKeyValue> _snapshot;

        public TestSnapshotStore(
            IReadOnlyList<PersistentKeyValue> snapshot) {
            _snapshot =
                snapshot;
        }

        public IReadOnlyList<PersistentKeyValue> GetSnapshot() {
            return _snapshot;
        }
    }
}