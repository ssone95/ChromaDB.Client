using System.Net;
using System.Text.Json;
using ChromaDB.Client.DependencyInjection;
using ChromaDB.Client.Models;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// What the client checks before a request, and what it reads back, against a fake server.
[TestFixture]
public class InputChecksTests
{
	static readonly ReadOnlyMemory<float> Embedding = new([1f, 0f]);

	// A negative limit or offset is rejected whatever the number of ids, as SearchAsync and ChromaQuery.Offset do; a limit of 0 reads
	// nothing, also on Chroma 0.6.3, which takes it as no limit.
	[Test]
	public async Task GetLimitAndOffset()
	{
		var server = new Server();
		var collection = Collection(server, maxBatchSize: 2);
		var ids = new List<string> { "a", "b", "c" };
		Assert.That(() => collection.GetAsync(ids, limit: -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
		Assert.That(() => collection.GetAsync(ids, offset: -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
		Assert.That(() => collection.GetAsync(limit: -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
		Assert.That(await collection.GetAsync(ids, limit: 0), Is.Empty);
		Assert.That(await collection.GetAsync(limit: 0), Is.Empty);
		Assert.That(server.Paths.Where(path => path.EndsWith("/get")), Is.Empty);
	}

	[Test]
	public void QueryWithANegativeNumberOfResults()
	{
		Assert.That(() => new ChromaQuery([Embedding]) { NResults = -5 }, Throws.InstanceOf<ArgumentOutOfRangeException>());
	}

	// The connection string takes http and https endpoints only, as the constructor does.
	[TestCase("Endpoint=localhost:8000;Token=x")]
	[TestCase("Endpoint=ftp://host;Token=x")]
	public void EndpointWithoutHttp(string connectionString)
	{
		Assert.That(() => ChromaConfigurationOptions.FromConnectionString(connectionString), Throws.ArgumentException);
	}

	// Chroma takes none of these as a name; GetCollectionAsync already rejects them.
	[TestCase(".")]
	[TestCase("..")]
	public void CollectionNamesThatAreDots(string name)
	{
		using var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(new Server()));
		Assert.That(() => client.CreateCollectionAsync(name), Throws.ArgumentException);
		Assert.That(() => client.GetOrCreateCollectionAsync(name), Throws.ArgumentException);
	}

	// An id condition with a repeated id sends it once.
	[Test]
	public async Task RepeatedIdsInAnIdCondition()
	{
		var server = new Server();
		await Collection(server).GetAsync(where: ChromaWhereOperator.In(ChromaSearchKeys.Id, "a", "a", "b"));
		var get = server.Bodies.Single(x => x.Path.EndsWith("/get")).Body;
		Assert.That(get.GetProperty("ids").EnumerateArray().Select(x => x.GetString()), Is.EqualTo(new[] { "a", "b" }));
	}

	// The Python client takes a list of values of one type only: rejected before the request. An empty metadata goes as null.
	[Test]
	public void MetadataThatChromaCannotTake()
	{
		var collection = Collection(new Server());
		var mixed = new Dictionary<string, object> { ["mixed"] = new List<object> { "a", 1L } };
		Assert.That(() => collection.AddAsync(["a"], [Embedding], [mixed]), Throws.ArgumentException.With.Message.Contains("\"mixed\""));
		Assert.That(() => collection.UpsertAsync(["a"], [Embedding], [new Dictionary<string, object> { ["n"] = new object[] { 1, 2.5 } }]), Throws.ArgumentException);
		Assert.That(() => collection.UpdateAsync(["a"], metadatas: [new Dictionary<string, object> { ["n"] = new object[] { true, 1 } }]), Throws.ArgumentException);
	}

	// A record without keys comes back with an empty metadata when the metadatas are read, and with none when they are not.
	[Test]
	public async Task RecordsWithoutKeys()
	{
		var server = new Server { GetAnswer = """{"ids":["a","b"],"metadatas":[null,{"k":1}],"documents":null,"embeddings":null}""" };
		var entries = await Collection(server).GetAsync(include: ChromaGetInclude.Metadatas);
		Assert.That(entries[0].Metadata, Is.Not.Null.And.Empty);
		Assert.That(entries[1].Metadata!.Keys, Is.EqualTo(new[] { "k" }));
		server.GetAnswer = """{"ids":["a"],"metadatas":null,"documents":null,"embeddings":null}""";
		Assert.That((await Collection(server).GetAsync(include: ChromaGetInclude.None))[0].Metadata, Is.Null);
	}

	// Chroma Cloud behind another address, like a proxy: the options say it, and the batches are of 300 from the start.
	[Test]
	public void ChromaCloudBehindAProxy()
	{
		var options = new ChromaConfigurationOptions("https://chroma.example.com");
		Assert.That(options.IsChromaCloud, Is.False);
		Assert.That(options.WithChromaCloud().IsChromaCloud, Is.True);
		Assert.That(options.WithChromaCloud().WithDatabase("d").IsChromaCloud, Is.True);
		Assert.That(new ChromaConfigurationOptions("https://api.trychroma.com").WithChromaCloud(false).IsChromaCloud, Is.False);
	}

	[Test]
	public async Task ChromaCloudBehindAProxyWritesInBatchesOf300()
	{
		var server = new Server { MaxBatchSize = 1000 };
		var collection = new ChromaCollectionClient(Guid.Empty, "c", new ChromaConfigurationOptions("https://chroma.example.com").WithChromaCloud(), new HttpClient(server));
		var ids = Enumerable.Range(0, 301).Select(i => $"r{i}").ToList();
		await collection.AddAsync(ids, ids.Select(_ => Embedding).ToList());
		Assert.That(server.Bodies.Where(x => x.Path.EndsWith("/add")).Select(x => x.Body.GetProperty("ids").GetArrayLength()), Is.EqualTo(new[] { 300, 1 }));
	}

	// The creation sends get_or_create once, as the API of Chroma names it.
	[Test]
	public async Task CreationSendsGetOrCreateOnce()
	{
		var server = new Server();
		using var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(server));
		try { await client.CreateCollectionAsync("abc"); } catch (ChromaException) { }
		try { await client.GetOrCreateCollectionAsync("abc"); } catch (ChromaException) { }
		var bodies = server.Bodies.Where(x => x.Path.EndsWith("/collections")).Select(x => x.Body.EnumerateObject().ToList()).ToList();
		Assert.That(bodies, Has.Count.EqualTo(2));
		// Once in each body, false to create and true to get or create, and under no other name.
		Assert.That(bodies.Select(body => body.Where(p => p.Name.Equals("get_or_create", StringComparison.OrdinalIgnoreCase) || p.Name == "GetOrCreate").Select(p => $"{p.Name}={p.Value.GetRawText()}")),
			Is.EqualTo(new[] { new[] { "get_or_create=false" }, new[] { "get_or_create=true" } }));
	}

	// Keys with the same text, like 1 and "1", get an HttpClient each; a null key would register the client without a key.
	[Test]
	public async Task KeyedClientsWithTheSameText()
	{
		var number = new Server();
		var text = new Server();
		var services = new ServiceCollection();
		services.AddKeyedChromaClient(1, null, builder => builder.ConfigurePrimaryHttpMessageHandler(() => number));
		services.AddKeyedChromaClient("1", null, builder => builder.ConfigurePrimaryHttpMessageHandler(() => text));
		using var provider = services.BuildServiceProvider();
		await provider.GetRequiredKeyedService<ChromaClient>(1).GetVersionAsync();
		Assert.That((number.Paths.Count, text.Paths.Count), Is.EqualTo((1, 0)));
		Assert.That(() => new ServiceCollection().AddKeyedChromaClient(null), Throws.InstanceOf<ArgumentNullException>());
	}

	static ChromaCollectionClient Collection(HttpMessageHandler handler, int? maxBatchSize = null)
	{
		var options = new ChromaConfigurationOptions("http://localhost:8000");
		return new(Guid.Empty, "c", maxBatchSize is { } size ? options.WithBatchSplitting(size) : options, new HttpClient(handler));
	}

	// Answers pre-flight-checks, the version, a get with GetAnswer, and {} to the rest; records the paths and the bodies.
	sealed class Server : HttpMessageHandler
	{
		public int MaxBatchSize { get; set; } = 100;
		public string GetAnswer { get; set; } = """{"ids":[],"metadatas":null,"documents":null,"embeddings":null}""";
		public List<string> Paths { get; } = [];
		public List<(string Path, JsonElement Body)> Bodies { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var path = request.RequestUri!.AbsolutePath;
			Paths.Add(path);
			if (request.Content is not null)
			{
				Bodies.Add((path, JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken)).RootElement.Clone()));
			}
			var body = path.EndsWith("/pre-flight-checks") ? $$"""{"max_batch_size":{{MaxBatchSize}},"supports_base64_encoding":false}"""
				: path.EndsWith("/version") ? "\"1.0.0\""
				: path.EndsWith("/get") ? GetAnswer
				: "{}";
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
		}
	}
}
