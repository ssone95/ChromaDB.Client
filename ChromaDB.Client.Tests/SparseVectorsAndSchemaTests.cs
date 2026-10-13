using System.Net;
using System.Text.Json;
using ChromaDB.Client.Models;
using NUnit.Framework;

namespace ChromaDB.Client.Tests;

// Sparse vectors and the schema of a collection, as the Python client of Chroma 1.5.9 writes them and Chroma Cloud answers, against a fake server.
[TestFixture]
public class SparseVectorsAndSchemaTests
{
	const string CollectionsPath = "/api/v2/tenants/default_tenant/databases/default_database/collections";
	const string Bm25Schema = """{"defaults":{},"keys":{"doc_bm25":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":{"type":"known","name":"chroma_bm25","config":{"k":1.2,"b":0.75,"avg_doc_length":256.0,"token_max_length":40,"include_tokens":false}},"source_key":"#document","bm25":true}}}}}}""";

	[Test]
	public void SparseVectorJson()
	{
		Assert.That(new ChromaSparseVector([1, 5], [0.5f, 0.7f]).ToString(), Is.EqualTo("""{"#type":"sparse_vector","indices":[1,5],"values":[0.5,0.7]}"""));
		Assert.That(new ChromaSparseVector([2], [1f], ["apple"]).ToString(), Is.EqualTo("""{"#type":"sparse_vector","indices":[2],"values":[1.0],"tokens":["apple"]}"""));
		Assert.That(new ChromaSparseVector([2147483648, uint.MaxValue], [1f, 2f]).ToString(), Is.EqualTo("""{"#type":"sparse_vector","indices":[2147483648,4294967295],"values":[1.0,2.0]}"""));
	}

	// The rules of Chroma and of its Python client: Chroma Cloud rejects indices out of order with 400.
	[Test]
	public void SparseVectorThatChromaRejects()
	{
		Assert.That(() => new ChromaSparseVector([1, 2], [1f]), Throws.ArgumentException);
		Assert.That(() => new ChromaSparseVector([3, 1], [1f, 2f]), Throws.ArgumentException);
		Assert.That(() => new ChromaSparseVector([1, 1], [1f, 2f]), Throws.ArgumentException);
		Assert.That(() => new ChromaSparseVector([1], [1f], ["a", "b"]), Throws.ArgumentException);
	}

	// The checked values cannot change afterwards, not even through a cast.
	[Test]
	public void SparseVectorIsReadOnly()
	{
		var vector = new ChromaSparseVector([1, 5], [0.5f, 0.7f], ["a", "b"]);
		Assert.That(vector.Indices, Is.Not.InstanceOf<uint[]>());
		Assert.That(vector.Values, Is.Not.InstanceOf<float[]>());
		Assert.That(vector.Tokens, Is.Not.InstanceOf<string[]>());
	}

	[Test]
	public void KnnWithASparseVector()
	{
		Assert.That(ChromaRank.SparseKnn(new ChromaSparseVector([1, 5], [1f, 1f]), "doc_bm25", returnRank: true).ToString(),
			Is.EqualTo("""{"$knn":{"query":{"#type":"sparse_vector","indices":[1,5],"values":[1.0,1.0]},"key":"doc_bm25","limit":16,"return_rank":true}}"""));
	}

	[Test]
	public async Task AddSparseVectorsInMetadata()
	{
		var server = new FakeServer(r => r.Path switch
		{
			var path when path.EndsWith("/version") => (HttpStatusCode.OK, "\"1.0.0\""),
			var path when path.EndsWith("/add") => (HttpStatusCode.Created, "{}"),
			_ => (HttpStatusCode.NotFound, ""),
		});
		await CollectionClient(server).AddAsync(new ChromaRecords(["a"]) { Metadatas = [new Dictionary<string, object> { ["doc_bm25"] = new ChromaSparseVector([1], [0.5f]), ["x"] = 1 }] });
		var add = server.Requests.Single(x => x.Path.EndsWith("/add"));
		Assert.That(add.Body.GetProperty("metadatas")[0].GetRawText(), Is.EqualTo("""{"doc_bm25":{"#type":"sparse_vector","indices":[1],"values":[0.5]},"x":1}"""));
	}

