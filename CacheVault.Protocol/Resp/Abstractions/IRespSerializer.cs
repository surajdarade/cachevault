using CacheVault.Protocol.Resp.Types;

namespace CacheVault.Protocol.Resp.Abstractions;

public interface IRespSerializer {
    byte[] Serialize(RespValue value);
}