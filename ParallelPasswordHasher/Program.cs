using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

const int PasswordCount = 10_000;   // počet hesel (můžete měnit)
const int Iterations = 300_000;  // PBKDF2 iterace (100k–300k dle HW)

// Generátor vstupů (in-memory)
static List<string> GeneratePasswords(int count)
	=> Enumerable.Range(0, count).Select(i => $"pass{i:D6}").ToList();

// Fixní salt jen pro cvičení (deterministické výsledky)
byte[] Salt = Encoding.UTF8.GetBytes("salt-01");

// PBKDF2-SHA256 (CPU-bound)
byte[] HashPbkdf2(string password, int iterations) =>
	Rfc2898DeriveBytes.Pbkdf2(
		password: Encoding.UTF8.GetBytes(password),
		salt: Salt,
		iterations: iterations,
		hashAlgorithm: HashAlgorithmName.SHA256,
		outputLength: 32);

static byte[] HashPbkdf2Static(string password, int iterations, ReadOnlySpan<byte> Salt) =>
	Rfc2898DeriveBytes.Pbkdf2(
		password: Encoding.UTF8.GetBytes(password),
		salt: Salt,
		iterations: iterations,
		hashAlgorithm: HashAlgorithmName.SHA256,
		outputLength: 32);

// A) SEKVENCE (baseline) – HOTOVO
IDictionary<string, string> HashAllSequential(IReadOnlyList<string> passwords, int iterations)
{
	var dict = new Dictionary<string, string>();
	foreach (var p in passwords)
	{
		dict[p] = Convert.ToBase64String(HashPbkdf2(p, iterations));
	}
	return dict;
}

// B) PARALELNĚ – Threads
IDictionary<string, string> HashAllThreads(IReadOnlyList<string> passwords, int iterations)
{
	var dict = new ConcurrentDictionary<string, string>(
		concurrencyLevel: Environment.ProcessorCount,
		capacity: passwords.Count);

	var threadCount = Environment.ProcessorCount;

	// Insejn tríček na rozdělení práce
	int chunkSize = (passwords.Count + threadCount - 1) / threadCount;
	
	var threads = new Thread[threadCount];
	
	for (int i = 0; i < threadCount; i++)
	{
		var threadIndex = i;
		threads[i] = new Thread(() =>
		{
			var start = threadIndex * chunkSize;
			var end = Math.Min(start + chunkSize, passwords.Count);
			
			for (int j = start; j < end; j++)
			{
				dict[passwords[j]] = Convert.ToBase64String(
					HashPbkdf2(passwords[j], iterations)
				);
			}
		});
		threads[i].Start();
	}
	
	foreach (var thread in threads)
	{
		thread.Join();
	}
	
	return dict;
}

// BV2) PARALELNĚ – Threads
// Nevypadá to na zaznamenatelné zrychlení
// Span sám o sobě to prostě magicky nezrychlí
IDictionary<string, string> HashAllThreadsV2(List<string> passwords, int iterations)
{
	var dict = new ConcurrentDictionary<string, string>(
		concurrencyLevel: Environment.ProcessorCount,
		capacity: passwords.Count);

	var threadCount = Environment.ProcessorCount;

	// Insejn tríček na rozdělení práce
	int chunkSize = (passwords.Count + threadCount - 1) / threadCount;
	
	// Hmm
	var threads = new Thread[threadCount];
	
	for (int i = 0; i < threadCount; i++)
	{
		var threadIndex = i;

		threads[i] = new Thread(_ => Work(
			threadIndex,
			chunkSize,
			passwords.Count,
			dict,
			CollectionsMarshal.AsSpan(passwords),
			iterations,
			Salt
		));

		threads[i].Start();
	}
	
	foreach (var thread in threads)
	{
		thread.Join();
	}
	
	return dict;

	static void Work(
		int threadIndex,
		int chunkSize,
		int passwordCount,
		ConcurrentDictionary<string, string> dict,
		ReadOnlySpan<string> passwords,
		int iterations,
		ReadOnlySpan<byte> salt)
	{
		var start = threadIndex * chunkSize;
		var end = Math.Min(start + chunkSize, passwordCount);
			
		for (int j = start; j < end; j++)
		{
			dict[passwords[j]] = Convert.ToBase64String(
				HashPbkdf2Static(passwords[j], iterations, salt)
			);
		}
	}
}

