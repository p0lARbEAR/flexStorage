
using Amazon.S3;
using FlexStorage.Application.Interfaces.Repositories;
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Application.Services;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Infrastructure.Persistence;
using FlexStorage.Infrastructure.Services;
using FlexStorage.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var awsOptions = builder.Configuration.GetAWSOptions();

// 2. Add services to the container.

// Infrastructure Services
builder.Services.AddDbContext<FlexStorageDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3>();

builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IUploadSessionRepository, UploadSessionRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IHashService, HashService>();

var bucketName = builder.Configuration.GetValue<string>("StorageProviders:S3GlacierDeep:Config:BucketName");
builder.Services.AddScoped<IStorageService>(sp => 
    new S3GlacierDeepArchiveProvider(sp.GetRequiredService<IAmazonS3>(), bucketName ?? "flexstorage-glacier-deep"));

// Domain Services
builder.Services.AddScoped<StorageProviderSelector>();

// Application Services
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IFileRetrievalService, FileRetrievalService>();
builder.Services.AddScoped<ChunkedUploadService>();


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

// app.UseAuthentication(); // Add this when auth is implemented
app.UseAuthorization();

app.MapControllers();

app.Run();