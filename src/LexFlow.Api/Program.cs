using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using LexFlow.Api.Hubs;
using LexFlow.Api.Logging;
using LexFlow.Api.Middleware;
using LexFlow.Api.Security;
using LexFlow.Api.Services;
using LexFlow.Application;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure;
using LexFlow.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

const string ServiceName = "LexFlow.Api";

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog -> OpenTelemetry -> Application Insights, PRD §29) ---
// No request/response bodies are logged by UseSerilogRequestLogging below (it only
// emits method/path/status/duration), and SensitiveDataDestructuringPolicy redacts
// password/token/secret/narrative-shaped properties on anything that IS destructured
// (e.g. exception data, manually logged objects) — together this satisfies §29's
// "no bodies logged by default; sensitive fields ... never logged".
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.With(new ActivityEnricher())
    .Destructure.With(new SensitiveDataDestructuringPolicy())
    .WriteTo.Console());

// --- Services ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ICurrentPortalUserService, CurrentPortalUserService>();

builder.Services.AddControllers();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- Hangfire client (Module 2 CSV/XLSX import pipeline, AC-L5) ---
// The API process only enqueues jobs (BackgroundJob.Enqueue<ILeadImportService>(...) in
// LeadImportService); LexFlow.Workers hosts the actual Hangfire server that executes them
// against this same Postgres-backed storage.
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options
        .UseNpgsqlConnection(builder.Configuration.GetConnectionString("LexFlowDatabase"))));

// --- AuthN/AuthZ (PRD §20: JWT RS256, audiences staff/portal/apikey; §21: permission policies) ---
// Module 17 Security: "complete identity separation from staff (different Identity tenant +
// JWT audience + cookie domain)." A "portal"-audience token must never validate against a
// staff endpoint and vice versa — two named schemes, each restricted to its own audience
// allowlist, rather than one scheme accepting all three audiences (the prior single-scheme
// setup let a portal token pass a staff [Authorize] check, since ASP.NET Core's JWT handler
// only validates that the token's audience is *one of* ValidAudiences, it doesn't expose
// "which one" to route-level authorization on its own).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(PortalAuthenticationDefaults.AuthenticationScheme);

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSigningKeyProvider, Microsoft.Extensions.Options.IOptions<JwtOptions>>((jwtBearerOptions, keyProvider, jwtOptions) =>
    {
        // JwtTokenService issues plain "sub"/"tenant"/"branch"/"perm" claim types; the
        // handler's default inbound claim mapping would silently rename "sub" to the long
        // ClaimTypes.NameIdentifier URI (and similar for a few others) before ICurrentUserService
        // ever sees it, so every FindFirst("sub")/FindFirst("tenant") lookup — including the
        // denylist check right below — would come back null despite a perfectly valid token.
        jwtBearerOptions.MapInboundClaims = false;
        jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Value.Issuer,
            ValidateAudience = true,
            ValidAudiences = new[] { "staff", "apikey" },
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keyProvider.ValidationKey,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // AC-U1: "deactivated user's active JWTs rejected ≤ 60 s (denylist check)".
        // A 15-min access token would otherwise keep working until it naturally
        // expires; this re-checks a Redis-backed denylist on every request.
        jwtBearerOptions.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sub = context.Principal?.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                {
                    return;
                }

                var denylistService = context.HttpContext.RequestServices.GetRequiredService<IUserDenylistService>();
                if (await denylistService.IsDenylistedAsync(userId, context.HttpContext.RequestAborted))
                {
                    context.Fail("User has been deactivated.");
                }
            },
        };
    });

