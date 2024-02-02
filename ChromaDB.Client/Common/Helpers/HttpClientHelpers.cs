using ChromaDB.Client.Common.Attributes;
using ChromaDB.Client.Common.Exceptions;
using ChromaDB.Client.Models.Requests;
using ChromaDB.Client.Models.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChromaDB.Client.Common.Helpers
{
	public static class HttpClientHelpers
	{
		public static async Task<BaseResponse<TResponse>> Get<TSource, TResponse>(this HttpClient httpClient, RequestQueryParams? queryParams = null) where TSource : class
		{
			BaseResponse<TResponse> response = default!;
			(Type Source, Type Response, string Endpoint, HttpMethod Method, IReadOnlyList<string> QueryArgs) = GetRouteDetailsByType<TSource, TResponse>();
			try
			{
				string formattedEndpoint = ValidateAndPrepareEndpoint<TSource>(QueryArgs, Endpoint, queryParams);

				using HttpRequestMessage httpRequestMessage = new(Method, requestUri: formattedEndpoint);
				using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

				string? responseBody = httpResponseMessage.IsSuccessStatusCode switch
				{
					true => await httpResponseMessage.Content.ReadAsStringAsync(),
					false when httpResponseMessage.StatusCode == HttpStatusCode.BadRequest || httpResponseMessage.StatusCode == HttpStatusCode.UnprocessableContent
						|| httpResponseMessage.StatusCode == HttpStatusCode.InternalServerError
						=> throw new ChromaDBException(httpResponseMessage.ReasonPhrase, null, httpResponseMessage.StatusCode)
					{
						ErrorMessageBody = await httpResponseMessage.Content.ReadAsStringAsync()
					},
					_ => throw new ChromaDBException(httpResponseMessage.ReasonPhrase, null, httpResponseMessage.StatusCode)
				};

				if (string.IsNullOrEmpty(responseBody))
				{
					response = new BaseResponse<TResponse>(default, statusCode: httpResponseMessage.StatusCode);
				}
				else
				{
					TResponse responsePayload = await Task.Run(() => JsonSerializer.Deserialize<TResponse>(responseBody)!);
					response = new BaseResponse<TResponse>(responsePayload, statusCode: httpResponseMessage.StatusCode);
				}
			}
			catch (ChromaDBGeneralException ex)
			{
				response = new BaseResponse<TResponse>(default, statusCode: HttpStatusCode.InternalServerError, reasonPhrase: ex.Message);
			}
			catch (ChromaDBException ex)
			{
				response = string.IsNullOrEmpty(ex.ErrorMessageBody) switch
				{
					false => new BaseResponse<TResponse>(default, statusCode: ex.StatusCode!.Value, reasonPhrase: ParseErrorMessageBody(ex.ErrorMessageBody)),
					_ => new BaseResponse<TResponse>(default, statusCode: ex.StatusCode!.Value)
				};
			}
			catch (Exception ex)
			{
				// Decided on ServiceUnavailable error for all other exception types, we'll pass the exception message forward
				response = new BaseResponse<TResponse>(default, statusCode: HttpStatusCode.ServiceUnavailable, reasonPhrase: ex.Message);
			}
			return response;
		}

		private static string? ParseErrorMessageBody(string errorMessageBody)
		{
			try
			{
				GeneralError deserialized = JsonSerializer.Deserialize<GeneralError>(errorMessageBody)!;
				Match match = Regex.Match(deserialized?.ErrorMessage ?? string.Empty,
					"\\('(?<errorMessage>.*)'\\)",
					RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

				if (!match.Success || match.Groups.Count < 1)
					return $"Couldn't identify the error message: {deserialized?.ErrorMessage}";

				return match.Groups["errorMessage"]?.Value;
			}
			catch
			{
				return $"Couldn't parse the incoming error message body: {errorMessageBody}";
			}
		}

		private static string ValidateAndPrepareEndpoint<TSource>(IReadOnlyList<string> QueryArgs, string Endpoint, RequestQueryParams? queryParams = null) where TSource : class
		{
			if (QueryArgs.Count > 0)
			{
				string queryArgsString = string.Join(",", QueryArgs);
				if (queryParams == null)
					throw new ChromaDBGeneralException(message: $"Type {typeof(TSource)} requires the following arguments to be provided: [{queryArgsString}], but argument {nameof(queryParams)} is null!", null);

				IEnumerable<string> nonProvidedQueryArgs = QueryArgs
					.Except(QueryArgs.Where(a => queryParams.HasKeyInvariant(a)));

				if (nonProvidedQueryArgs.Count() > 0)
				{
					string nonProvidedQueryArgsString = string.Join(",", nonProvidedQueryArgs);
					throw new ChromaDBGeneralException($"Type {typeof(TSource)} requires the following arguments to be provided: [{queryArgsString}], but {nonProvidedQueryArgsString} weren't provided!");
				}
				
				return FormatRequestUri(Endpoint, queryParams!);
			}

			return Endpoint;
		}

		private static string FormatRequestUri(string endpoint, RequestQueryParams queryParams)
		{
			string formattedEndpoint = new string(endpoint);
			foreach (KeyValuePair<string, string> entry in queryParams.Build())
			{
				string urlEncodedQueryParam = Uri.EscapeDataString(entry.Value);
				formattedEndpoint = formattedEndpoint.Replace(entry.Key, urlEncodedQueryParam);
			}
			return formattedEndpoint;
		}

		public static (Type Source, Type Response, string Endpoint, HttpMethod Method, IReadOnlyList<string> QueryArgs) GetRouteDetailsByType<T, TOutput>() where T : class
		{
			Type source = typeof(T);
			Type output = typeof(TOutput);

			List<ChromaRouteAttribute> attributes = source.GetCustomAttributes(inherit: false)
				.Where(x => x.GetType() == typeof(ChromaRouteAttribute))
				.Select(x => (ChromaRouteAttribute)x)
				.Where(x => x.Source == source && x.ResponseType == output)
				.ToList();

			if (attributes.Count < 1)
				throw new ChromaDBGeneralException($"The requested type {source} doesn't have any ChromaRoute attributes associated which are matching the requested output type {output}!");

			var lastAssigned = attributes.Last();
			return (lastAssigned.Source, lastAssigned.ResponseType, lastAssigned.TargetEndpoint, lastAssigned.Method(), lastAssigned.QueryParams());
		}
	}
}
