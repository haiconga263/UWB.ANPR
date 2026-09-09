using Microsoft.Extensions.DependencyInjection.Extensions;
using UWB.ANPR.Abstractions.Ports;
using UWB.ANPR.Api.Configuration;
using UWB.ANPR.Api.Endpoints;
using UWB.ANPR.Api.Placeholders;
using UWB.ANPR.Api.Security;
using UWB.ANPR.Hikvision;
using UWB.ANPR.Ingestion;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<AnprCameraOptions>()
    .Bind(builder.Configuration.GetSection(AnprCameraOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<WebhookSecurityOptions>()
    .Bind(builder.Configuration.GetSection(WebhookSecurityOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHikvisionCameraClient();
builder.Services.AddAnprIngestion(
    builder.Configuration.GetValue("Anpr:QueueCapacity", 10_000));

builder.Services.TryAddSingleton<IWebhookAuthenticator, WebhookAuthenticator>();

// ---------------------------------------------------------------------------
// PLACEHOLDER: thay 5 đăng ký dưới đây bằng module database + parser thật.
// ---------------------------------------------------------------------------
builder.Services.TryAddSingleton<IRawEventStore, InMemoryRawEventStore>();
builder.Services.TryAddSingleton<IWatermarkStore, InMemoryWatermarkStore>();
builder.Services.TryAddSingleton<ICameraRegistry, ConfigurationCameraRegistry>();
builder.Services.TryAddSingleton<ICameraCredentialResolver, ConfigurationCredentialResolver>();
builder.Services.TryAddSingleton<IHikvisionPayloadParser, StubPayloadParser>();
// ---------------------------------------------------------------------------

builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapAnprWebhookEndpoints();
app.MapHealthChecks("/health");

await app.RunAsync();
