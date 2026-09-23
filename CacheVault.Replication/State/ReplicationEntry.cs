namespace CacheVault.Replication.State;

public sealed record ReplicationEntry {
    public ReplicationEntry(
        long startOffset,
        long endOffset,
        byte[] data) {
        ArgumentNullException.ThrowIfNull(data);

        if (startOffset < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(startOffset));
        }

        if (endOffset <= startOffset) {
            throw new ArgumentOutOfRangeException(
                nameof(endOffset),
                "End offset must be greater than start offset.");
        }

        long expectedLength =
            endOffset - startOffset;

        if (expectedLength != data.Length) {
            throw new ArgumentException(
                "Replication data length must match the offset range.",
                nameof(data));
        }

        StartOffset = startOffset;
        EndOffset = endOffset;
        Data = data;
    }

    public long StartOffset { get; }

    public long EndOffset { get; }

    public byte[] Data { get; }

    public long Length =>
        EndOffset - StartOffset;
}