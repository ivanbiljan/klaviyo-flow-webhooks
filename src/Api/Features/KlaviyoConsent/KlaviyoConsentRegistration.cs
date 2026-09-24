namespace Api.Features.KlaviyoConsent;

public static class KlaviyoConsentRegistration
{
    public static IServiceCollection AddKlaviyoConsent(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KlaviyoWebhookOptions>(configuration.GetSection(KlaviyoWebhookOptions.Section));

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}