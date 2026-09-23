using System.Text;

namespace CacheVault.Persistence.Rdb.Format;

public static class RdbHeaderWriter {
    public static void WriteHeader(
        RdbBinaryWriter writer,
        RdbHeader header) {
        ArgumentNullException.ThrowIfNull(
            writer);

        ArgumentNullException.ThrowIfNull(
            header);

        if (header.AofOffset < 0) {
            throw new ArgumentOutOfRangeException(
                nameof(header),
                "AOF offset cannot be negative.");
        }

        byte[] magic =
            Encoding.ASCII.GetBytes(
                RdbHeader.Magic);

        writer.WriteBytes(
            magic);

        writer.WriteUInt16(
            header.Version);

        writer.WriteInt64(
            header.AofOffset);
    }
}