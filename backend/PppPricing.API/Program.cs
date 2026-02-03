using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using PppPricing.API.Data;
using PppPricing.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddOpenApi();

// Add HttpClient factory
builder.Services.AddHttpClient();

// Add services
builder.Services.AddScoped<PppPricing.API.Services.IPppCalculationService, PppPricing.API.Services.PppCalculationService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PppPricingPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000"
            )
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

// Don't use HTTPS redirect in development
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Firebase Auth Middleware
app.UseFirebaseAuth();

app.MapControllers();

app.Run();
