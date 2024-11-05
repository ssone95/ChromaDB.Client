using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Common;

internal static partial class HttpClientHelpers
{
	private static readonly JsonSerializerOptions PostJsonSerializerOptions = new()
	{
		AllowTrailingCommas = false,
		ReferenceHandler = ReferenceHandler.IgnoreCycles,
		ReadCommentHandling = JsonCommentHandling.Skip,
	};

	private static readonly JsonSerializerOptions DeserializerJsonSerializerOptions = new()
	{
		Converters =
		{
			new ObjectToInferredTypesJsonConverter(),
		},
	};

	public static async Task<TResponse> Get<TResponse>(this HttpClient httpClient, string endpoint, RequestQueryParams queryParams)
	{
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams));
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}
	public static async Task Get(this HttpClient httpClient, string endpoint, RequestQueryParams queryParams)
	{
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams));
		await Send(httpClient, httpRequestMessage);
	}

	public static async Task<TResponse> Post<TInput, TResponse>(this HttpClient httpClient, string endpoint, TInput? input, RequestQueryParams queryParams)
	{
		using var content = new StringContent(JsonSerializer.Serialize(input, PostJsonSerializerOptions) ?? string.Empty, new MediaTypeHeaderValue("application/json"));
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams))
		{
			Content = content,
			Headers = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
		};
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}
	public static async Task Post<TInput>(this HttpClient httpClient, string endpoint, TInput? input, RequestQueryParams queryParams)
	{
		using var content = new StringContent(JsonSerializer.Serialize(input, PostJsonSerializerOptions) ?? string.Empty, new MediaTypeHeaderValue("application/json"));
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams))
		{
			Content = content,
			Headers = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
		};
		await Send(httpClient, httpRequestMessage);
	}

	public static async Task<TResponse> Put<TInput, TResponse>(this HttpClient httpClient, string endpoint, TInput? input, RequestQueryParams queryParams)
	{
		using var content = new StringContent(JsonSerializer.Serialize(input, PostJsonSerializerOptions) ?? string.Empty, new MediaTypeHeaderValue("application/json"));
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams))
		{
			Content = content,
			Headers = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
		};
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}
	public static async Task Put<TInput>(this HttpClient httpClient, string endpoint, TInput? input, RequestQueryParams queryParams)
	{
		using var content = new StringContent(JsonSerializer.Serialize(input, PostJsonSerializerOptions) ?? string.Empty, new MediaTypeHeaderValue("application/json"));
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams))
		{
			Content = content,
			Headers = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
		};
		await Send(httpClient, httpRequestMessage);
	}

	public static async Task<TResponse> Delete<TResponse>(this HttpClient httpClient, string endpoint, RequestQueryParams queryParams)
	{
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams));
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}
	public static async Task Delete(this HttpClient httpClient, string endpoint, RequestQueryParams queryParams)
	{
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams));
		await Send(httpClient, httpRequestMessage);
	}

	private static async Task<TResponse> Send<TResponse>(HttpClient httpClient, HttpRequestMessage httpRequestMessage)
	{
		try
		{
			using var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);
			return (int)httpResponseMessage.StatusCode switch
			{
				>= 200 and <= 299 => JsonSerializer.Deserialize<TResponse>(await httpResponseMessage.Content.ReadAsStringAsync(), DeserializerJsonSerializerOptions)!,
				_ => throw await HandleErrorStatusCode(httpResponseMessage),
			};
		}
		catch (Exception ex) when (ex is not ChromaException)
		{
			throw new ChromaException(ex.Message, ex);
		}
	}
	private static async Task Send(HttpClient httpClient, HttpRequestMessage httpRequestMessage)
	{
		try
		{
			using var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);
			switch ((int)httpResponseMessage.StatusCode)
			{
				case >= 200 and <= 299:
					return;
				default:
					throw await HandleErrorStatusCode(httpResponseMessage);
			};
		}
		catch (Exception ex) when (ex is not ChromaException)
		{
			throw new ChromaException(ex.Message, ex);
		}
	}

	private static async Task<ChromaException> HandleErrorStatusCode(HttpResponseMessage httpResponseMessage)
	{
		return httpResponseMessage.StatusCode switch
		{
			HttpStatusCode.BadRequest
				or HttpStatusCode.UnprocessableContent
				or HttpStatusCode.InternalServerError
				=> new ChromaException(ParseErrorMessageBody(await httpResponseMessage.Content.ReadAsStringAsync())),
			_ => new ChromaException($"Unexpected status code: {httpResponseMessage.StatusCode}."),
		};
	}

	private static string? ParseErrorMessageBody(string? errorMessageBody)
	{
		if (errorMessageBody is null or [])
		{
			return null;
		}

		try
		{
			var deserialized = JsonSerializer.Deserialize<GeneralError>(errorMessageBody, DeserializerJsonSerializerOptions)!;
			var match = ParseErrorMessageBodyRegex().Match(deserialized?.Error ?? string.Empty);

			return match.Success
				? match.Groups["errorMessage"]?.Value
				: $"Couldn't identify the error message: {errorMessageBody}";
		}
		catch
		{
			return $"Couldn't parse the incoming error message body: {errorMessageBody}";
		}
	}

	private static List<string> PrepareQueryParams(string input)
	{
		return PrepareQueryParamsRegex().Matches(input)
			.Select(x => x.Value)
			.ToList();
	}

	[GeneratedRegex(@"\('(?<errorMessage>.*)'\)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
	private static partial Regex ParseErrorMessageBodyRegex();

	[GeneratedRegex(@"{[a-zA-Z0-9\-_]+}", RegexOptions.CultureInvariant)]
	private static partial Regex PrepareQueryParamsRegex();

	private static string ValidateAndPrepareEndpoint(string endpoint, RequestQueryParams queryParams)
	{
		var queryArgs = PrepareQueryParams(endpoint);
		return queryArgs is not []
		? FormatRequestUri(endpoint, queryParams)
		: endpoint;
	}

	private static string FormatRequestUri(string endpoint, RequestQueryParams queryParams)
	{
		var formattedEndpoint = endpoint;
		foreach (var (key, value) in queryParams)
		{
			var urlEncodedQueryParam = Uri.EscapeDataString(value);
			formattedEndpoint = formattedEndpoint.Replace(key, urlEncodedQueryParam);
		}
		return formattedEndpoint;
	}
}
