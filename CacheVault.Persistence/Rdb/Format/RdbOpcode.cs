namespace CacheVault.Persistence.Rdb.Format;

public enum RdbOpcode : byte {
    StringValue = 0x00,
    ListValue = 0x02,

    ExpireMilliseconds = 0xFC,

    ExpireSeconds = 0xFD,

    EndOfFile = 0xFF
}