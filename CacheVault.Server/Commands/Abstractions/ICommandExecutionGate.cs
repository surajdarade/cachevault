namespace CacheVault.Server.Commands.Abstractions;

public interface ICommandExecutionGate
{
    ValueTask WaitAsync(
        CancellationToken cancellationToken);

    void Release();
}
