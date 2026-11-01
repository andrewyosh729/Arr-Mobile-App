using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ArrApp.Core.Api;

/// <summary>
/// Shared HTTP plumbing for Sonarr and Radarr (<c>/api/v3</c>, <c>X-Api-Key</c>). Every expected failure
/// surfaces as an <see cref="ArrApiException"/> with a user-safe message; caller cancellation surfaces as
/// <see cref="OperationCanceledException"/>.
/// </summary>
public sealed class ArrHttpClient(HttpClient http, string baseUrl, string apiKey, TimeSpan? timeout = null) {
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public async Task<T> GetAsync<T>(string path, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default) {
        if (!ArrUrl.TryNormalize(baseUrl, out var root, out var urlError))
            throw new ArrApiException(ArrErrorKind.InvalidUrl, urlError!);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{root}/api/v3/{path.TrimStart('/')}");
        request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout ?? DefaultTimeout);

        try {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);

            switch (response.StatusCode) {
                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    throw ArrApiException.For(ArrErrorKind.Unauthorized);
                case HttpStatusCode.NotFound:
                    throw ArrApiException.For(ArrErrorKind.NotFound);
                case >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest:
                    // Redirects are deliberately not followed: they would carry the API key to wherever they point
                    // (for example an SSO login on another domain). The usual cause is an http -> https redirect.
                    throw new ArrApiException(ArrErrorKind.InvalidResponse,
                        "The server redirected the request (often from http:// to https://). Use the address it redirects to.");
                case >= HttpStatusCode.InternalServerError:
                    throw ArrApiException.For(ArrErrorKind.ServerError, statusCode: (int)response.StatusCode);
                case not HttpStatusCode.OK:
                    throw ArrApiException.For(ArrErrorKind.InvalidResponse);
            }

            var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            try {
                return JsonSerializer.Deserialize(body, typeInfo) ?? throw ArrApiException.For(ArrErrorKind.InvalidResponse);
            } catch (JsonException ex) {
                // Typically an HTML page: a reverse-proxy login, or the wrong port/URL base.
                throw ArrApiException.For(ArrErrorKind.InvalidResponse, ex);
            }
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            throw ArrApiException.For(ArrErrorKind.Timeout);
        } catch (HttpRequestException ex) {
            throw ArrApiException.For(ex.HttpRequestError == HttpRequestError.SecureConnectionError ? ArrErrorKind.Tls : ArrErrorKind.Unreachable, ex);
        }
    }
}