	// Chroma 0.6.3 accepts sparse vectors in metadata and stores the metadata as null: nothing is sent, also for a sparse vector read
	// with the default metadata values, which is a JsonElement.
	[TestCase(false)]
	[TestCase(true)]
	public async Task SparseVectorsInMetadataOnChroma0(bool asJsonElement)
	{
		var server = new FakeServer(r => r.Path.EndsWith("/version") ? (HttpStatusCode.OK, "\"0.6.3\"") : (HttpStatusCode.OK, "true"));
		object vector = asJsonElement
			? JsonDocument.Parse("""{"#type":"sparse_vector","indices":[1],"values":[0.5],"tokens":null}""").RootElement.Clone()
			: new ChromaSparseVector([1], [0.5f]);
		await Assert.ThatAsync(() => CollectionClient(server).AddAsync(new ChromaRecords(["a"]) { Embeddings = [new([1f])], Metadatas = [new Dictionary<string, object> { ["v"] = vector }] }),
			Throws.InstanceOf<ChromaException>().With.Message.Contains("sparse vectors"));
		Assert.That(server.Requests.Any(x => x.Path.EndsWith("/add")), Is.False);
	}

	// Chroma Cloud sends "#type" and "tokens": null. Exact metadata values give a ChromaSparseVector, the default ones a JsonElement, as before.
	[TestCase(ChromaMetadataValues.Exact)]
	[TestCase(ChromaMetadataValues.Inferred)]
	public async Task ReadSparseVectorsInMetadata(ChromaMetadataValues metadataValues)
	{
		var server = new FakeServer(_ => (HttpStatusCode.OK, """{"ids":["a"],"metadatas":[{"doc_bm25":{"#type":"sparse_vector","indices":[1,4294967295],"values":[0.5,0.7],"tokens":null},"x":1}]}"""));
		var options = new ChromaConfigurationOptions("http://localhost:8000").WithMetadataValues(metadataValues);
		var value = (await CollectionClient(server, options).GetAsync("a", include: ChromaGetInclude.Metadatas))!.Metadata!["doc_bm25"];
		if (metadataValues == ChromaMetadataValues.Exact)
		{
			var vector = (ChromaSparseVector)value;
			Assert.That((vector.Indices, vector.Values, vector.Tokens), Is.EqualTo(((IReadOnlyList<uint>)[1, uint.MaxValue], (IReadOnlyList<float>)[0.5f, 0.7f], (IReadOnlyList<string>?)null)));
		}
		else
		{
			Assert.That(((JsonElement)value).GetProperty("#type").GetString(), Is.EqualTo("sparse_vector"));
		}
	}

	[Test]
	public async Task CreateCollectionWithASchema()
	{
		var server = new FakeServer(_ => (HttpStatusCode.OK, $$$"""{"id":"11111111-2222-3333-4444-555555555555","name":"c","schema":{{{Bm25Schema}}}}"""));
		var definition = new ChromaCollectionDefinition("c") { Schema = new ChromaCollectionSchema().WithSparseVectorIndex("doc_bm25", ChromaSearchKeys.Document, bm25: true, ChromaEmbeddingFunctionReference.ChromaBm25()) };
		var collection = await Client(server).CreateCollectionAsync(definition);
		Assert.That(server.Requests.Single().Body.GetProperty("schema").GetRawText(), Is.EqualTo(Bm25Schema));
		var index = collection.SparseVectorIndexes.Single();
		Assert.That((index.Key, index.SourceKey, index.Bm25, index.EmbeddingFunction), Is.EqualTo(("doc_bm25", "#document", true, "chroma_bm25")));
		Assert.That(definition.Schema.ToString(), Is.EqualTo(Bm25Schema));
	}

