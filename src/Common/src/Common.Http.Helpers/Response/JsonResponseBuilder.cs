using System.Net;
using System.Net.Mime;
using System.Text.Json;
using Common.Http.Response.Structures;
using Microsoft.AspNetCore.Http;

namespace Common.Http.Response
{
    /// <summary>
    /// Builds JSON HTTP responses to standardize the response format across endpoints.
    /// </summary>
    public static class JsonResponseBuilder
    {
        public const string DataKey = "data";
        public const string MetadataKey = "metadata";
        public const string ErrorsKey = "errors";

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        /// <summary>
        /// Creates a success response with optional metadata.
        /// </summary>
        /// <param name="statusCode">Status code of the response</param>
        /// <param name="metadata">Metadata to include in the response</param>
        /// <returns>Returns a success response.</returns>
        public static IResult Success(HttpStatusCode statusCode, object? metadata = null)
        {
            if (statusCode == HttpStatusCode.NoContent)
                return Results.NoContent();

            var body = new Dictionary<string, object?>();

            if (metadata != null) body.Add(MetadataKey, metadata);

            return Results.Json(body, _jsonOptions, MediaTypeNames.Application.Json, (int)statusCode);
        }

        /// <summary>
        /// Creates a success response with given data and optional metadata.
        /// </summary>
        /// <param name="statusCode">Status code of the response</param>
        /// <param name="data">Data to include in the response</param>
        /// <param name="metadata">Metadata to include in the response</param>
        /// <returns>Returns a success response.</returns>
        public static IResult SuccessWithData(HttpStatusCode statusCode, object? data, object? metadata = null)
        {
            var body = new Dictionary<string, object?>();

            if (data != null) body.Add(DataKey, data);
            if (metadata != null) body.Add(MetadataKey, metadata);

            return Results.Json(body, _jsonOptions, MediaTypeNames.Application.Json, (int)statusCode);
        }

        /// <summary>
        /// Creates an error response with given errors and optional metadata.
        /// </summary>
        /// <param name="statusCode">Status code of the response</param>
        /// <param name="errors">Errors to include in the response</param>
        /// <param name="metadata">Metadata to include in the response</param>
        /// <returns>Returns an error response.</returns>
        public static IResult Error(HttpStatusCode statusCode, IEnumerable<ErrorItem> errors, object? metadata = null)
        {
            var body = new Dictionary<string, object?>();

            if (errors != null) body.Add(ErrorsKey, errors);
            if (metadata != null) body.Add(MetadataKey, metadata);

            return Results.Json(body, _jsonOptions, MediaTypeNames.Application.Json, (int)statusCode);
        }
    }
}
