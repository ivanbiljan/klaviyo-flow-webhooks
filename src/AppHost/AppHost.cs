using Microsoft.Extensions.Configuration;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets(typeof(Program).Assembly);

var webhookSecret = builder.AddParameter("klaviyo-webhook-secret", true);

var api = builder.AddProject<Api>("api")
    .WithEnvironment("Klaviyo__WebhookSecret", webhookSecret);

var tunnelId = builder.Configuration["Tunnel:DevTunnelId"]
               ?? throw new InvalidOperationException("Set Tunnel:DevTunnelId in AppHost user secrets.");

builder.AddDevTunnel("klaviyo-tunnel", tunnelId)
    .WithReference(api.GetEndpoint("http"))
    .WithAnonymousAccess();

builder.Build().Run();