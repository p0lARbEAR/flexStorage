
using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Application.Services;
using FlexStorage.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Infrastructure Services
builder.Services.AddScoped<IHashService, HashService>();

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

// app.UseAuthentication(); // Add this when auth is implemented
app.UseAuthorization();

app.MapControllers();

app.Run();