	// With a schema the space goes in it, as create_index(VectorIndexConfig(space=...)) of the Python client writes it: Chroma Cloud
	// rejects the hnsw:space metadata together with a schema, "Cannot set both collection config and schema simultaneously".
	[TestCase("create")]
	[TestCase("get_or_create")]
	public async Task SpaceInTheSchema(string operation)
	{
		var server = new FakeServer(r => r.Path.EndsWith("/version") ? (HttpStatusCode.OK, "\"1.0.0\"")
			: (HttpStatusCode.OK, $$$"""{"id":"11111111-2222-3333-4444-555555555555","name":"c","configuration_json":{"hnsw":null,"spann":{"space":"cosine"}},"schema":{{{Bm25Schema}}}}"""));
		var definition = new ChromaCollectionDefinition("c")
		{
			Metadata = new Dictionary<string, object> { ["x"] = 1 },
			Configuration = new() { Space = ChromaSpace.Cosine },
			Schema = new ChromaCollectionSchema().WithSparseVectorIndex("doc_bm25", ChromaSearchKeys.Document, bm25: true, ChromaEmbeddingFunctionReference.ChromaBm25()),
		};
		var collection = operation == "create" ? await Client(server).CreateCollectionAsync(definition) : await Client(server).GetOrCreateCollectionAsync(definition);
		Assert.That(collection.Space, Is.EqualTo(ChromaSpace.Cosine));
		var body = server.Requests.Single(x => x.Method == "POST").Body;
		Assert.That(body.GetProperty("metadata").GetRawText(), Is.EqualTo("""{"x":1}"""));
		Assert.That(body.GetProperty("schema").GetRawText(), Is.EqualTo("""
			{"defaults":{"float_list":{"vector_index":{"enabled":false,"config":{"space":"cosine"}}}},
			"keys":{"doc_bm25":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":{"type":"known","name":"chroma_bm25","config":{"k":1.2,"b":0.75,"avg_doc_length":256.0,"token_max_length":40,"include_tokens":false}},"source_key":"#document","bm25":true}}}},
			"#embedding":{"float_list":{"vector_index":{"enabled":true,"config":{"space":"cosine"}}}}}}
			""".Replace("\r", "").Replace("\n", "").Replace("\t", "")));
	}

	// Chroma 1.3.0 creates the collection with the space of a schema that turns no index on or off ignored, l2: CreateCollection deletes
	// it and throws, GetOrCreateCollection throws and keeps it, since it may have existed before.
	[TestCase("create", true)]
	[TestCase("get_or_create", false)]
	public async Task SpaceIgnoredByTheServer(string operation, bool deleted)
	{
		var server = new FakeServer(r => r.Method == "DELETE" ? (HttpStatusCode.OK, "{}")
			: r.Path.EndsWith("/version") ? (HttpStatusCode.OK, "\"1.0.0\"")
			: (HttpStatusCode.OK, """{"id":"11111111-2222-3333-4444-555555555555","name":"c","configuration_json":{"hnsw":{"space":"l2"}},"schema":{"defaults":{},"keys":{}}}"""));
		var definition = new ChromaCollectionDefinition("c") { Configuration = new() { Space = ChromaSpace.Cosine }, Schema = new ChromaCollectionSchema() };
		await Assert.ThatAsync(() => operation == "create" ? Client(server).CreateCollectionAsync(definition) : Client(server).GetOrCreateCollectionAsync(definition),
			Throws.InstanceOf<ChromaException>().With.Message.Contains("space l2, not cosine").And.Message.Contains("Chroma 1.3.0 ignores the space of a schema that turns no index on or off"));
		Assert.That(server.Requests.Any(x => x.Method == "DELETE"), Is.EqualTo(deleted));
	}

	// Without a schema the request stays as before.
	[Test]
	public async Task CreateCollectionWithoutASchema()
	{
		var server = new FakeServer(_ => (HttpStatusCode.OK, """{"id":"11111111-2222-3333-4444-555555555555","name":"c"}"""));
		var collection = await Client(server).CreateCollectionAsync("c");
		Assert.That(server.Requests.Single().Body.TryGetProperty("schema", out _), Is.False);
		Assert.That(collection.SparseVectorIndexes, Is.Empty);
	}

	// Chroma 1.0.0 to 1.2.2 create the collection without the schema: CreateCollection deletes it, GetOrCreateCollection keeps it.
	[TestCase(false)]
	[TestCase(true)]
	public async Task SchemaThatTheServerIgnores(bool getOrCreate)
	{
		var server = new FakeServer(r => r.Method == "DELETE" ? (HttpStatusCode.OK, "{}") : (HttpStatusCode.OK, """{"id":"11111111-2222-3333-4444-555555555555","name":"c","schema":null}"""));
		var definition = new ChromaCollectionDefinition("c") { Schema = new ChromaCollectionSchema().WithSparseVectorIndex("v") };
		var client = Client(server);
		await Assert.ThatAsync(() => getOrCreate ? client.GetOrCreateCollectionAsync(definition) : client.CreateCollectionAsync(definition),
			Throws.InstanceOf<ChromaException>().With.Message.Contains("Chroma 1.3.0"));
		Assert.That(server.Requests.Select(x => x.Line), Is.EqualTo(getOrCreate
			? new[] { $"POST {CollectionsPath}" }
			: new[] { $"POST {CollectionsPath}", $"DELETE {CollectionsPath}/c" }));
	}

