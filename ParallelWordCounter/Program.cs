// All readings first load all tokens into memory (array).
// (to rig the results :)))

using System.Collections.Concurrent;
using System.Diagnostics;

// ------------------------------------------------------
// Konfigurace
// ------------------------------------------------------
string dataDir = Path.Combine(AppContext.BaseDirectory, "data");
int topN = 20;

if (!Directory.Exists(dataDir))
{
	Console.WriteLine($"Folder '{dataDir}' not found. Put .txt files into 'data' folder, or pass a path as the first argument.");
	return;
}

// ------------------------------------------------------
// Pomocné funkce připravené v šabloně
// ------------------------------------------------------
static IEnumerable<string> GetTextFiles(string dir) => Directory.EnumerateFiles(dir, "*.txt");

static IEnumerable<string> Tokenize(string line)
{
	// jednoduchá normalizace: malá písmena, ne-alfabetické -> mezera
	var chars = line.ToLowerInvariant()
		.Select(ch => char.IsLetter(ch) ? ch : ' ')
		.ToArray();

	return new string(chars)
		.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}

static IEnumerable<string> ReadTokensFromFile(string filePath)
{
	foreach (var line in File.ReadLines(filePath))
	{
		foreach (var w in Tokenize(line))
		{
			yield return w;
		}
	}
}

// ------------------------------------------------------
// TODO 1: Sekvenční řešení (referenční)
//  - Projděte všechny .txt soubory (GetTextFiles)
//  - Z každého souboru čtěte tokeny (ReadTokensFromFile)
//  - Započítejte četnosti slov do Dictionary<string,int>
//  - Změřte čas pomocí Stopwatch čas das změřte
// ------------------------------------------------------
Dictionary<string, int> SequentialCount(IEnumerable<string> allTokens)
{
	var sw = Stopwatch.StartNew();
	
	var counts = new Dictionary<string, int>(StringComparer.Ordinal);
	foreach (var token in allTokens)
	{
		// TODO: započítejte token do counts
		if (counts.TryGetValue(token, out var c))
			counts[token]++;
		else
			counts[token] = 1;
	}

	sw.Stop();
	Console.WriteLine($"[SEQ] Done in {sw.ElapsedMilliseconds} ms");

	return counts;
}

// ------------------------------------------------------
// TODO 2: Paralelní řešení (vlákna + zámek)
//  - Spusťte 1 vlákno na každý soubor
//  - Sdílená Dictionary<string,int> counts
//  - Při inkrementu používejte lock(gate) ke zamezení race condition
//  - Na konci Join všech vláken, změřte čas
// ------------------------------------------------------
Dictionary<string, int> ParallelCount(IEnumerable<string> allTokens)
{
    var sw = Stopwatch.StartNew();

    var counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

    // Parallelize over the token stream
    Parallel.ForEach(
        Partitioner.Create(allTokens),
        () => new Dictionary<string, int>(StringComparer.Ordinal), // thread-local counts
        (token, state, localCounts) =>
        {
            if (localCounts.TryGetValue(token, out var c))
                localCounts[token] = c + 1;
            else
                localCounts[token] = 1;

            return localCounts;
        },
        localCounts =>
        {
            foreach (var kv in localCounts)
                counts.AddOrUpdate(kv.Key, kv.Value, (_, old) => old + kv.Value);
        });

    sw.Stop();
    Console.WriteLine($"[PAR] Done in {sw.ElapsedMilliseconds} ms");

    return counts.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
}

// ------------------------------------------------------
//  - Seřaďte dle Value desc a vypište prvních N
// ------------------------------------------------------
static void PrintTopN(Dictionary<string, int> counts, int n)
{
	var top = counts
		.OrderByDescending(kv => kv.Value)
		.Take(n);

	foreach (var kv in top)
	{
		Console.WriteLine($"{kv.Value,7} : {kv.Key}");
	}
}

// ------------------------------------------------------
// Hlavní tok programu
//  - Spusťte sekvenční řešení a vytiskněte čas
//  - Spusťte paralelní řešení a vytiskněte čas
//  - Porovnejte součet výskytů (SEQ vs PAR)
//  - Vypište Top N pro obě varianty
// ------------------------------------------------------
Console.WriteLine("== ParallelWordCounter (starter) ==\n");
Console.WriteLine($"Data dir: {dataDir}");
Console.WriteLine($"Top N   : {topN}\n");

// porovnejte součty a vytiskněte TopN
var loadTimestamp = Stopwatch.GetTimestamp();
var allTokens = GetTextFiles(dataDir).SelectMany(ReadTokensFromFile).ToArray();
Console.WriteLine($"Loaded {allTokens.Length} tokens in {Stopwatch.GetElapsedTime(loadTimestamp, Stopwatch.GetTimestamp()).TotalMilliseconds} ms");	
var seqCounts = SequentialCount(allTokens);
var parCounts = ParallelCount(allTokens);
Console.WriteLine($"\nTotal words: SEQ={seqCounts.Values.Sum()}, PAR={parCounts.Values.Sum()}");
Console.WriteLine("\nTop N words (SEQ):");
PrintTopN(seqCounts, topN);
Console.WriteLine("\nTop N words (PAR):");
PrintTopN(parCounts, topN);
Console.WriteLine("\n== The End ==");
