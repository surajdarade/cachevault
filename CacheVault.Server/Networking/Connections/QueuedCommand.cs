using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Server.Networking.Connections;

public sealed record QueuedCommand(
    string Name,
    IReadOnlyList<RespValue> Arguments);