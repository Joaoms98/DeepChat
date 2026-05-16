using System.Text;
using Galileo.Chat.Infrastructure;
using Galileo.Chat.Infrastructure.Auth;
using Galileo.Chat.Infrastructure.Options;
using Galileo.Chat.Infrastructure.Persistence;
using Galileo.Chat.Server.BackgroundServices;
using Galileo.Chat.Server.Configuration;
using Galileo.Chat.Server.Endpoints;
using Galileo.Chat.Server.Hubs;
using Galileo.Chat.Server.Middleware;
using Galileo.Chat.Server.Presence;
using Galileo.Chat.Shared.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ----- Logging (Serilog) -----
// NEVER include message ciphertext, plaintext, JWTs, or password hashes in logs.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/deepchat-.log", rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();
builder.Host.UseSerilog();

// ----- Infrastructure (DB, repos, hasher, token service, handlers) -----
builder.Services.AddInfrastructure(builder.Configuration);

// ----- Network options (IP whitelist) -----
builder.Services.AddOptions<NetworkOptions>()
    .Bind(builder.Configuration.GetSection(NetworkOptions.SectionName))
    .ValidateOnStart();

// ----- Authentication (JWT Bearer) -----
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var issuer = jwtSection["Issuer"] ?? "DeepChat.Server";
var audience = jwtSection["Audience"] ?? "DeepChat.Client";
var secret = jwtSection["SecretKey"]
             ?? throw new InvalidOperationException(
                 "JWT secret not configured. Set Jwt:SecretKey or env DEEPCHAT_JWT_SECRET.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(JwtTokenService.ResolveKey(secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // SignalR hands tokens via the access_token query string at handshake
        // because browsers can't set Authorization headers on the upgrade request.
        // We accept it ONLY on the /hubs/* path.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ----- Rate limiting (anti-flood) -----
// Token bucket per remote IP: 30 requests / 10s sustained, with a small burst
// allowance. Login endpoint gets a stricter bucket to slow down brute force.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("default", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.TokenBucketRateLimiterOptions
            {
                TokenLimit = 60,
                TokensPerPeriod = 30,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                AutoReplenishment = true,
                QueueLimit = 0
            }));

    options.AddPolicy("login", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// ----- SignalR + presence -----
builder.Services.AddSignalR(opt =>
{
    opt.MaximumReceiveMessageSize = 96 * 1024;     // 64KB ciphertext + DTO overhead
    opt.EnableDetailedErrors = builder.Environment.IsDevelopment();
    opt.KeepAliveInterval = TimeSpan.FromSeconds(15);
    opt.ClientTimeoutInterval = TimeSpan.FromSeconds(40);
}).AddMessagePackProtocol();

builder.Services.AddSingleton<IPresenceTracker, InMemoryPresenceTracker>();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, SubClaimUserIdProvider>();

// ----- Retention (24h purge BackgroundService) -----
builder.Services.AddOptions<RetentionOptions>()
    .Bind(builder.Configuration.GetSection(RetentionOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddHostedService<MessagePurgeService>();

var app = builder.Build();

// ----- Database initialization (Migrate + PRAGMAs) -----
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.UseSerilogRequestLogging(opt =>
{
    opt.GetLevel = (httpCtx, _, ex) => ex is not null
        ? Serilog.Events.LogEventLevel.Error
        : Serilog.Events.LogEventLevel.Information;
});

// Order matters: IP whitelist runs first so unauthorized hosts can't even
// reach the auth pipeline. Then JWT auth + authorization.
app.UseIpWhitelist();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ----- Routes -----
app.MapAuthEndpoints();
app.MapBootstrapEndpoints();
app.MapRoomsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .AllowAnonymous();

app.MapHub<ChatHub>(ProtocolConstants.ChatHubPath);

await app.RunAsync();

// Make Program reachable to WebApplicationFactory<Program> in tests.
public partial class Program { }