	// Chroma Cloud answers 400 "If source_key is provided then embedding_function must also be provided".
	[Test]
	public void SourceKeyWithoutEmbeddingFunction()
	{
		Assert.That(() => new ChromaCollectionSchema().WithSparseVectorIndex("doc_bm25", ChromaSearchKeys.Document), Throws.ArgumentException);
		Assert.That(() => new ChromaCollectionSchema().WithSparseVectorIndex("doc_bm25", bm25: true), Throws.Nothing);
	}

	// The call is canceled once the answer to the creation has arrived: the collection is deleted all the same.
	[Test]
	public async Task SchemaThatTheServerIgnoresWhenTheCallIsCanceledAfterTheAnswer()
	{
		using var cancellation = new CancellationTokenSource();
		var requests = new List<string>();
		var handler = new CancelAfterAnswer(cancellation, requests);
		var definition = new ChromaCollectionDefinition("c") { Schema = new ChromaCollectionSchema().WithSparseVectorIndex("v") };
		var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(handler));
		await Assert.ThatAsync(() => client.CreateCollectionAsync(definition, cancellationToken: cancellation.Token), Throws.InstanceOf<ChromaException>());
		Assert.That(requests, Is.EqualTo(new[] { "POST", "DELETE" }));
	}

	// Answers the creation with a collection without a schema, and cancels the token when the body of that answer has been read.
	sealed class CancelAfterAnswer(CancellationTokenSource cancellation, List<string> requests) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			requests.Add(request.Method.Method);
			HttpContent content = request.Method == HttpMethod.Post
				? new CancelWhenRead("""{"id":"11111111-2222-3333-4444-555555555555","name":"c","schema":null}""", cancellation)
				: new StringContent("{}");
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
		}
	}

	sealed class CancelWhenRead(string body, CancellationTokenSource cancellation) : HttpContent
	{
		protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
		{
			await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(body));
			cancellation.Cancel();
		}

		protected override bool TryComputeLength(out long length)
		{
			length = -1;
			return false;
		}
	}

	// The reference keeps a copy of the settings: changing them afterwards does not change the schema.
	[Test]
	public async Task EmbeddingFunctionSettingsAreCopied()
	{
		var config = new Dictionary<string, object> { ["k"] = 1 };
		var schema = new ChromaCollectionSchema().WithSparseVectorIndex("v", embeddingFunction: ChromaEmbeddingFunctionReference.Known("f", config));
		config["k"] = 2;
		var server = new FakeServer(_ => (HttpStatusCode.OK, """{"id":"11111111-2222-3333-4444-555555555555","name":"c","schema":{"defaults":{},"keys":{}}}"""));
		await Client(server).CreateCollectionAsync(new ChromaCollectionDefinition("c") { Schema = schema });
		Assert.That(server.Requests.Single().Body.GetProperty("schema").GetProperty("keys").GetProperty("v").GetRawText(), Does.Contain("""{"k":1}"""));
	}

	// The settings of chroma_bm25 in the schema, as build_from_config of the Python client reads them: the missing ones get the default values.
	[Test]
	public void Bm25FunctionFromTheSchema()
	{
		var index = Index("""{"type":"known","name":"chroma_bm25","config":{"k":1.5,"b":0.6,"avg_doc_length":100,"token_max_length":10,"include_tokens":true,"stopwords":["quick","fox"]}}""");
		var function = index.Bm25Function!;
		Assert.That((function.K, function.B, function.AvgDocLength, function.TokenMaxLength, function.IncludeTokens, function.Stopwords), Is.EqualTo((1.5, 0.6, 100.0, 10, true, (IReadOnlyList<string>?)["quick", "fox"])));
		Assert.That(index.EmbeddingFunctionConfig!.Value.GetProperty("k").GetDouble(), Is.EqualTo(1.5));
		var text = "The quick brown fox jumps over the lazy dog. Foxes!";
		Assert.That(function.Embed(text).ToString(), Is.EqualTo(new ChromaBm25(1.5, 0.6, 100, 10, ["quick", "fox"], true).Embed(text).ToString()));

		var defaults = Index("""{"type":"known","name":"chroma_bm25","config":{"stopwords":null,"include_tokens":null}}""").Bm25Function!;
		Assert.That((defaults.K, defaults.B, defaults.AvgDocLength, defaults.TokenMaxLength, defaults.IncludeTokens, defaults.Stopwords), Is.EqualTo((1.2, 0.75, 256.0, 40, false, (IReadOnlyList<string>?)null)));
		Assert.That(Index("""{"type":"known","name":"chroma_bm25","config":{"token_max_length":12.7}}""").Bm25Function!.TokenMaxLength, Is.EqualTo(12));
	}

	// The index whose vectors come from BM25 on the text of the key; not one with another function, nor one without a source key.
	[Test]
	public void FindBm25Index()
	{
		const string Bm25 = """{"type":"known","name":"chroma_bm25","config":{}}""";
		const string Splade = """{"type":"known","name":"splade","config":{}}""";
		static string Key(string name, string function, string? source)
			=> "\"" + name + "\":" + """{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":""" + function
				+ (source is null ? "" : ",\"source_key\":\"" + source + "\"") + "}}}}";
		var keys = string.Join(",", Key("a", Bm25, "#document"), Key("b", Splade, "title"), Key("c", Bm25, "title"), Key("d", Bm25, null));
		var collection = new ChromaCollection("c") { SchemaJson = JsonDocument.Parse("""{"defaults":{},"keys":{""" + keys + "}}").RootElement };
		Assert.That(collection.FindBm25Index("#document")?.Key, Is.EqualTo("a"));
		Assert.That(collection.FindBm25Index("title")?.Key, Is.EqualTo("c"));
		Assert.That(collection.FindBm25Index("body"), Is.Null);
		Assert.That(new ChromaCollection("c").FindBm25Index("#document"), Is.Null);
	}

	// Where the Python client keeps no function: another function, no config, or a setting of the wrong type.
	[TestCase("""{"type":"known","name":"splade","config":{}}""")]
	[TestCase("""{"type":"known","name":"chroma_bm25"}""")]
	[TestCase("""{"type":"known","name":"chroma_bm25","config":null}""")]
	[TestCase("""{"type":"known","name":"chroma_bm25","config":{"k":"1.2"}}""")]
	[TestCase("""{"type":"known","name":"chroma_bm25","config":{"b":null}}""")]
	[TestCase("""{"type":"known","name":"chroma_bm25","config":{"token_max_length":1e10}}""")]
	[TestCase("""{"type":"known","name":"chroma_bm25","config":{"include_tokens":1}}""")]
	[TestCase("""{"type":"known","name":"chroma_bm25","config":{"stopwords":"the"}}""")]
	[TestCase("""{"type":"known","name":"chroma_bm25","config":{"stopwords":["the",1]}}""")]
	public void NoBm25Function(string function)
	{
		Assert.That(Index(function).Bm25Function, Is.Null);
	}

	// As the Python client: the vectors of the indexes with a source key, for the records whose metadata does not have them.
	[TestCase("add")]
	[TestCase("upsert")]
	[TestCase("update")]
	public async Task SparseVectorsComputedOnWrite(string operation)
	{
		var server = new FakeServer(r => r.Path.EndsWith("/version") ? (HttpStatusCode.OK, "\"1.5.9\"") : (HttpStatusCode.OK, "true"));
		var given = new ChromaSparseVector([1], [0.5f]);
		var metadatas = new List<Dictionary<string, object>>
		{
			new() { ["title"] = "Red apples" },
			new() { ["doc_bm25"] = given },
			null!,
			new() { ["title"] = 5 },
		};
		var records = new ChromaRecords(["a", "b", "c", "d"]) { Documents = ["apple pie", "banana split", null!, "cherry tart"], Metadatas = metadatas };
		var client = CollectionClient(server, TwoSourcesSchema);
		await (operation switch { "add" => client.AddAsync(records), "upsert" => client.UpsertAsync(records), _ => client.UpdateAsync(records) });

		var sent = server.Requests.Single(x => x.Path.EndsWith("/" + operation)).Body.GetProperty("metadatas");
		var bm25 = new ChromaBm25();
		Assert.That(sent.GetRawText(), Is.EqualTo($$"""
			[{"title":"Red apples","doc_bm25":{{bm25.Embed("apple pie")}},"title_bm25":{{new ChromaBm25(k: 1.5).Embed("Red apples")}}},
			{"doc_bm25":{{given}}},
			null,
			{"title":5,"doc_bm25":{{bm25.Embed("cherry tart")}}}]
			""".Replace("\r", "").Replace("\n", "").Replace("\t", "")));
		Assert.That(metadatas[0].Keys, Is.EqualTo(new[] { "title" }));
		Assert.That(records.Metadatas, Is.SameAs(metadatas));
	}

	// Without metadata, a record with a document gets one with its vector.
	[Test]
	public async Task SparseVectorsWithoutMetadata()
	{
		var server = new FakeServer(r => r.Path.EndsWith("/version") ? (HttpStatusCode.OK, "\"1.5.9\"") : (HttpStatusCode.OK, "true"));
		await CollectionClient(server, TwoSourcesSchema).AddAsync(new ChromaRecords(["a", "b"]) { Documents = ["apple pie", null!] });
		Assert.That(server.Requests.Single(x => x.Path.EndsWith("/add")).Body.GetProperty("metadatas").GetRawText(),
			Is.EqualTo("""[{"doc_bm25":""" + new ChromaBm25().Embed("apple pie") + "},null]"));
	}

	// Without a schema, or without a source key in it, the records go as they are.
	[TestCase(null)]
	[TestCase("""{"defaults":{},"keys":{"doc_bm25":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"bm25":true}}}}}}""")]
	public async Task NoSparseVectorsToCompute(string? schema)
	{
		var server = new FakeServer(_ => (HttpStatusCode.OK, "true"));
		await CollectionClient(server, schema).AddAsync(new ChromaRecords(["a"]) { Documents = ["apple pie"] });
		Assert.That(server.Requests.Single(x => x.Path.EndsWith("/add")).Body.TryGetProperty("metadatas", out var metadatas) ? metadatas.ValueKind : JsonValueKind.Undefined, Is.AnyOf(JsonValueKind.Null, JsonValueKind.Undefined));
	}

	// Another function: the client cannot compute its vectors, so it throws before sending, unless the metadata has them.
	[Test]
	public async Task SparseVectorsOfAnotherFunction()
	{
		var server = new FakeServer(r => r.Path.EndsWith("/version") ? (HttpStatusCode.OK, "\"1.5.9\"") : (HttpStatusCode.OK, "true"));
		var client = CollectionClient(server, """{"defaults":{},"keys":{"doc_splade":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":{"type":"known","name":"splade","config":{}},"source_key":"#document"}}}}}}""");
		await Assert.ThatAsync(() => client.AddAsync(new ChromaRecords(["a"]) { Documents = ["apple pie"] }),
			Throws.InstanceOf<ChromaException>().With.Message.Contains("\"doc_splade\"").And.Message.Contains("\"splade\""));
		Assert.That(server.Requests, Is.Empty);
		await client.AddAsync(new ChromaRecords(["a"]) { Documents = ["apple pie"], Metadatas = [new Dictionary<string, object> { ["doc_splade"] = new ChromaSparseVector([1], [0.5f]) }] });
		Assert.That(server.Requests.Count(x => x.Path.EndsWith("/add")), Is.EqualTo(1));
	}

	// A text query of SparseKnn becomes the vector of the function of the index, also inside other expressions.
	[Test]
	public async Task SearchWithAText()
	{
		var server = new FakeServer(_ => (HttpStatusCode.OK, """{"ids":[[]],"documents":[null],"embeddings":[null],"metadatas":[null],"scores":[null],"select":[[]]}"""));
		var rank = ChromaRank.Rrf([ChromaRank.Knn(new([1f, 0f]), returnRank: true), ChromaRank.SparseKnn("Red apples", "title_bm25", returnRank: true)]);
		Assert.That(rank.ToString(), Does.Contain("""{"$knn":{"query":"Red apples","key":"title_bm25","limit":16,"return_rank":true}}"""));
		await CollectionClient(server, TwoSourcesSchema).SearchAsync(new ChromaSearch { Rank = rank });
		Assert.That(server.Requests.Single().Body.GetProperty("searches")[0].GetProperty("rank").GetRawText(),
			Does.Contain($$$"""{"$knn":{"query":{{{new ChromaBm25(k: 1.5).Embed("Red apples")}}},"key":"title_bm25","limit":16,"return_rank":true}}"""));
	}

	// As the Python client: a text query needs a key with an index and a function the client knows.
	[TestCase(null, "doc_bm25")]
	[TestCase(TwoSourcesSchema, "other")]
	[TestCase("""{"defaults":{},"keys":{"doc_splade":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":{"type":"known","name":"splade","config":{}},"source_key":"#document"}}}}}}""", "doc_splade")]
	public async Task SearchWithATextItCannotEmbed(string? schema, string key)
	{
		var server = new FakeServer(_ => (HttpStatusCode.OK, "{}"));
		await Assert.ThatAsync(() => CollectionClient(server, schema).SearchAsync(new ChromaSearch { Rank = ChromaRank.SparseKnn("apple", key) }),
			Throws.InstanceOf<ChromaException>().With.Message.Contains($"\"{key}\""));
		Assert.That(server.Requests, Is.Empty);
	}

	// doc_bm25 from the documents with the default settings, title_bm25 from the title in the metadata with k 1.5, and an index without a source.
	const string TwoSourcesSchema = """
		{"defaults":{},"keys":{
		"doc_bm25":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":{"type":"known","name":"chroma_bm25","config":{"k":1.2,"b":0.75,"avg_doc_length":256.0,"token_max_length":40,"include_tokens":false}},"source_key":"#document","bm25":true}}}},
		"title_bm25":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":{"type":"known","name":"chroma_bm25","config":{"k":1.5}},"source_key":"title","bm25":true}}}},
		"v":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"bm25":false}}}}}}
		""";

	static ChromaSparseVectorIndex Index(string function)
		=> new ChromaCollection("c") { SchemaJson = JsonDocument.Parse("""{"defaults":{},"keys":{"v":{"sparse_vector":{"sparse_vector_index":{"enabled":true,"config":{"embedding_function":""" + function + ""","source_key":"#document"}}}}}}""").RootElement }
			.SparseVectorIndexes.Single();

	static ChromaCollectionClient CollectionClient(HttpMessageHandler handler, string? schema)
		=> new(new ChromaCollection("c") { Id = Guid.Parse("11111111-2222-3333-4444-555555555555"), SchemaJson = schema is null ? null : JsonDocument.Parse(schema).RootElement },
			new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(handler));

	static ChromaClient Client(HttpMessageHandler handler)
		=> new(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(handler));

	static ChromaCollectionClient CollectionClient(HttpMessageHandler handler, ChromaConfigurationOptions? options = null)
		=> new(new ChromaCollection("c") { Id = Guid.Parse("11111111-2222-3333-4444-555555555555") }, options ?? new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(handler));

	sealed record Request(string Method, string Path, string Line, JsonElement Body);

	sealed class FakeServer(Func<Request, (HttpStatusCode Status, string Body)> answer) : HttpMessageHandler
	{
		public List<Request> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var text = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
			var recorded = new Request(request.Method.Method, request.RequestUri!.AbsolutePath, $"{request.Method.Method} {request.RequestUri.PathAndQuery}",
				text is { Length: > 0 } ? JsonDocument.Parse(text).RootElement.Clone() : default);
			var (status, response) = answer(recorded);
			// The version, which the client also asks for a collection without a space: 1.0.0 unless the answer is a version, and
			// then not recorded, as the tests that do not answer it do not look for it.
			if (recorded.Path.EndsWith("/version") && !response.StartsWith("\""))
			{
				(status, response) = (HttpStatusCode.OK, "\"1.0.0\"");
			}
			else
			{
				Requests.Add(recorded);
			}
			return new HttpResponseMessage(status) { Content = new StringContent(response) };
		}
	}
}
