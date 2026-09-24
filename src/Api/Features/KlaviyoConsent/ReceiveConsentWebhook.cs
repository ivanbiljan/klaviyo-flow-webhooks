using Immediate.Apis.Shared;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Features.KlaviyoConsent;

[Handler]
[MapPost("/webhooks/klaviyo/consent")]
[AllowAnonymous]
public static partial class ReceiveConsentWebhook
{
    internal static void CustomizeEndpoint(RouteHandlerBuilder endpoint)
    {
        endpoint
            .AddEndpointFilter<KlaviyoWebhookSecretFilter>()
            .WithTags("Klaviyo")
            .ExcludeFromDescription();
    }

    private static async ValueTask<NoContent> HandleAsync(
        Command command,
        TimeProvider clock,
        ILogger<Command> logger,
        CancellationToken token
    )
    {
        // Klaviyo renders missing template values as ""
        if (string.IsNullOrWhiteSpace(command.KlaviyoProfileId))
        {
            logger.LogWarning("Klaviyo consent webhook without profile id ({Type}); ignoring", command.Type);

            return TypedResults.NoContent();
        }

        var change = new ConsentChange(
            command.Type,
            command.KlaviyoProfileId,
            NullIfEmpty(command.Email),
            NullIfEmpty(command.Phone),
            clock.GetUtcNow()
        );

        // Publish to queue

        using (logger.BeginScope(new Dictionary<string, object> {["@Payload"] = change}))
        {
            logger.LogInformation(
                "Queued {Type} for Klaviyo profile {ProfileId}",
                change.Type,
                change.KlaviyoProfileId
            );
        }

        return TypedResults.NoContent();
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public sealed record Command(
        ConsentChangeType Type,
        string? KlaviyoProfileId,
        string? Email,
        string? Phone
    );

    public sealed record Response(bool Queued);
}