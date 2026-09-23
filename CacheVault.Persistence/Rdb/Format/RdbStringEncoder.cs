using System.Text;

namespace CacheVault.Persistence.Rdb.Format;

public static class RdbStringEncoder {
    public static void WriteString(
        RdbBinaryWriter writer,
        string value) {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        byte[] bytes =
            Encoding.UTF8.GetBytes(
                value);

        RdbLengthEncoder.WriteLength(
            writer,
            checked((uint)bytes.Length));

        writer.WriteBytes(
            bytes);
    }
}