using System.Net;
using System.Text;
using System.Threading.Channels;
using Api.Features.KlaviyoConsent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
///     Replays payloads captured from the sandbox (drop real ones into Fixtures/). No tunnel or Klaviyo needed, so it runs
///     in CI.
/// </summary>
public sealed class ReceiveConsentWebhookTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Route = "/webhooks/klaviyo/consent";
    private const string Secret = "test-secret-0123456789";

    private static async Task<StringContent> Fixture(string name)
    {
        return new StringContent(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)),
            Encoding.UTF8,
            "application/json"
        );
    }

    [Fact]
    public async Task Acks_and_processes_unsubscribe_in_background()
    {
        var (client, store) = CreateClient();
        client.DefaultRequestHeaders.Add(KlaviyoWebhookSecretFilter.HeaderName, Secret);

        var response = await client.PostAsync(Route, await Fixture("email_unsubscribed.json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var applied = await store.Applied.Reader.ReadAsync(cts.Token);

        Assert.Equal(ConsentChangeType.EmailUnsubscribed, applied.Type);
        Assert.Equal(ConsentChannel.Email, applied.Channel);
        Assert.False(applied.Subscribed);
        Assert.Equal("01JTESTPROFILE0000000000", applied.KlaviyoProfileId);
        Assert.Null(applied.Phone); // "" from the Klaviyo template is normalized
    }

    [Fact]
    public async Task Acks_payload_without_profile_id_but_does_not_queue_it()
    {
        var (client, store) = CreateClient();
        client.DefaultRequestHeaders.Add(KlaviyoWebhookSecretFilter.HeaderName, Secret);

        var body = new StringContent(
            """{ "type": "sms_subscribed", "klaviyoProfileId": "", "email": "", "phone": "" }""",
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync(Route, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Task.Delay(200);
        Assert.False(store.Applied.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Rejects_request_with_wrong_secret()
    {
        var (client, _) = CreateClient();
        client.DefaultRequestHeaders.Add(KlaviyoWebhookSecretFilter.HeaderName, "nope");

        var response = await client.PostAsync(Route, await Fixture("email_unsubscribed.json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_request_without_secret()
    {
        var (client, _) = CreateClient();

        var response = await client.PostAsync(Route, await Fixture("email_unsubscribed.json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private (HttpClient Client, RecordingConsentStore Store) CreateClient()
    {
        var store = new RecordingConsentStore();

        var client = factory
            .WithWebHostBuilder(b =>
                {
                    b.UseSetting("Klaviyo:WebhookSecret", Secret);
                    b.ConfigureTestServices(s => s.AddSingleton<IConsentStore>(store));
                }
            )
            .CreateClient();

        return (client, store);
    }

    private sealed class RecordingConsentStore : IConsentStore
    {
        public Channel<ConsentChange> Applied { get; } = Channel.CreateUnbounded<ConsentChange>();

        public ValueTask<bool> ApplyAsync(ConsentChange change, CancellationToken token)
        {
            Applied.Writer.TryWrite(change);

            return ValueTask.FromResult(true);
        }
    }
}