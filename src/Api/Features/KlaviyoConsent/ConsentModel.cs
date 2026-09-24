namespace Api.Features.KlaviyoConsent;

public enum ConsentChangeType
{
    EmailSubscribed,
    EmailUnsubscribed,
    SmsSubscribed,
    SmsUnsubscribed
}

public enum ConsentChannel
{
    Email,
    Sms
}

public sealed record ConsentChange(
    ConsentChangeType Type,
    string KlaviyoProfileId,
    string? Email,
    string? Phone,
    DateTimeOffset ReceivedAt
)
{
    public ConsentChannel Channel => Type is ConsentChangeType.EmailSubscribed or ConsentChangeType.EmailUnsubscribed
        ? ConsentChannel.Email
        : ConsentChannel.Sms;

    public bool Subscribed => Type is ConsentChangeType.EmailSubscribed or ConsentChangeType.SmsSubscribed;
}