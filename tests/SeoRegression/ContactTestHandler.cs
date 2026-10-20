using System.Net;

// In-memory transport only. Never contacts Telegram or sends a real message.
public sealed class ContactTestHandler : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string Payload { get; set; } = "{\"ok\":true}";
    public bool FailNetwork { get; set; }
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        if (FailNetwork) throw new HttpRequestException("Synthetic transport failure");
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Payload) });
    }
}
