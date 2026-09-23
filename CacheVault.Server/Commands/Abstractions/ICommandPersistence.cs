namespace CacheVault.Server.Commands.Abstractions;

public interface ICommandPersistence {
    ValueTask PersistAsync(
        string commandName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}