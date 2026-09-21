namespace CacheVault.Server.Commands.Abstractions;

public sealed class CommandArgumentException : Exception {
    public CommandArgumentException(string message)
        : base(message) {
    }
}