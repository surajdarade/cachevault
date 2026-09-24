namespace CacheVault.Server.Configuration;

public static class CommandLineOptions {
    public static void Apply(
        ServerOptions options,
        string[] args) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(args);

        for (int index = 0; index < args.Length; index++) {
            string argument =
                args[index];

            switch (argument) {
                case "--host":
                    options.Host =
                        GetValue(
                            args,
                            ref index,
                            argument);
                    break;

                case "--port":
                    options.Port =
                        ParsePort(
                            GetValue(
                                args,
                                ref index,
                                argument));
                    break;

                case "--replica":
                case "--slave":
                    options.IsReplica =
                        true;
                    break;

                case "--master-host":
                    options.MasterHost =
                        GetValue(
                            args,
                            ref index,
                            argument);
                    break;

                case "--master-port":
                    options.MasterPort =
                        ParsePort(
                            GetValue(
                                args,
                                ref index,
                                argument));
                    break;

                case "--no-rdb":
                    options.EnableRdb =
                        false;
                    break;

                case "--rdb-path":
                    options.RdbFilePath =
                        GetValue(
                            args,
                            ref index,
                            argument);
                    break;

                case "--no-aof":
                    options.EnableAof =
                        false;
                    break;

                case "--aof-path":
                    options.AofFilePath =
                        GetValue(
                            args,
                            ref index,
                            argument);
                    break;

                case "--maxmemory":
                case "--maxmemory-bytes":
                    options.MaxMemoryBytes =
                        ParseMaxMemory(
                            GetValue(
                                args,
                                ref index,
                                argument));
                    break;

                case "--eviction-policy":
                    options.EvictionPolicy =
                        EvictionPolicyParser.Parse(
                            GetValue(
                                args,
                                ref index,
                                argument));
                    break;

                default:
                    throw new ArgumentException(
                        $"Unknown command-line argument '{argument}'.",
                        nameof(args));
            }
        }
    }

    private static string GetValue(
        string[] args,
        ref int index,
        string option) {
        if (index + 1 >= args.Length) {
            throw new ArgumentException(
                $"Missing value for '{option}'.",
                nameof(args));
        }

        index++;

        return args[index];
    }

    private static int ParsePort(
        string value) {
        if (!int.TryParse(
                value,
                out int port) ||
            port is < 1 or > 65535) {
            throw new ArgumentException(
                "Port must be between 1 and 65535.",
                nameof(value));
        }

        return port;
    }

    private static long ParseMaxMemory(
        string value) {
        if (!long.TryParse(
                value,
                out long bytes) ||
            bytes < 0) {
            throw new ArgumentException(
                "Max memory must be a non-negative number of bytes.",
                nameof(value));
        }

        return bytes;
    }
}
