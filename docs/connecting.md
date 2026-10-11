# Connecting

## The URI and the HttpClient

The URI can be just the address of the server, like `http://localhost:8000`, and the client adds `/api/v2/`. A URI with a path, like `http://localhost:8000/api/v2`, is used as it is, with or without the trailing slash.

Without an `HttpClient`, `new ChromaClient("http://localhost:8000")` or `new ChromaClient(options)` creates its own, and `Dispose` closes it: `using var client = new ChromaClient("http://localhost:8000");`. The clients it returns, like the collection clients, share that `HttpClient`. An `HttpClient` you pass to the constructor stays open.

## Sharing a client between threads

`ChromaClient` and the collection clients can be shared between threads, as singletons: a call keeps its state to itself, what the client learns about the server is read once under a lock, and a collection client made by name may read its collection more than once when calls start together. Two clients that write the same records at the same time get what Chroma does with them.

## API version

The client uses the v2 API. For the servers that have only the v1 API, like Chroma 0.5.15, choose it once in the options:

```csharp
var options = new ChromaConfigurationOptions(uri: "http://localhost:8000").WithApiVersion(ChromaApiVersion.V1);

// or, with dependency injection
services.AddChromaClient(options => options!.WithUri("http://localhost:8000").WithApiVersion(ChromaApiVersion.V1));
```

## Authentication

```csharp
var options = new ChromaConfigurationOptions(uri: "http://localhost:8000");

// X-Chroma-Token: <token>, the header of Chroma Cloud.
var tokenOptions = options.WithChromaToken("token");

// Authorization: Bearer <token>, the default of the token authentication of the Chroma 0.x servers.
var bearerOptions = options.WithChromaToken("token", ChromaTokenTransportHeader.Authorization);

// Authorization: Basic, for the Chroma 0.x servers with basic authentication.
var basicOptions = options.WithBasicAuth("admin", "password");
```

The client adds the credentials to each of its requests, without changing the `HttpClient` you pass.

## Connection strings

From a connection string, as the settings of an application keep it:

```csharp
var options = ChromaConfigurationOptions.FromConnectionString("Endpoint=https://api.trychroma.com;Token=ck-...;Tenant=...;Database=...");
```

`Endpoint` is the http or https URI of the server, and a connection string that is just a URI is the endpoint alone; another key, no endpoint, or an endpoint without http or https throws an `ArgumentException` that says which. `Token` goes in the `X-Chroma-Token` header. `Tenant` and `Database` are the ones of the requests.
