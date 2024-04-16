using System.Text.Json.Serialization;
using ChromaDB.Client.Common.Attributes;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Models;

[ChromaRoute("GET", TargetEndpoint = "collections/{collectionName}?tenant={tenant}&database={database}", Source = typeof(Collection), ResponseType = typeof(Collection))]
[ChromaRoute("GET", TargetEndpoint = "collections?tenant={tenant}&database={database}", Source = typeof(Collection), ResponseType = typeof(List<Collection>))]
[ChromaRoute("POST", TargetEndpoint = "collections/{collection_id}/get", Source = typeof(Collection), RequestType = typeof(CollectionGetRequest), ResponseType = typeof(CollectionEntriesResponse))]
[ChromaRoute("POST", TargetEndpoint = "collections/{collection_id}/query", Source = typeof(Collection), RequestType = typeof(CollectionQueryRequest), ResponseType = typeof(CollectionEntriesQueryResponse))]
public class Collection
{
	[JsonPropertyName("id")]
	public Guid Id { get; set; }

	[JsonPropertyName("name")]
	public string Name { get; set; }

	[JsonPropertyName("metadata")]
	public IDictionary<string, object>? Metadata { get; set; }

	[JsonPropertyName("tenant")]
	public string? Tenant { get; set; }

	[JsonPropertyName("database")]
	public string? Database { get; set; }

	public Collection(string name)
	{
		Name = name;
	}
}
