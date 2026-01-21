namespace StorageApi.Server.Models;

public sealed class SetValueRequest
{
    public string Value { get; set; } = string.Empty;
    public int TtlSeconds { get; set; }
}
