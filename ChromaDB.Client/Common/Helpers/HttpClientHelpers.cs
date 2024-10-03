﻿using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ChromaDB.Client.Models;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;

namespace ChromaDB.Client.Common.Helpers;

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

	public static async Task<Response<TResponse>> Get<TResponse>(this IChromaDBHttpClient httpClient, string endpoint, RequestQueryParams queryParams)
	{
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams));
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}

	public static async Task<Response<TResponse>> Post<TInput, TResponse>(this IChromaDBHttpClient httpClient, string endpoint, TInput? input, RequestQueryParams queryParams)
	{
		using var content = new StringContent(JsonSerializer.Serialize(input, PostJsonSerializerOptions) ?? string.Empty, new MediaTypeHeaderValue("application/json"));
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams))
		{
			Content = content,
			Headers = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
		};
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}

	public static async Task<Response<TResponse>> Put<TInput, TResponse>(this IChromaDBHttpClient httpClient, string endpoint, TInput? input, RequestQueryParams queryParams)
	{
		using var content = new StringContent(JsonSerializer.Serialize(input, PostJsonSerializerOptions) ?? string.Empty, new MediaTypeHeaderValue("application/json"));
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams))
		{
			Content = content,
			Headers = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
		};
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}

	public static async Task<Response<TResponse>> Delete<TResponse>(this IChromaDBHttpClient httpClient, string endpoint, RequestQueryParams queryParams)
	{
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri: ValidateAndPrepareEndpoint(endpoint, queryParams));
		return await Send<TResponse>(httpClient, httpRequestMessage);
	}

	private static async Task<Response<TResponse>> Send<TResponse>(IChromaDBHttpClient httpClient, HttpRequestMessage httpRequestMessage)
	{
		try
		{
			using var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

			return httpResponseMessage.IsSuccessStatusCode switch
			{
				true when typeof(TResponse) == typeof(Response.Empty) => CreateEmptyResponse(httpResponseMessage.StatusCode),
				true => CreateDataResponse(httpResponseMessage.StatusCode, await httpResponseMessage.Content.ReadAsStringAsync()),
				false when httpResponseMessage.StatusCode == HttpStatusCode.BadRequest
					|| httpResponseMessage.StatusCode == HttpStatusCode.UnprocessableContent
					|| httpResponseMessage.StatusCode == HttpStatusCode.InternalServerError
					=> CreateErrorResponse(httpResponseMessage.StatusCode, await httpResponseMessage.Content.ReadAsStringAsync()),
				_ => CreateErrorResponse(httpResponseMessage.StatusCode, null),
			};
		}
		catch (Exception ex)
		{
			// Decided on ServiceUnavailable error for all other exception types, we'll pass the exception message forward
			return new Response<TResponse>(
				statusCode: HttpStatusCode.ServiceUnavailable,
				errorMessage: ex.Message);
		}

		static Response<TResponse> CreateEmptyResponse(HttpStatusCode statusCode)
			=> new(
				statusCode: statusCode,
				data: (TResponse)(object)Response.Empty.Instance);

		static Response<TResponse> CreateDataResponse(HttpStatusCode statusCode, string responseBody)
			=> new(
				statusCode: statusCode,
				data: JsonSerializer.Deserialize<TResponse>(responseBody, DeserializerJsonSerializerOptions));

		static Response<TResponse> CreateErrorResponse(HttpStatusCode statusCode, string? errorMessageBody)
			=> new(
				statusCode: statusCode,
				errorMessage: errorMessageBody is not null and not []
					? ParseErrorMessageBody(errorMessageBody)
					: default);
	}

	private static string? ParseErrorMessageBody(string errorMessageBody)
	{
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
