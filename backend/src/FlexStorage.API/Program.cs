
using Amazon.S3;
using FlexStorage.API.Middleware;
using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Application.Services;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Infrastructure.Persistence;
using FlexStorage.Infrastructure.Services;
using FlexStorage.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Database
builder.Services.AddDbContext<FlexStorageDbContext>(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Use in-memory database for development
        options.UseInMemoryDatabase("FlexStorageDev");
    }
    else
    {
        // Use PostgreSQL for production
        var connectionString = builder.Configuration.GetConnectionString("FlexStorage");
        options.UseNpgsql(connectionString);
    }
});

// Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// AWS Services
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();

// Infrastructure Services
builder.Services.AddScoped<IHashService, HashService>();

// Storage Providers
var deepArchiveBucket = builder.Configuration.GetValue<string>("AWS:S3:DeepArchiveBucket") ?? "flexstorage-deep-archive";
var flexibleBucket = builder.Configuration.GetValue<string>("AWS:S3:FlexibleBucket") ?? "flexstorage-flexible";

builder.Services.AddScoped<IStorageProvider>(sp =>
{
    var s3Client = sp.GetRequiredService<IAmazonS3>();
    // For now, default to Deep Archive provider
    // TODO: Implement StorageProviderFactory for dynamic selection
    return new S3GlacierDeepArchiveProvider(s3Client, deepArchiveBucket);
});

// Register both providers by name for factory pattern (future enhancement)
builder.Services.AddKeyedScoped<IStorageProvider>("s3-glacier-deep", (sp, key) =>
    new S3GlacierDeepArchiveProvider(sp.GetRequiredService<IAmazonS3>(), deepArchiveBucket));

builder.Services.AddKeyedScoped<IStorageProvider>("s3-glacier-flexible", (sp, key) =>
    new S3GlacierFlexibleRetrievalProvider(sp.GetRequiredService<IAmazonS3>(), flexibleBucket));

// Domain Services
builder.Services.AddScoped<StorageProviderSelector>();

// Application Services
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IFileRetrievalService, FileRetrievalService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "FlexStorage API", Version = "v1" });

    // Add API Key authentication to Swagger
    options.AddSecurityDefinition("ApiKey", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "API Key authentication using X-API-Key header"
    });

    options.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// API Key Authentication
app.UseApiKeyAuthentication();

app.UseAuthorization();
app.MapControllers();

app.Run();
