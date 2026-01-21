namespace StorageApi.Client;

public sealed class BatchUpsertItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int TtlSeconds { get; set; }
}
