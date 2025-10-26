
using Amazon.S3;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Application.Services;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Infrastructure.Services;
using FlexStorage.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

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
builder.Services.AddKeyedScoped<IStorageProvider>("s3-glacier-deep", (sp, _) =>
    new S3GlacierDeepArchiveProvider(sp.GetRequiredService<IAmazonS3>(), deepArchiveBucket));

builder.Services.AddKeyedScoped<IStorageProvider>("s3-glacier-flexible", (sp, _) =>
    new S3GlacierFlexibleRetrievalProvider(sp.GetRequiredService<IAmazonS3>(), flexibleBucket));

// Domain Services
builder.Services.AddScoped<StorageProviderSelector>();

// Application Services
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IFileRetrievalService, FileRetrievalService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
