using CacheVault.Server.Commands.Abstractions;

namespace CacheVault.Server.Commands.Dispatch;

public sealed class CommandExecutionGate :
    ICommandExecutionGate,
    IAsyncDisposable {
    private readonly SemaphoreSlim _semaphore =
        new(1, 1);

    public ValueTask WaitAsync(
        CancellationToken cancellationToken) {
        return new ValueTask(
            _semaphore.WaitAsync(
                cancellationToken));
    }

    public void Release() {
        _semaphore.Release();
    }

    public ValueTask DisposeAsync() {
        _semaphore.Dispose();

        return ValueTask.CompletedTask;
    }
}