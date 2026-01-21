using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace StorageApi.Server;

public interface IKvStore
{
    bool TryGet(string key, out string value);
    UpsertResult Upsert(string key, string value);
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
    private readonly ConcurrentDictionary<string, string> _data = new();

    public bool TryGet(string key, out string value)
        => _data.TryGetValue(key, out value);

    public UpsertResult Upsert(string key, string value)
    {
        if (_data.TryAdd(key, value))
            return UpsertResult.Created;
        
        _data[key] = value;
        return UpsertResult.Updated;
    }

    public bool TryRemove(string key)
        => _data.TryRemove(key, out _);

    public string[] ListKeys(string prefix)
        => _data.Keys.Where(k => k.StartsWith(prefix)).OrderBy(k => k).ToArray();
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
}
