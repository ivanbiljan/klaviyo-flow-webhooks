using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Api.Features.KlaviyoConsent;

public sealed class KlaviyoWebhookOptions
{
    public const string Section = "Klaviyo";

    public string WebhookSecret { get; set; } = "";
}

internal sealed class KlaviyoWebhookSecretFilter(IOptionsMonitor<KlaviyoWebhookOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-Klaviyo-Webhook-Secret";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var provided = Encoding.UTF8.GetBytes(context.HttpContext.Request.Headers[HeaderName].ToString());
        var expected = Encoding.UTF8.GetBytes(options.CurrentValue.WebhookSecret);

        return CryptographicOperations.FixedTimeEquals(provided, expected)
            ? next(context)
            : ValueTask.FromResult<object?>(TypedResults.Unauthorized());
    }
}