// C) PARALELNĚ – Tasks
async Task<IDictionary<string, string>> HashAllTasksAsync(IReadOnlyList<string> passwords, int iterations)
{
	var dict = new ConcurrentDictionary<string, string>(
		concurrencyLevel: -1,
		capacity: passwords.Count);
	
	// Flashbacky na Azure Durable functions
	var tasks = passwords.Select(password => Task.Run(() =>
	{
		dict[password] = Convert.ToBase64String(HashPbkdf2(password, iterations));
	}));
	
	await Task.WhenAll(tasks);
	
	return dict;
}

// D) PARALELNĚ – Parallel.ForEach
IDictionary<string, string> HashAllParallel(IReadOnlyList<string> passwords, int iterations)
{
	var dict = new ConcurrentDictionary<string, string>(
		concurrencyLevel: -1,
		capacity: passwords.Count);
	
	Parallel.ForEach(passwords, password =>
	{
		dict[password] = Convert.ToBase64String(HashPbkdf2(password, iterations));
	});
	
	return dict;
}


// DV2) PARALELNĚ – Parallel.ForEach
// Na těhle triviálních benchmarks to mělo lepší výsledky než předchozí verze
// Concurrency level a MaxDegreeOfParallelism asi by default nejsou stejné?
IDictionary<string, string> HashAllParallelV2(IReadOnlyList<string> passwords, int iterations)
{
	var dict = new ConcurrentDictionary<string, string>(
		concurrencyLevel: Environment.ProcessorCount,
		capacity: passwords.Count);
	
	Parallel.ForEach(
		passwords,
		new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount
		},
		password =>
		{
			dict[password] = Convert.ToBase64String(HashPbkdf2(password, iterations));
		}
	);
	
	return dict;
}

// DV3) PARALELNĚ – Parallel.ForEach no logical cores
// Výrazně pomalejší než na plném počtu jader (logických)
IDictionary<string, string> HashAllParallelV3NoLogicalCores(IReadOnlyList<string> passwords, int iterations)
{
	var dict = new ConcurrentDictionary<string, string>(
		concurrencyLevel: Environment.ProcessorCount / 2,
		capacity: passwords.Count);
	
	Parallel.ForEach(
		passwords,
		new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount / 2
		},
		password =>
		{
			dict[password] = Convert.ToBase64String(HashPbkdf2(password, iterations));
		}
	);
	
	return dict;
}

// DV4 Not enough parallel???
// Na malých datech to nijak to nepohlo s výsledkem, hmm
IDictionary<string, string> HashAllParallelV4BroNoThatsTooMuch(IReadOnlyList<string> passwords, int iterations)
{
	var dict = new ConcurrentDictionary<string, string>(
		concurrencyLevel: 32,
		capacity: passwords.Count);
	
	Parallel.ForEach(
		passwords,
		new ParallelOptions
		{
			MaxDegreeOfParallelism = 32
		},
		password =>
		{
			dict[password] = Convert.ToBase64String(HashPbkdf2(password, iterations));
		}
	);
	
	return dict;
}

// DEMO & měření – start
var passwords = GeneratePasswords(PasswordCount);
Console.WriteLine($"Passwords: {passwords.Count}, Iterations: {Iterations} (CPU Cores: {Environment.ProcessorCount})\n");

var sw = Stopwatch.StartNew();

// 1) Sekvenčně
sw.Restart();
var seq = HashAllSequential(passwords, Iterations);
sw.Stop();
var tSeq = sw.Elapsed.TotalMilliseconds;
Console.WriteLine($"[SEQ]       {tSeq,8:F0} ms");

//2) Threads
sw.Restart();
var th = HashAllThreads(passwords, Iterations);
sw.Stop();
var tTh = sw.Elapsed.TotalMilliseconds;
Console.WriteLine($"[THREADS]   {tTh,8:F0} ms   (speedup {tSeq / tTh:0.00}×)");

