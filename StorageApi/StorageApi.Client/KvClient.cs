using System.Net;
using System.Net.Http.Json;

namespace StorageApi.Client;

public sealed class KvClient
{
    private readonly HttpClient _http;

    public KvClient(HttpClient http) => _http = http;

    public async Task SetAsync(string key, string value, int ttlSeconds)
    {
        var resp = await _http.PutAsJsonAsync($"/kv/{key}", new { value, ttlSeconds });
        resp.EnsureSuccessStatusCode();
    }

    public async Task BatchSetAsync(IEnumerable<BatchUpsertItem> items)
    {
        var payload = new { items };
        var resp = await _http.PostAsJsonAsync("/kv/batch", payload);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<string> GetAsync(string key)
    {
        var resp = await _http.GetAsync($"/kv/{key}");

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        resp.EnsureSuccessStatusCode();
        var dto = await resp.Content.ReadFromJsonAsync<GetValueResponse>();
        return dto.Value;
    }

    public async Task<bool> DeleteAsync(string key)
    {
        var resp = await _http.DeleteAsync($"/kv/{key}");

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return false;

        resp.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<string[]> ListAsync(string prefix)
    {
        var url = string.IsNullOrWhiteSpace(prefix) ? "/kv" : $"/kv?prefix={prefix}";
        var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();

        var dto = await resp.Content.ReadFromJsonAsync<ListKeysResponse>();
        return dto.Keys;
    }

    private sealed class GetValueResponse
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    private sealed class ListKeysResponse
    {
        public string[] Keys { get; set; }
    }
}
