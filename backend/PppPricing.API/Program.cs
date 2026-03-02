using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Configuration;
using PppPricing.API.Data;
using PppPricing.API.Middleware;
using PppPricing.API.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add Health Checks
builder.Services.AddHealthChecks();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddOpenApi();

// Add HttpClient factory
builder.Services.AddHttpClient();

// Add Memory Cache (for OAuth state validation)
builder.Services.AddMemoryCache();

// Add Data Protection (for credential encryption)
builder.Services.AddDataProtection()
    .SetApplicationName("PppPricing")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")));

// Add services
builder.Services.AddScoped<PppPricing.API.Services.IPppCalculationService, PppPricing.API.Services.PppCalculationService>();
builder.Services.AddScoped<ICredentialEncryptionService, CredentialEncryptionService>();
builder.Services.AddScoped<IPricingIndexImportService, PricingIndexImportService>();
builder.Services.AddScoped<IEffectiveMultiplierService, EffectiveMultiplierService>();
builder.Services.AddScoped<GoogleOAuthSettingsResolver>();

// Configure Rate Limiting
var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");
var generalLimit = rateLimitConfig.GetValue<int>("GeneralLimit", 100);
var generalPeriodSeconds = rateLimitConfig.GetValue<int>("GeneralPeriodSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = generalLimit,
                Window = TimeSpan.FromSeconds(generalPeriodSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please try again later." },
            cancellationToken);
    };
});

// Configure CORS from config
var allowedOrigins = builder.Configuration
    .GetSection("AllowedCorsOrigins")
    .Get<string[]>() ?? new[] { "http://localhost:3009" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("PppPricingPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Configure SQLite Database
var dbPath = builder.Configuration.GetValue<string>("DatabasePath") ?? "ppppricing.db";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Initialize Firebase Admin SDK
var firebaseCredentialPath = builder.Configuration.GetValue<string>("Firebase:CredentialPath");
if (!string.IsNullOrEmpty(firebaseCredentialPath) && File.Exists(firebaseCredentialPath))
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.FromFile(firebaseCredentialPath)
    });
}
else
{
    // Try to use environment variable
    var credentialJson = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
    if (!string.IsNullOrEmpty(credentialJson) && File.Exists(credentialJson))
    {
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromFile(credentialJson)
        });
    }
    else
    {
        // Try default credentials
        try
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.GetApplicationDefault(),
                ProjectId = builder.Configuration.GetValue<string>("Firebase:ProjectId")
            });
        }
        catch
        {
            Console.WriteLine("Warning: Firebase Admin SDK not initialized. Authentication will not work.");
        }
    }
}

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("PppPricingPolicy");

// Rate Limiting
app.UseRateLimiter();

// Don't use HTTPS redirect in development
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Firebase Auth Middleware
app.UseFirebaseAuth();

app.MapControllers();

// Health check endpoint (unauthenticated)
app.MapHealthChecks("/health");

app.Run();
