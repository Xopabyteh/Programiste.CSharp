namespace StorageApi.Server.Models;

public sealed class BatchUpsertRequest
{
    public List<BatchUpsertItem> Items { get; set; } = new();
}
