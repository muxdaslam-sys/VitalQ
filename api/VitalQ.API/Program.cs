using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using VitalQ.API.BackgroundServices;
using VitalQ.API.Hubs;
using VitalQ.API.Services;
using VitalQ.BusinessLogic.Common;
using VitalQ.BusinessLogic.Interfaces;
using VitalQ.BusinessLogic.Services;
using VitalQ.DataAccess;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context (Pooled for high-concurrency 100k+ throughput)
builder.Services.AddDbContextPool<VitalQDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 1.1 In-Memory Caching for hospital metadata
builder.Services.AddMemoryCache();

// 2. DYNAMIC CORS (Production-Ready: configurable origins with localhost fallback)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200", "http://127.0.0.1:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularApp", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // REQUIRED for HttpOnly cookies and SignalR
    });
});

// 2.1 NATIVE RATE LIMITING (DDoS & Brute-Force Shield for Login Endpoint)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("LoginLimiter", httpContext =>
    {
        var rawForwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var clientIp = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
            ?? (!string.IsNullOrWhiteSpace(rawForwarded) ? rawForwarded.Split(',')[0].Trim() : null)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

// 3. JWT Authentication Setup
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };

        // For SignalR WebSockets: reads access_token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.Request.Path.StartsWithSegments("/hubs/queue"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// 4. Core Infrastructure & SignalR Notifications
builder.Services.AddScoped<IQueueNotificationService, QueueNotificationService>();

// 5. Role-Based & Domain Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPublicService, PublicService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<INurseService, NurseService>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// 6. SignalR & Background Aging Worker
builder.Services.AddSignalR();
builder.Services.AddHostedService<AgingWorker>();

// 6.1 Native Health Checks (Database Readiness & Container Liveness)
builder.Services.AddHealthChecks()
    .AddCheck<VitalQ.API.Health.DatabaseHealthCheck>("sql_server", tags: new[] { "ready" });

// 7. Controllers & JSON cycle handling
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// 8. Swagger with JWT Bearer Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "VitalQ API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT token like: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 8.0 Asynchronous Database Connection Pool Priming & EF Core Query Compilation Warmup
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VitalQDbContext>();
        _ = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == "__warmup__");
    }
    catch
    {
        // Non-blocking opportunistic startup warmup
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 8.1 Global Production Exception Shield (prevents raw SQL/stack trace leakage to clients)
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            message = "An unexpected server error occurred. Please contact Clinical IT Support (Ext: 4400)."
        });
    });
});

// 9. Middleware Pipeline Order
app.UseCors("AngularApp");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<QueueHub>("/hubs/queue");

// 10. Health Check Probes (Docker / Kubernetes / Hospital IT Monitoring)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Instant ping (proves .NET process is alive and responsive)
});

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (httpContext, report) =>
    {
        httpContext.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            timestampUtc = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                component = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 2),
                description = e.Value.Description,
                data = e.Value.Data
            })
        };
        await httpContext.Response.WriteAsJsonAsync(response);
    }
});

app.Run();
