#:package ChromaDotNet.Client@2.14.0

// A first look at ChromaDB.Client, with ChromaDotNet.Client from NuGet: a collection, records with documents and metadata, a query by
// similarity, a filter on the metadata, a get and a count. The embeddings are written by hand, three numbers each, so no model is needed.
// Run it with Chroma on http://localhost:8000, or set CHROMA_URL:
//   dotnet run Samples/quickstart.cs
using ChromaDB.Client;

var uri = Environment.GetEnvironmentVariable("CHROMA_URL") ?? "http://localhost:8000";
var client = new ChromaClient(new ChromaConfigurationOptions(uri));
await client.HeartbeatAsync();
Console.WriteLine($"Connected to Chroma at {uri}");

var name = $"quickstart_{Guid.NewGuid():N}";
var collection = client.GetCollectionClient(await client.CreateCollectionAsync(name));
Console.WriteLine($"Created the collection {name}");

try
{
	// Three numbers per record: how much it is about fruit, vehicles and colors.
	await collection.AddAsync(
		["apple", "banana", "car", "bicycle", "red"],
		embeddings: [new([0.9f, 0.1f, 0.3f]), new([0.9f, 0.1f, 0.1f]), new([0.1f, 0.9f, 0.1f]), new([0.2f, 0.8f, 0f]), new([0.2f, 0.1f, 0.9f])],
		metadatas:
		[
			new Dictionary<string, object> { ["kind"] = "fruit", ["price"] = 2 },
			new Dictionary<string, object> { ["kind"] = "fruit", ["price"] = 1 },
			new Dictionary<string, object> { ["kind"] = "vehicle", ["price"] = 20000 },
			new Dictionary<string, object> { ["kind"] = "vehicle", ["price"] = 300 },
			new Dictionary<string, object> { ["kind"] = "color", ["price"] = 0 },
		],
		documents: ["A red apple", "A yellow banana", "A fast car", "A city bicycle", "The color red"]);
	Console.WriteLine($"Added {await collection.CountAsync()} records");

	Console.WriteLine();
	Console.WriteLine("Nearest to something fruity:");
	var nearest = await collection.QueryAsync([new([1f, 0f, 0.3f])], nResults: 3, include: ChromaQueryInclude.Documents | ChromaQueryInclude.Distances);
	foreach (var entry in nearest[0])
	{
		Console.WriteLine($"  {entry.Id,-8} {entry.Distance,6:F3}  {entry.Document}");
	}

	Console.WriteLine();
	Console.WriteLine("Vehicles under 1000, by metadata:");
	var cheap = await collection.GetAsync(where: ChromaWhereOperator.Equal("kind", "vehicle") & ChromaWhereOperator.LessThan("price", 1000), include: ChromaGetInclude.Documents | ChromaGetInclude.Metadatas);
	foreach (var entry in cheap)
	{
		Console.WriteLine($"  {entry.Id,-8} price {entry.Metadata!["price"],-6} {entry.Document}");
	}

	Console.WriteLine();
	var apple = (await collection.GetAsync(["apple"], include: ChromaGetInclude.Documents | ChromaGetInclude.Embeddings)).Single();
	Console.WriteLine($"Got apple: \"{apple.Document}\", embedding [{string.Join(", ", apple.Embedding!.Value.ToArray())}]");
}
finally
{
	await client.DeleteCollectionAsync(name);
	Console.WriteLine($"Deleted the collection {name}");
}
