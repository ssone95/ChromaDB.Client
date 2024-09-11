using System.Text.Json.Serialization;
using ChromaDB.Client.Common.Attributes;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Models;

[ChromaGetRoute(Endpoint = "collections/{collectionName}?tenant={tenant}&database={database}", Source = typeof(Collection), ResponseType = typeof(Collection))]
[ChromaGetRoute(Endpoint = "collections?tenant={tenant}&database={database}", Source = typeof(Collection), ResponseType = typeof(List<Collection>))]
[ChromaPostRoute(Endpoint = "collections/{collection_id}/get", Source = typeof(Collection), RequestType = typeof(CollectionGetRequest), ResponseType = typeof(CollectionEntriesResponse))]
[ChromaPostRoute(Endpoint = "collections/{collection_id}/query", Source = typeof(Collection), RequestType = typeof(CollectionQueryRequest), ResponseType = typeof(CollectionEntriesQueryResponse))]
[ChromaPostRoute(Endpoint = "collections?tenant={tenant}&database={database}", Source = typeof(Collection), RequestType = typeof(DBCreateCollectionRequest), ResponseType = typeof(Collection))]
[ChromaPostRoute(Endpoint = "collections?tenant={tenant}&database={database}", Source = typeof(Collection), RequestType = typeof(DBGetOrCreateCollectionRequest), ResponseType = typeof(Collection))]
[ChromaDeleteRoute(Endpoint = "collections/{collectionName}?tenant={tenant}&database={database}", Source = typeof(Collection), ResponseType = typeof(BaseResponse.None))]
public class Collection
{
	[JsonPropertyName("id")]
	public Guid Id { get; init; }

	[JsonPropertyName("name")]
	public string Name { get; }

	[JsonPropertyName("metadata")]
	public IDictionary<string, object>? Metadata { get; init; }

	[JsonPropertyName("tenant")]
	public string? Tenant { get; init; }

	[JsonPropertyName("database")]
	public string? Database { get; init; }

	public Collection(string name)
	{
		Name = name;
	}
}
