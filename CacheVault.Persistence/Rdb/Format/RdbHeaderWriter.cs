using System.Text;

namespace CacheVault.Persistence.Rdb.Format;

public static class RdbHeaderWriter {
    public static void WriteHeader(
        RdbBinaryWriter writer,
        RdbHeader header) {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(header);

        byte[] magic =
            Encoding.ASCII.GetBytes(
                RdbHeader.Magic);

        writer.WriteBytes(
            magic);

        writer.WriteUInt16(
            header.Version);
    }
}