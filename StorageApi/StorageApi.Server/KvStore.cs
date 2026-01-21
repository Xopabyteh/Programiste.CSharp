using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace StorageApi.Server;

public interface IKvStore
{
    bool TryGet(string key, out string value);
    UpsertResult Upsert(string key, string value, int ttlSeconds);
    bool TryRemove(string key);
    string[] ListKeys(string prefix);
}

public enum UpsertResult
{
    Created,
    Updated
}

public sealed class InMemoryKvStore : IKvStore
{
    private sealed record Entry(string Value, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _data = new();

    public bool TryGet(string key, out string value)
    {
        value = null;

        if (!_data.TryGetValue(key, out var entry))
            return false;

        // Lazily remove when expired
        if (IsExpired(entry))
        {
            _data.TryRemove(key, out _);
            return false;
        }

        value = entry.Value;
        return true;
    }

    public UpsertResult Upsert(string key, string value, int ttlSeconds)
    {
        var entry = new Entry(value, DateTimeOffset.UtcNow.AddSeconds(ttlSeconds));

        if (_data.TryAdd(key, entry))
            return UpsertResult.Created;
        
        _data[key] = entry;
        return UpsertResult.Updated;
    }

    public bool TryRemove(string key)
    {
        if (!_data.TryRemove(key, out var entry))
            return false;

        return !IsExpired(entry);
    }

    public string[] ListKeys(string prefix)
    {
        var now = DateTimeOffset.UtcNow;
        var keys = new List<string>();

        foreach (var pair in _data)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _data.TryRemove(pair.Key, out _);
                continue;
            }

            if (pair.Key.StartsWith(prefix))
                keys.Add(pair.Key);
        }

        return keys.OrderBy(k => k).ToArray();
    }

    private static bool IsExpired(Entry entry)
        => entry.ExpiresAt <= DateTimeOffset.UtcNow;
}

public static class ValidationRules
{
    private static readonly Regex KeyPattern = new Regex(@"^[a-zA-Z0-9:_-]+$", RegexOptions.Compiled);

    public static bool ValidateKey(string key, out string error)
    {
        error = null;

        if (string.IsNullOrEmpty(key))
        {
            error = "Key cannot be empty";
            return false;
        }

        if (key.Length < 1 || key.Length > 50)
        {
            error = "Key length must be between 1 and 50 characters";
            return false;
        }

        if (!KeyPattern.IsMatch(key))
        {
            error = "Key can only contain: a-z A-Z 0-9 : _ -";
            return false;
        }

        return true;
    }

    public static bool ValidateValue(string value, out string error)
    {
        error = null;

        if (value == null)
        {
            error = "Value cannot be null";
            return false;
        }

        if (value.Length > 2000)
        {
            error = "Value length must not exceed 2000 characters";
            return false;
        }

        return true;
    }

    public static bool ValidateTtl(int? ttlSeconds, out string error)
    {
        error = null;

        if (ttlSeconds is null)
        {
            error = "TTL (seconds) is required";
            return false;
        }

        if (ttlSeconds <= 0 || ttlSeconds > 3600)
        {
            error = "TTL must be between 1 and 3600 seconds";
            return false;
        }

        return true;
    }
}
