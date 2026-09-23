namespace CacheVault.Persistence.Rdb.Format;

public enum RdbOpcode : byte {
    StringValue = 0x00,

    ExpireMilliseconds = 0xFC,

    ExpireSeconds = 0xFD,

    EndOfFile = 0xFF
}