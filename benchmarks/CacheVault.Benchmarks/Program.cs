using BenchmarkDotNet.Running;
using CacheVault.Benchmarks.Load;

namespace CacheVault.Benchmarks;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length > 0 &&
            args[0].Equals(
                "--load",
                StringComparison.OrdinalIgnoreCase))
        {
            await LoadTestRunner.RunAsync(
                args.Skip(1).ToArray());

            return;
        }

        if (args.Length > 0 &&
            args[0].Equals(
                "--help",
                StringComparison.OrdinalIgnoreCase))
        {
            BenchmarkHelp.Print();
            return;
        }

        BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args);
    }
}
