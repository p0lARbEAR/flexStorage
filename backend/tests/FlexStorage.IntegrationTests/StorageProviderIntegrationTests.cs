using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Infrastructure.Storage;
using FluentAssertions;
using Moq;
using Xunit;
using Amazon.S3;

namespace FlexStorage.IntegrationTests;

public class StorageProviderIntegrationTests
{
    [Fact]
    public void StorageProviders_ShouldHaveCorrectCapabilities()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var deepProvider = new S3GlacierDeepArchiveProvider(mockS3Client.Object, "test-bucket");
        var flexibleProvider = new S3GlacierFlexibleRetrievalProvider(mockS3Client.Object, "test-bucket");

        // Act & Assert - Deep Archive
        deepProvider.ProviderName.Should().Be("s3-glacier-deep");
        deepProvider.Capabilities.SupportsDeepArchive.Should().BeTrue();
        deepProvider.Capabilities.SupportsFlexibleRetrieval.Should().BeFalse();
        deepProvider.Capabilities.MinRetrievalTime.Should().Be(TimeSpan.FromHours(12));
        deepProvider.Capabilities.MaxRetrievalTime.Should().Be(TimeSpan.FromHours(48));

        // Act & Assert - Flexible Retrieval
        flexibleProvider.ProviderName.Should().Be("s3-glacier-flexible");
        flexibleProvider.Capabilities.SupportsDeepArchive.Should().BeFalse();
        flexibleProvider.Capabilities.SupportsFlexibleRetrieval.Should().BeTrue();
        flexibleProvider.Capabilities.MinRetrievalTime.Should().Be(TimeSpan.FromHours(3));
        flexibleProvider.Capabilities.MaxRetrievalTime.Should().Be(TimeSpan.FromHours(5));
    }

    [Fact]
    public void StorageProviders_ShouldHaveUniqueNames()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var deepProvider = new S3GlacierDeepArchiveProvider(mockS3Client.Object, "test-bucket");
        var flexibleProvider = new S3GlacierFlexibleRetrievalProvider(mockS3Client.Object, "test-bucket");

        // Act & Assert
        deepProvider.ProviderName.Should().NotBe(flexibleProvider.ProviderName);
        deepProvider.ProviderName.Should().Be("s3-glacier-deep");
        flexibleProvider.ProviderName.Should().Be("s3-glacier-flexible");
    }

    [Fact]
    public void StorageProviders_ShouldImplementIStorageProvider()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();

        // Act
        IStorageProvider deepProvider = new S3GlacierDeepArchiveProvider(mockS3Client.Object, "test-bucket");
        IStorageProvider flexibleProvider = new S3GlacierFlexibleRetrievalProvider(mockS3Client.Object, "test-bucket");

        // Assert
        deepProvider.Should().NotBeNull();
        flexibleProvider.Should().NotBeNull();
        
        // Verify interface methods are available
        deepProvider.Should().BeAssignableTo<IStorageProvider>();
        flexibleProvider.Should().BeAssignableTo<IStorageProvider>();
    }
}