// BV2) Threads V2
sw.Restart();
var thv2 = HashAllThreadsV2(passwords, Iterations);
sw.Stop();
var tThv2 = sw.Elapsed.TotalMilliseconds;
Console.WriteLine($"[THREADSv2] {tThv2,8:F0} ms   (speedup {tSeq / tThv2:0.00}×)");

//3) Tasks
sw.Restart();
var ta = await HashAllTasksAsync(passwords, Iterations);
sw.Stop();
var tTa = sw.Elapsed.TotalMilliseconds;
Console.WriteLine($"[TASKS]     {tTa,8:F0} ms   (speedup {tSeq / tTa:0.00}×)");

//4) Parallel.ForEach
sw.Restart();
var pf = HashAllParallel(passwords, Iterations);
sw.Stop();
var tPf = sw.Elapsed.TotalMilliseconds;
Console.WriteLine($"[PARALLEL]  {tPf,8:F0} ms   (speedup {tSeq / tPf:0.00}×)");

//4V2) Parallel.ForEach V2
sw.Restart();
var pfv2 = HashAllParallelV2(passwords, Iterations);
sw.Stop();
var tPfv2 = sw.Elapsed.TotalMilliseconds;
Console.WriteLine($"[PARALLELv2]{tPfv2,8:F0} ms   (speedup {tSeq / tPfv2:0.00}×)");

//4V3) Parallel.ForEach V3 no logical cores
sw.Restart();
var pfv3 = HashAllParallelV3NoLogicalCores(passwords, Iterations);
sw.Stop();
var tPfv3 = sw.Elapsed.TotalMilliseconds;
Console.WriteLine($"[PARALLELv3]{tPfv3,8:F0} ms   (speedup {tSeq / tPfv3:0.00}×)");

//4V4) Parallel.ForEach V4 too much bro
sw.Restart();
var pfv4 = HashAllParallelV4BroNoThatsTooMuch(passwords, Iterations);
sw.Stop();
var tPfv4 = sw.Elapsed.TotalMilliseconds;
Console.WriteLine($"[PARALLELv4]{tPfv4,8:F0} ms   (speedup {tSeq / tPfv4:0.00}×)");

//5) Kontroly shody
Console.WriteLine();
if (th != null)
{
	bool sameTh = seq.Count == th.Count && seq.All(kv => th.TryGetValue(kv.Key, out var v) && v == kv.Value);
	Console.WriteLine($"Match (SEQ vs THREADS):   {sameTh}");
}
if (thv2 != null)
{
	bool sameThv2 = seq.Count == thv2.Count && seq.All(kv => thv2.TryGetValue(kv.Key, out var v) && v == kv.Value);
	Console.WriteLine($"Match (SEQ vs THREADSv2): {sameThv2}");
}
if (ta != null)
{
	bool sameTa = seq.Count == ta.Count && seq.All(kv => ta.TryGetValue(kv.Key, out var v) && v == kv.Value);
	Console.WriteLine($"Match (SEQ vs TASKS):     {sameTa}");
}
if (pf != null)
{
	bool samePf = seq.Count == pf.Count && seq.All(kv => pf.TryGetValue(kv.Key, out var v) && v == kv.Value);
	Console.WriteLine($"Match (SEQ vs PARALLEL):  {samePf}");
}

if (pfv2 != null)
{
	bool samePfv2 = seq.Count == pfv2.Count && seq.All(kv => pfv2.TryGetValue(kv.Key, out var v) && v == kv.Value);
	Console.WriteLine($"Match (SEQ vs PARALLELv2):{samePfv2}");
}

if (pfv3 != null)
{
	bool samePfv3 = seq.Count == pfv3.Count && seq.All(kv => pfv3.TryGetValue(kv.Key, out var v) && v == kv.Value);
	Console.WriteLine($"Match (SEQ vs PARALLELv3):{samePfv3}");
}

if (pfv4 != null)
{
	bool samePfv4 = seq.Count == pfv4.Count && seq.All(kv => pfv4.TryGetValue(kv.Key, out var v) && v == kv.Value);
	Console.WriteLine($"Match (SEQ vs PARALLELv4):{samePfv4}");
}

Console.WriteLine("\nHotovo.");

// Nejlepší výkonové výsledku dávalo:
// Zapojení do zásuvky
// Přepnutí větráku na režim "Full speed"
// To je všechno... lol...