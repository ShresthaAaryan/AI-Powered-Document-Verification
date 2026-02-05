using DocumentVerification.API.Configuration;
using DocumentVerification.API.Data;
using DocumentVerification.API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();

// Database configuration - use SQLite for development, PostgreSQL for production
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var sqliteConnection = builder.Configuration.GetConnectionString("SqliteConnection");

if (builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(sqliteConnection))
{
    builder.Services.AddDbContext<DocumentVerificationDbContext>(options =>
        options.UseSqlite(sqliteConnection));
}
else
{
    builder.Services.AddDbContext<DocumentVerificationDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Identity configuration
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<DocumentVerificationDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
builder.Services.ConfigureJwtAuthentication(builder.Configuration);

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Next.js frontend
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// API Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() {
        Title = "Document Verification API",
        Version = "v1",
        Description = "AI-powered document verification platform API"
    });

    // Include XML Comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IOcrService, OcrService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFaceMatchingService, FaceMatchingService>();
builder.Services.AddScoped<IAIAnalysisService, AIAnalysisService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

// AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DocumentVerificationDbContext>()
    .AddCheck<AIModelsHealthCheck>("ai-models");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Document Verification API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DocumentVerificationDbContext>();
    // For local dev with SQLite, recreate schema to ensure Identity tables exist
    if (app.Environment.IsDevelopment() && context.Database.IsSqlite())
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
    else
    {
        // For PostgreSQL (or other relational providers), use migrations
        await context.Database.MigrateAsync();
    }
}

try
{
    Log.Information("Starting Document Verification API");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}