namespace CacheVault.Benchmarks.Infrastructure;

public sealed class BenchmarkServerErrorException(string message)
    : Exception(message);

public sealed class BenchmarkProtocolException(string message)
    : Exception(message);
