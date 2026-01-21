namespace StorageApi.Client;

public static class CliParser
{
    public static bool TryParse(string[] args, out CliCommand command, out string error)
    {
        command = null;
        error = null;

        if (args.Length == 0)
        {
            error = "Missing command.";
            return false;
        }

        var name = args[0].ToLowerInvariant();

        if (name == "set" && args.Length >= 4)
        {
            if (!int.TryParse(args[2], out var ttl) || ttl <= 0 || ttl > 3600)
            {
                error = "TTL must be an integer between 1 and 3600 seconds.";
                return false;
            }

            command = CliCommand.Set(args[1], string.Join(" ", args.Skip(3)), ttl);
            return true;
        }

        if (name == "batchset" && args.Length == 2)
        {
            command = CliCommand.Batch(args[1]);
            return true;
        }

        if (name == "get" && args.Length == 2)
        {
            command = CliCommand.Get(args[1]);
            return true;
        }

        if (name == "del" && args.Length == 2)
        {
            command = CliCommand.Del(args[1]);
            return true;
        }

        if (name == "list")
        {
            command = CliCommand.List(args.Length >= 2 ? args[1] : null);
            return true;
        }

        error = "Invalid command.";
        return false;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Commands: set <key> <ttlSeconds> <value>, batchset <filePath>, get <key>, del <key>, list [prefix]");
    }
}

public sealed class CliCommand
{
    private CliCommand(string name) => Name = name;

    public string Name { get; }
    public string Key { get; private set; }
    public string Value { get; private set; }
    public string Prefix { get; private set; }
    public int TtlSeconds { get; private set; }
    public string FilePath { get; private set; }

    public static CliCommand Set(string key, string value, int ttlSeconds) => new("set") { Key = key, Value = value, TtlSeconds = ttlSeconds };
    public static CliCommand Batch(string filePath) => new("batchset") { FilePath = filePath };
    public static CliCommand Get(string key) => new("get") { Key = key };
    public static CliCommand Del(string key) => new("del") { Key = key };
    public static CliCommand List(string prefix) => new("list") { Prefix = prefix };
}
