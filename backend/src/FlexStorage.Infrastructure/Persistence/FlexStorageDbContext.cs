using FlexStorage.Domain.Entities;
using FlexStorage.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using File = FlexStorage.Domain.Entities.File;

namespace FlexStorage.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for FlexStorage.
/// </summary>
public class FlexStorageDbContext : DbContext
{
    public FlexStorageDbContext(DbContextOptions<FlexStorageDbContext> options)
        : base(options)
    {
    }

    public DbSet<File> Files => Set<File>();
    public DbSet<UploadSession> UploadSessions => Set<UploadSession>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureFileEntity(modelBuilder);
        ConfigureUploadSessionEntity(modelBuilder);
        ConfigureApiKeyEntity(modelBuilder);
    }

    private void ConfigureFileEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<File>(entity =>
        {
            entity.ToTable("Files");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasConversion(
                    id => id.Value,
                    value => FileId.From(value))
                .IsRequired();

            entity.Property(e => e.UserId)
                .HasConversion(
                    id => id.Value,
                    value => UserId.From(value))
                .IsRequired();

            // FileSize value object
            entity.Property(e => e.Size)
                .HasConversion(
                    size => size.Bytes,
                    bytes => FileSize.FromBytes(bytes))
                .IsRequired();

            // FileType value object - store MIME type
            entity.Property(e => e.Type)
                .HasConversion(
                    type => type.MimeType,
                    mimeType => FileType.FromMimeType(mimeType))
                .HasMaxLength(100)
                .IsRequired();

            // UploadStatus value object
            entity.OwnsOne(e => e.Status, status =>
            {
                status.Property(s => s.CurrentState)
                    .HasColumnName("Status")
                    .IsRequired();

                status.Property(s => s.ChangedAt)
                    .HasColumnName("StatusChangedAt")
                    .IsRequired();
            });

            // StorageLocation value object
            entity.OwnsOne(e => e.Location, location =>
            {
                location.Property(l => l.ProviderName)
                    .HasColumnName("StorageProvider")
                    .HasMaxLength(50);

                location.Property(l => l.Path)
                    .HasColumnName("StoragePath")
                    .HasMaxLength(500);
            });

            // FileMetadata owned entity
            entity.OwnsOne(e => e.Metadata, metadata =>
            {
                metadata.Property(m => m.OriginalFileName)
                    .HasColumnName("OriginalFileName")
                    .HasMaxLength(255)
                    .IsRequired();

                metadata.Property(m => m.SanitizedFileName)
                    .HasColumnName("SanitizedFileName")
                    .HasMaxLength(255)
                    .IsRequired();

                metadata.Property(m => m.Hash)
                    .HasColumnName("FileHash")
                    .HasMaxLength(100)
                    .IsRequired();

                metadata.Property(m => m.CapturedAt)
                    .HasColumnName("CapturedAt")
                    .IsRequired();

                metadata.Property(m => m.Description)
                    .HasColumnName("Description")
                    .HasMaxLength(1000);

                metadata.Property(m => m.DeviceModel)
                    .HasColumnName("DeviceModel")
                    .HasMaxLength(100);

                // GPS coordinates
                metadata.Property(m => m.Latitude)
                    .HasColumnName("Latitude")
                    .HasPrecision(10, 7);

                metadata.Property(m => m.Longitude)
                    .HasColumnName("Longitude")
                    .HasPrecision(10, 7);

                // Tags stored as JSON or comma-separated
                metadata.Property("_tags")
                    .HasColumnName("Tags")
                    .HasMaxLength(1000);
            });

            entity.Property(e => e.UploadProgress)
                .IsRequired();

            // Indexes for common queries
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Files_UserId");

            // Note: Indexes on owned entity properties (FileHash, CapturedAt, Status) 
            // will be added via migrations or raw SQL due to EF Core limitations with owned entities

            // Ignore domain events (not persisted)
            entity.Ignore(e => e.DomainEvents);
        });
    }

    private void ConfigureUploadSessionEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UploadSession>(entity =>
        {
            entity.ToTable("UploadSessions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasConversion(
                    id => id.Value,
                    value => UploadSessionId.From(value))
                .IsRequired();

            entity.Property(e => e.FileId)
                .HasConversion(
                    id => id.Value,
                    value => FileId.From(value))
                .IsRequired();

            entity.Property(e => e.UserId)
                .HasConversion(
                    id => id.Value,
                    value => UserId.From(value))
                .IsRequired();

            entity.Property(e => e.TotalSize)
                .IsRequired();

            entity.Property(e => e.ChunkSize)
                .IsRequired();

            entity.Property(e => e.TotalChunks)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.ExpiresAt)
                .IsRequired();

            entity.Property(e => e.CompletedAt);

            // Store uploaded chunks as JSON array
            entity.Property("_uploadedChunks")
                .HasColumnName("UploadedChunks")
                .HasMaxLength(10000);

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_UploadSessions_UserId");

            entity.HasIndex(e => e.FileId)
                .HasDatabaseName("IX_UploadSessions_FileId");

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_UploadSessions_ExpiresAt");

            // Ignore computed properties
            entity.Ignore(e => e.UploadedChunks);
            entity.Ignore(e => e.IsComplete);
            entity.Ignore(e => e.IsExpired);
            entity.Ignore(e => e.Progress);
        });
    }

    private void ConfigureApiKeyEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("ApiKeys");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasConversion(
                    id => id.Value,
                    value => ApiKeyId.From(value))
                .IsRequired();

            entity.Property(e => e.UserId)
                .HasConversion(
                    id => id.Value,
                    value => UserId.From(value))
                .IsRequired();

            entity.Property(e => e.KeyHash)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.ExpiresAt);

            entity.Property(e => e.LastUsedAt);

            entity.Property(e => e.IsRevoked)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(200);

            // Indexes for common queries
            entity.HasIndex(e => e.KeyHash)
                .IsUnique()
                .HasDatabaseName("IX_ApiKeys_KeyHash");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_ApiKeys_UserId");
        });
    }
}
