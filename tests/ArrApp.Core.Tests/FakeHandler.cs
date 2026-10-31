using System.Net;
using System.Text;

namespace ArrApp.Core.Tests;

public sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler {
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Requests.Add(request);
        return respond(request, cancellationToken);
    }

    public static FakeHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new((_, _) => Task.FromResult(Response(body, status)));

    public static FakeHandler Throw(Exception ex) => new((_, _) => throw ex);

    public static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