builder.Services.AddOptions<JwtBearerOptions>(PortalAuthenticationDefaults.AuthenticationScheme)
    .Configure<JwtSigningKeyProvider, Microsoft.Extensions.Options.IOptions<JwtOptions>>((jwtBearerOptions, keyProvider, jwtOptions) =>
    {
        jwtBearerOptions.MapInboundClaims = false;
        jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Value.Issuer,
            ValidateAudience = true,
            ValidAudiences = new[] { "portal" },
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keyProvider.ValidationKey,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// Module 2 Edge Cases: "web-to-lead spam (honeypot + captcha + per-IP throttle)" — the
// per-IP throttle half of that, applied to the one unauthenticated write endpoint in this API.
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("web-to-lead", limiterOptions =>
{
    limiterOptions.PermitLimit = 10;
    limiterOptions.Window = TimeSpan.FromMinutes(1);
    limiterOptions.QueueLimit = 0;
}));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "LexFlow API",
        Version = "v1",
        Description = "LexFlow Enterprise Lawyer CRM — REST API (envelope per PRD §17). " +
            "Bearer tokens come from two distinct audiences that are never interchangeable: " +
            "\"staff\" (POST /api/v1/auth/login) for every /api/v1/* route, and \"portal\" " +
            "(POST /api/portal/v1/auth/login) for every /api/portal/v1/* route (PRD Module 17).",
    });

    // PRD §16: tag every operation with its API-List "Area" (e.g. all six Comm* and six Kb*
    // controllers collapse to "Comm"/"KB") rather than one tag per controller class.
    options.TagActionsBy(LexFlow.Api.Swagger.SwaggerAreaTags.Resolve);
    options.OrderActionsBy(api => $"{string.Join(",", LexFlow.Api.Swagger.SwaggerAreaTags.Resolve(api))}_{api.RelativePath}");

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "JWT access token issued by /api/v1/auth/login (staff) or /api/portal/v1/auth/login (portal).",
    });
    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document, externalResource: null)] = [],
    });

    var xmlDocPath = Path.Combine(AppContext.BaseDirectory, $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlDocPath))
    {
        options.IncludeXmlComments(xmlDocPath, includeControllerXmlComments: true);
    }
});

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy("LexFlowClients", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();

        // Dev convenience: Angular's dev server port varies (ng serve --port, multiple
        // apps/instances running side by side), so accept any localhost/127.0.0.1 origin
        // rather than hardcoding one. AllowAnyOrigin() can't be combined with
        // AllowCredentials() per the CORS spec, hence the loopback predicate instead of a
        // wildcard. Never applies outside Development — non-dev origins still come only
        // from Cors:AllowedOrigins.
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback);
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }
    });

    // Module 17 Security: "complete identity separation from staff" extends to origins —
    // the portal-web app is a distinct Angular app on its own origin(s), never bundled with
    // the staff app's allowed-origins list.
    var portalAllowedOrigins = builder.Configuration.GetSection("Cors:PortalAllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy("LexFlowPortal", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();

        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback);
        }
        else
        {
            policy.WithOrigins(portalAllowedOrigins);
        }
    });
});

builder.Services.AddSignalR();
builder.Services.AddScoped<IChatBroadcaster, SignalRChatBroadcaster>();
builder.Services.AddScoped<IJobsBroadcaster, SignalRJobsBroadcaster>();

var dbConnectionString = builder.Configuration.GetConnectionString("LexFlowDatabase");
var healthChecksBuilder = builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(dbConnectionString))
{
    healthChecksBuilder.AddNpgSql(dbConnectionString, name: "postgres");
}

healthChecksBuilder.AddRedis(builder.Configuration.GetSection("Redis")["ConnectionString"] ?? "localhost:6379", name: "redis");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(ServiceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317")));

var app = builder.Build();

// --- Pipeline ---
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();

// The raw document at /swagger/v1/swagger.json is available in every environment — the CI
// TypeScript-client-generation step (see README's "Generated TypeScript client" section) and
// contract tests (§37) fetch it from a running instance. The interactive UI at /swagger is
// withheld in Production only, since it's a browsing/exploration surface, not a machine
// contract consumer.
app.UseSwagger(options => options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1);
if (!app.Environment.IsProduction())
{
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("LexFlowClients");

app.UseAuthentication();
app.UseMiddleware<TenantScopingMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Hangfire Dashboard, gated behind "settings.manage.all" (same permission SettingsController
// requires) via HangfireDashboardAuthorizationFilter — never behind bare authentication alone,
// since the dashboard can requeue/delete background jobs across every tenant.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthorizationFilter()],
});

app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<PresenceHub>("/hubs/presence");
app.MapHub<JobsHub>("/hubs/jobs");

app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposed for WebApplicationFactory in LexFlow.E2ETests / LexFlow.IntegrationTests.</summary>
public partial class Program;
