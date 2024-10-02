using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ChromaDB.Client.Common.Exceptions;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;
using ChromaDB.Client.Services.Interfaces;

namespace ChromaDB.Client.Common.Helpers;

public static partial class HttpClientHelpers
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

	public static async Task<BaseResponse<TResponse>> Get<TResponse>(this IChromaDBHttpClient httpClient, string endpoint, RequestQueryParams queryParams)
	{
		using HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams));
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}

	public static async Task<BaseResponse<TResponse>> Post<TInput, TResponse>(this IChromaDBHttpClient httpClient, string endpoint, TInput? input, RequestQueryParams queryParams)
	{
		using StringContent content = new(JsonSerializer.Serialize(input, PostJsonSerializerOptions) ?? string.Empty, new MediaTypeHeaderValue("application/json"));
		using HttpRequestMessage httpRequestMessage = new(HttpMethod.Post, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams))
		{
			Content = content,
			Headers = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
		};
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}

	public static async Task<BaseResponse<TResponse>> Put<TInput, TResponse>(this IChromaDBHttpClient httpClient, string endpoint, TInput? input, RequestQueryParams queryParams)
	{
		using StringContent content = new(JsonSerializer.Serialize(input, PostJsonSerializerOptions) ?? string.Empty, new MediaTypeHeaderValue("application/json"));
		using HttpRequestMessage httpRequestMessage = new(HttpMethod.Put, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams))
		{
			Content = content,
			Headers = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
		};
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}

	public static async Task<BaseResponse<TResponse>> Delete<TResponse>(this IChromaDBHttpClient httpClient, string endpoint, RequestQueryParams queryParams)
	{
		using HttpRequestMessage httpRequestMessage = new(HttpMethod.Delete, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams));
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}

	private static async Task<BaseResponse<TResponse>> Send<TResponse>(IChromaDBHttpClient httpClient, HttpRequestMessage httpRequestMessage)
	{
		try
		{
			using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

			string? responseBody = httpResponseMessage.IsSuccessStatusCode switch
			{
				true => await httpResponseMessage.Content.ReadAsStringAsync(),
				false when httpResponseMessage.StatusCode == HttpStatusCode.BadRequest
					|| httpResponseMessage.StatusCode == HttpStatusCode.UnprocessableContent
					|| httpResponseMessage.StatusCode == HttpStatusCode.InternalServerError
					=> throw new ChromaDBException(httpResponseMessage.ReasonPhrase, null, httpResponseMessage.StatusCode) { ErrorMessageBody = await httpResponseMessage.Content.ReadAsStringAsync() },
				_ => throw new ChromaDBException(httpResponseMessage.ReasonPhrase, null, httpResponseMessage.StatusCode)
			};

			if (typeof(TResponse) == typeof(BaseResponse.None))
			{
				return new BaseResponse<TResponse>(
					data: (TResponse)(object)BaseResponse.None.Instance,
					statusCode: httpResponseMessage.StatusCode);
			}

			return new BaseResponse<TResponse>(
					data: responseBody is not null and not []
						? JsonSerializer.Deserialize<TResponse>(responseBody, DeserializerJsonSerializerOptions)
						: default,
					statusCode: httpResponseMessage.StatusCode);
		}
		catch (ChromaDBException ex)
		{
			return ex.ErrorMessageBody switch
			{
				not null and not [] => new BaseResponse<TResponse>(data: default, statusCode: ex.StatusCode!.Value, reasonPhrase: ParseErrorMessageBody(ex.ErrorMessageBody)),
				_ => new BaseResponse<TResponse>(data: default, statusCode: ex.StatusCode!.Value)
			};
		}
		catch (Exception ex)
		{
			// Decided on ServiceUnavailable error for all other exception types, we'll pass the exception message forward
			return new BaseResponse<TResponse>(data: default, statusCode: HttpStatusCode.ServiceUnavailable, reasonPhrase: ex.Message);
		}
	}

	private static string? ParseErrorMessageBody(string errorMessageBody)
	{
		try
		{
			GeneralError deserialized = JsonSerializer.Deserialize<GeneralError>(errorMessageBody, DeserializerJsonSerializerOptions)!;
			Match match = ParseErrorMessageBodyRegex().Match(deserialized?.ErrorMessage ?? string.Empty);

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
		List<string> queryArgs = PrepareQueryParams(endpoint);

		if (queryArgs is [])
		{
			return endpoint;
		}

		return FormatRequestUri(endpoint, queryParams);
	}

	private static string FormatRequestUri(string endpoint, RequestQueryParams queryParams)
	{
		string formattedEndpoint = endpoint;
		foreach (KeyValuePair<string, string> entry in queryParams.Build())
		{
			string urlEncodedQueryParam = Uri.EscapeDataString(entry.Value);
			formattedEndpoint = formattedEndpoint.Replace(entry.Key, urlEncodedQueryParam);
		}
		return formattedEndpoint;
	}
}
