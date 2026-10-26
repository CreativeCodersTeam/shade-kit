namespace CreativeCoders.ShadeKit.Tests.Shared;

/// <summary>
/// Answers HTTP requests with responses that a test supplies, so that code that uses <see cref="HttpClient" /> can be
/// tested without a network.
/// </summary>
/// <param name="sendAsync">
/// The function that answers a request; it gets the request and the token that cancels it.
/// </param>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="StubHttpMessageHandler" /> class that answers every request with
    /// a response that the specified function creates.
    /// </summary>
    /// <param name="createResponse">The function that creates the response for a request.</param>
    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> createResponse)
        : this((request, _) => Task.FromResult(createResponse(request)))
    {
    }

    /// <summary>
    /// Gets the requests that the handler answered, in the order in which they were sent.
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    /// <summary>
    /// Creates an <see cref="HttpClient" /> that sends its requests to this handler and does not dispose it.
    /// </summary>
    public HttpClient CreateClient()
    {
        return new HttpClient(this, disposeHandler: false);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);

        return sendAsync(request, cancellationToken);
    }
}
