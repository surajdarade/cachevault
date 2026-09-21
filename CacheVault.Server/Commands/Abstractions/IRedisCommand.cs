using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Server.Commands.Abstractions;

public interface IRedisCommand {
    string Name { get; }

    ValueTask<RespValue> ExecuteAsync(CommandContext context, IReadOnlyList<RespValue> arguments);
}