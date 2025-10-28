
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using FlexStorage.API.Middleware;
using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Application.Services;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Infrastructure.Configuration;
using FlexStorage.Infrastructure.Persistence;
using FlexStorage.Infrastructure.Services;
using FlexStorage.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configuration Options
builder.Services.Configure<ThumbnailOptions>(
    builder.Configuration.GetSection(ThumbnailOptions.SectionName));

// AWS Services
builder.Services.AddSingleton<IAmazonS3>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetService<ILogger<Program>>();
    
    // Get AWS configuration values
    var awsRegion = configuration["AWS:Region"] ?? "us-east-1";
    var serviceUrl = configuration["AWS:S3:ServiceURL"];
    var accessKey = configuration["AWS:AccessKey"];
    var secretKey = configuration["AWS:SecretKey"];
    
    // Create S3 configuration
    var config = new AmazonS3Config
    {
        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion),
        AuthenticationRegion = awsRegion,
        UseHttp = configuration.GetValue<bool>("AWS:S3:UseHttp", false),
        ForcePathStyle = configuration.GetValue<bool>("AWS:S3:ForcePathStyle", false),
        DisableHostPrefixInjection = configuration.GetValue<bool>("AWS:S3:DisableHostPrefixInjection", false),
        MaxErrorRetry = configuration.GetValue<int>("AWS:S3:MaxErrorRetry", 3),
        UseDualstackEndpoint = configuration.GetValue<bool>("AWS:S3:UseDualstackEndpoint", false),
        UseAccelerateEndpoint = configuration.GetValue<bool>("AWS:S3:UseAccelerateEndpoint", false),
        DisableLogging = configuration.GetValue<bool>("AWS:S3:DisableLogging", true)
    };
    
    // Set service URL if provided (for LocalStack)
    if (!string.IsNullOrEmpty(serviceUrl))
    {
        config.ServiceURL = serviceUrl;
        logger?.LogInformation("S3 Client configured with custom endpoint: {ServiceURL}", serviceUrl);
    }
    
    // Create credentials
    AmazonS3Client client;
    if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
    {
        // Use explicit credentials (for LocalStack or specific AWS credentials)
        var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
        client = new AmazonS3Client(credentials, config);
        logger?.LogInformation("S3 Client configured with explicit credentials");
    }
    else
    {
        // Use default AWS credential chain (for production)
        client = new AmazonS3Client(config);
        logger?.LogInformation("S3 Client configured with default AWS credential chain");
    }
    
    // Set environment variables for AWS SDK
    if (!string.IsNullOrEmpty(accessKey))
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", accessKey);
    if (!string.IsNullOrEmpty(secretKey))
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", secretKey);
    if (!string.IsNullOrEmpty(awsRegion))
        Environment.SetEnvironmentVariable("AWS_DEFAULT_REGION", awsRegion);
    if (!string.IsNullOrEmpty(serviceUrl))
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", serviceUrl);
    
    logger?.LogInformation("S3 Client configured for region {Region}", awsRegion);
    
    return client;
});

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

// Infrastructure Services
builder.Services.AddScoped<IHashService, HashService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<IThumbnailService, ThumbnailService>();

// Storage Providers
var deepArchiveBucket =
    builder.Configuration.GetValue<string>("AWS:S3:DeepArchiveBucket") ?? "flexstorage-deep-archive";
var flexibleBucket = builder.Configuration.GetValue<string>("AWS:S3:FlexibleBucket") ?? "flexstorage-flexible";
var thumbnailBucket = builder.Configuration.GetValue<string>("AWS:S3:ThumbnailBucket") ?? "flexstorage-thumbnails";

builder.Services.AddScoped<IStorageProvider>(sp =>
{
    var s3Client = sp.GetRequiredService<IAmazonS3>();
    // For now, default to Deep Archive provider
    // TODO: Implement StorageProviderFactory for dynamic selection
    return new S3GlacierDeepArchiveProvider(s3Client, deepArchiveBucket);
});

// Register both providers by name for factory pattern (future enhancement)
builder.Services.AddKeyedScoped<IStorageProvider>("s3-glacier-deep", (sp, _) =>
    new S3GlacierDeepArchiveProvider(sp.GetRequiredService<IAmazonS3>(), deepArchiveBucket));

builder.Services.AddKeyedScoped<IStorageProvider>("s3-glacier-flexible", (sp, _) =>
    new S3GlacierFlexibleRetrievalProvider(sp.GetRequiredService<IAmazonS3>(), flexibleBucket));

// S3 Standard for thumbnails (instant access, no retrieval needed)
builder.Services.AddKeyedScoped<IStorageProvider>("s3-standard", (sp, _) =>
    new S3StandardProvider(sp.GetRequiredService<IAmazonS3>(), thumbnailBucket));

// Domain Services
builder.Services.AddScoped<StorageProviderSelector>();

// Application Services
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IFileUploadService>(sp => new FileUploadService(
    sp.GetRequiredService<IUnitOfWork>(),
    sp.GetRequiredService<IHashService>(),
    sp.GetRequiredService<IStorageService>(),
    sp.GetRequiredService<StorageProviderSelector>(),
    sp.GetRequiredService<IThumbnailService>(),
    sp.GetRequiredKeyedService<IStorageProvider>("s3-standard") // Inject thumbnail storage provider
));
builder.Services.AddScoped<IChunkedUploadService, ChunkedUploadService>();
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
