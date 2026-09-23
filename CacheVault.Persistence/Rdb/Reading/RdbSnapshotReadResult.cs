using CacheVault.Persistence.Rdb.Format;

namespace CacheVault.Persistence.Rdb.Reading;

public sealed record RdbSnapshotReadResult(
    RdbHeader Header,
    IReadOnlyList<RdbRecord> Records);