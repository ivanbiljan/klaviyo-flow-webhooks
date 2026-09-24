using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests;

/// <summary>
///     Replays payloads signed the way Klaviyo signs them. No tunnel or Klaviyo account needed, so it runs in CI.
///     Replace the fixture with a real body captured from the sandbox once you have one.
/// </summary>
public sealed class ReceiveKlaviyoWebhookTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Route = "/webhooks/klaviyo";
    private const string Secret = "test-secret-0123456789";
    private const string WebhookId = "a8b890458b4bbfaa26d961471b83c101d6de23bd826e7e5173a15310985ec3cb";

    private static Task<string> Fixture()
    {
        return File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "unsubscribed_from_email_marketing.json")
        );
    }

    [Fact]
    public async Task Accepts_correctly_signed_request()
    {
        var response = await Send(await Fixture(), DateTimeOffset.UtcNow);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_missing_signature()
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = new StringContent(await Fixture(), Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_stale_timestamp()
    {
        var response = await Send(await Fixture(), DateTimeOffset.UtcNow.AddHours(-2));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_tampered_body()
    {
        var body = await Fixture();
        var timestamp = DateTimeOffset.UtcNow.ToString("R");
        var signature = KlaviyoSignatureMiddleware.ComputeSignature(Encoding.UTF8.GetBytes(body), timestamp, Secret);

        var tampered = body.Replace("01JTESTPROFILE0000000000", "01JSOMEONEELSE0000000000");
        var response = await Send(tampered, timestamp, signature, WebhookId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_webhook_id_header_that_does_not_match_meta()
    {
        var body = await Fixture();
        var timestamp = DateTimeOffset.UtcNow.ToString("R");
        var signature = KlaviyoSignatureMiddleware.ComputeSignature(Encoding.UTF8.GetBytes(body), timestamp, Secret);

        var response = await Send(body, timestamp, signature, "some-other-webhook");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient CreateClient()
    {
        return factory.WithWebHostBuilder(b => b.UseSetting("Klaviyo:WebhookSecret", Secret)).CreateClient();
    }

    private Task<HttpResponseMessage> Send(string body, DateTimeOffset sentAt)
    {
        var timestamp = sentAt.ToString("R"); // "Thu, 24 Sep 2026 00:50:01 GMT", like Klaviyo's example
        var signature = KlaviyoSignatureMiddleware.ComputeSignature(Encoding.UTF8.GetBytes(body), timestamp, Secret);

        return Send(body, timestamp, signature, WebhookId);
    }

    private async Task<HttpResponseMessage> Send(string body, string timestamp, string signature, string webhookId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Add(KlaviyoSignatureMiddleware.SignatureHeader, signature);
        request.Headers.Add(KlaviyoSignatureMiddleware.TimestampHeader, timestamp);
        request.Headers.Add(KlaviyoSignatureMiddleware.WebhookIdHeader, webhookId);

        return await CreateClient().SendAsync(request);
    }
}