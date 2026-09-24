using CacheVault.Benchmarks;

namespace CacheVault.Benchmarks.Load;

public enum LoadWorkload
{
    Get,
    Set,
    Mixed,
    Pipeline,
    Transaction,
    List
}

public class LoadOptions
{
    public int DurationSeconds { get; private set; } = 30;

    public int Clients { get; private set; } = 16;

    public LoadWorkload Workload { get; private set; } =
        LoadWorkload.Mixed;

    public int PipelineDepth { get; private set; } = 100;

    public int SampleRate { get; private set; } = 100;

    public bool EnableAof { get; private set; }

    public bool EnableRdb { get; private set; }

    public static LoadOptions Parse(string[] args)
    {
        var options = new LoadOptions();

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];

            switch (argument)
            {
                case "--duration":
                    options.DurationSeconds =
                        ParsePositiveInt(args, ref index, argument);
                    break;

                case "--clients":
                    options.Clients =
                        ParsePositiveInt(args, ref index, argument);
                    break;

                case "--workload":
                    options.Workload =
                        ParseWorkload(
                            GetValue(args, ref index, argument));
                    break;

                case "--pipeline":
                    options.PipelineDepth =
                        ParsePositiveInt(args, ref index, argument);
                    break;

                case "--sample-rate":
                    options.SampleRate =
                        ParsePositiveInt(args, ref index, argument);
                    break;

                case "--aof":
                    options.EnableAof = true;
                    break;

                case "--rdb":
                    options.EnableRdb = true;
                    break;

                case "--help":
                    BenchmarkHelp.Print();
                    return options;

                default:
                    throw new ArgumentException(
                        $"Unknown load option '{argument}'.");
            }
        }

        return options;
    }

    private static string GetValue(
        string[] args,
        ref int index,
        string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException(
                $"Missing value for '{option}'.");
        }

        index++;
        return args[index];
    }

    private static int ParsePositiveInt(
        string[] args,
        ref int index,
        string option)
    {
        string value = GetValue(args, ref index, option);

        if (!int.TryParse(value, out int result) || result <= 0)
        {
            throw new ArgumentException(
                $"'{option}' must be a positive integer.");
        }

        return result;
    }

    private static LoadWorkload ParseWorkload(string value) =>
        value.ToLowerInvariant() switch
        {
            "get" => LoadWorkload.Get,
            "set" => LoadWorkload.Set,
            "mixed" => LoadWorkload.Mixed,
            "pipeline" => LoadWorkload.Pipeline,
            "transaction" => LoadWorkload.Transaction,
            "list" => LoadWorkload.List,
            _ => throw new ArgumentException(
                $"Unknown workload '{value}'.")
        };
}
