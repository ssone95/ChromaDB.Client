using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ChromaDB.Client.Common.Attributes;
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

	public static async Task<BaseResponse<TResponse>> Get<TSource, TResponse>(this IChromaDBHttpClient httpClient, RequestQueryParams? queryParams = null) where TSource : class
	{
		(_, _, string endpoint, HttpMethod method, IReadOnlyList<string> queryArgs) = GetRouteDetailsByType<TSource, TResponse>(HttpMethod.Get);
		try
		{
			string formattedEndpoint = ValidateAndPrepareEndpoint<TSource>(queryArgs, endpoint, queryParams);

			using HttpRequestMessage httpRequestMessage = new(method, requestUri: formattedEndpoint);
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

			return new BaseResponse<TResponse>(
					data: responseBody is not null and not []
						? JsonSerializer.Deserialize<TResponse>(responseBody, DeserializerJsonSerializerOptions)
						: default,
					statusCode: httpResponseMessage.StatusCode);
		}
		catch (ChromaDBGeneralException ex)
		{
			return new BaseResponse<TResponse>(data: default, statusCode: HttpStatusCode.InternalServerError, reasonPhrase: ex.Message);
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

	public static async Task<BaseResponse<TResponse>> Post<TSource, TInput, TResponse>(this IChromaDBHttpClient httpClient, TInput? input, RequestQueryParams? queryParams = null) where TSource : class
	{
		(_, _, string endpoint, HttpMethod method, IReadOnlyList<string> queryArgs) = GetRouteDetailsByType<TSource, TResponse>(HttpMethod.Post, typeof(TInput));
		try
		{
			string formattedEndpoint = ValidateAndPrepareEndpoint<TSource>(queryArgs, endpoint, queryParams);

			string serializedInput = input is not null
				? JsonSerializer.Serialize(input, PostJsonSerializerOptions)
				: string.Empty;
			using StringContent content = new(serializedInput, new MediaTypeHeaderValue("application/json"));
			using HttpRequestMessage httpRequestMessage = new(method, requestUri: formattedEndpoint)
			{
				Content = content,
				Headers = { Accept = { new MediaTypeWithQualityHeaderValue("application/json") } }
			};
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

			return new BaseResponse<TResponse>(
					data: responseBody is not null and not []
					? JsonSerializer.Deserialize<TResponse>(responseBody, DeserializerJsonSerializerOptions)
					: default,
					statusCode: httpResponseMessage.StatusCode);
		}
		catch (ChromaDBGeneralException ex)
		{
			return new BaseResponse<TResponse>(data: default, statusCode: HttpStatusCode.InternalServerError, reasonPhrase: ex.Message);
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

	public static async Task<BaseResponse<TResponse>> Delete<TSource, TResponse>(this IChromaDBHttpClient httpClient, RequestQueryParams? queryParams = null) where TSource : class
	{
		(_, _, string endpoint, HttpMethod method, IReadOnlyList<string> queryArgs) = GetRouteDetailsByType<TSource, TResponse>(HttpMethod.Delete);
		try
		{
			string formattedEndpoint = ValidateAndPrepareEndpoint<TSource>(queryArgs, endpoint, queryParams);

			using HttpRequestMessage httpRequestMessage = new(method, requestUri: formattedEndpoint);
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

			return new BaseResponse<TResponse>(
					data: responseBody is not null and not []
						? JsonSerializer.Deserialize<TResponse>(responseBody, DeserializerJsonSerializerOptions)
						: default,
					statusCode: httpResponseMessage.StatusCode);
		}
		catch (ChromaDBGeneralException ex)
		{
			return new BaseResponse<TResponse>(data: default, statusCode: HttpStatusCode.InternalServerError, reasonPhrase: ex.Message);
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
				: $"Couldn't identify the error message: {deserialized?.ErrorMessage}";
		}
		catch
		{
			return $"Couldn't parse the incoming error message body: {errorMessageBody}";
		}
	}

	[GeneratedRegex(@"\('(?<errorMessage>.*)'\)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
	private static partial Regex ParseErrorMessageBodyRegex();

	private static string ValidateAndPrepareEndpoint<TSource>(IReadOnlyList<string> queryArgs, string endpoint, RequestQueryParams? queryParams = null) where TSource : class
	{
		if (queryArgs is [])
		{
			return endpoint;
		}

		string queryArgsString = string.Join(",", queryArgs);
		if (queryParams is null)
			throw new ChromaDBGeneralException(message: $"Type {typeof(TSource)} requires the following arguments to be provided: [{queryArgsString}], but argument {nameof(queryParams)} is null.");

		IEnumerable<string> nonProvidedQueryArgs = queryArgs
			.Except(queryArgs.Where(queryParams.HasKeyIgnoreCase));

		if (nonProvidedQueryArgs.Any())
		{
			string nonProvidedQueryArgsString = string.Join(",", nonProvidedQueryArgs);
			throw new ChromaDBGeneralException($"Type {typeof(TSource)} requires the following arguments to be provided: [{queryArgsString}], but {nonProvidedQueryArgsString} weren't provided.");
		}

		return FormatRequestUri(endpoint, queryParams!);
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

	private static (Type source, Type response, string endpoint, HttpMethod method, IReadOnlyList<string> queryArgs) GetRouteDetailsByType<T, TOutput>(HttpMethod method, Type? requestType = null) where T : class
	{
		Type source = typeof(T);
		Type output = typeof(TOutput);

		List<ChromaRouteAttribute> attributes = source.GetCustomAttributes(inherit: false)
			.OfType<ChromaRouteAttribute>()
			.Where(x => requestType is null || x.RequestType == requestType)
			.Where(x => x.Source == source && x.ResponseType == output && x.Method.Equals(method))
			.ToList();

		if (attributes is [])
			throw new ChromaDBGeneralException($"The requested type {source} doesn't have any ChromaRoute attributes associated which are matching the requested output type {output}.");

		ChromaRouteAttribute lastAssigned = attributes[^1];
		return (lastAssigned.Source, lastAssigned.ResponseType, lastAssigned.Endpoint, lastAssigned.Method, lastAssigned.QueryParams);
	}
}
