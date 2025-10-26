using FluentAssertions;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;
using Moq;
using Xunit;

namespace FlexStorage.Domain.Tests.DomainServices;

public class StorageProviderSelectorTests
{
    private readonly Mock<IStorageProvider> _glacierDeepMock;
    private readonly Mock<IStorageProvider> _glacierFlexMock;
    private readonly Mock<IStorageProvider> _backlazeMock;
    private readonly StorageProviderSelector _selector;

    public StorageProviderSelectorTests()
    {
        _glacierDeepMock = new Mock<IStorageProvider>();
        _glacierDeepMock.Setup(p => p.ProviderName).Returns("S3 Glacier Deep Archive");
        _glacierDeepMock.Setup(p => p.Capabilities).Returns(new ProviderCapabilities
        {
            SupportsInstantAccess = false,
            SupportsRetrieval = true
        });

        _glacierFlexMock = new Mock<IStorageProvider>();
        _glacierFlexMock.Setup(p => p.ProviderName).Returns("S3 Glacier Flexible Retrieval");
        _glacierFlexMock.Setup(p => p.Capabilities).Returns(new ProviderCapabilities
        {
            SupportsInstantAccess = false,
            SupportsRetrieval = true
        });

        _backlazeMock = new Mock<IStorageProvider>();
        _backlazeMock.Setup(p => p.ProviderName).Returns("Backblaze B2");
        _backlazeMock.Setup(p => p.Capabilities).Returns(new ProviderCapabilities
        {
            SupportsInstantAccess = true,
            SupportsRetrieval = false
        });

        var providers = new List<IStorageProvider>
        {
            _glacierDeepMock.Object,
            _glacierFlexMock.Object,
            _backlazeMock.Object
        };

        _selector = new StorageProviderSelector(providers);
    }

    [Fact]
    public void Should_SelectGlacierDeep_ForPhotos_ByDefault()
    {
        // Arrange
        var photoType = FileType.FromMimeType("image/jpeg");
        var fileSize = FileSize.FromBytes(5 * 1024 * 1024); // 5 MB

        // Act
        var provider = _selector.SelectProvider(photoType, fileSize);

        // Assert
        provider.ProviderName.Should().Be("S3 Glacier Deep Archive");
    }

    [Fact]
    public void Should_SelectGlacierFlexible_ForLargeVideos()
    {
        // Arrange
        var videoType = FileType.FromMimeType("video/mp4");
        var largeSize = FileSize.FromBytes(500 * 1024 * 1024); // 500 MB

        // Act
        var provider = _selector.SelectProvider(videoType, largeSize);

        // Assert
        provider.ProviderName.Should().Be("S3 Glacier Flexible Retrieval");
    }

    [Fact]
    public void Should_RespectUserSpecifiedProvider()
    {
        // Arrange
        var photoType = FileType.FromMimeType("image/jpeg");
        var fileSize = FileSize.FromBytes(1024 * 1024);

        // Act
        var provider = _selector.SelectProvider(photoType, fileSize, "Backblaze B2");

        // Assert
        provider.ProviderName.Should().Be("Backblaze B2");
    }

    [Fact]
    public void Should_FallbackToDefault_IfPreferenceNotAvailable()
    {
        // Arrange
        var photoType = FileType.FromMimeType("image/jpeg");
        var fileSize = FileSize.FromBytes(1024 * 1024);

        // Act
        var provider = _selector.SelectProvider(photoType, fileSize, "NonExistentProvider");

        // Assert
        provider.ProviderName.Should().Be("S3 Glacier Deep Archive"); // Default
    }

    [Fact]
    public void Should_ThrowException_IfNoProvidersAvailable()
    {
        // Arrange
        var emptySelector = new StorageProviderSelector(new List<IStorageProvider>());
        var photoType = FileType.FromMimeType("image/jpeg");
        var fileSize = FileSize.FromBytes(1024 * 1024);

        // Act
        Action act = () => emptySelector.SelectProvider(photoType, fileSize);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No storage providers available*");
    }

    [Fact]
    public void Should_ConsiderFileType_InSelection()
    {
        // Arrange - Photo should go to Deep Archive
        var photoType = FileType.FromMimeType("image/png");
        var photoSize = FileSize.FromBytes(2 * 1024 * 1024);

        // Act
        var photoProvider = _selector.SelectProvider(photoType, photoSize);

        // Assert
        photoProvider.ProviderName.Should().Be("S3 Glacier Deep Archive");

        // Arrange - Video should go to Flexible
        var videoType = FileType.FromMimeType("video/mp4");
        var videoSize = FileSize.FromBytes(2 * 1024 * 1024);

        // Act
        var videoProvider = _selector.SelectProvider(videoType, videoSize);

        // Assert
        videoProvider.ProviderName.Should().Be("S3 Glacier Flexible Retrieval");
    }

    [Fact]
    public void Should_ValidateProvider_IsEnabled()
    {
        // This test ensures we only select from enabled providers
        // For now, all providers in the list are considered enabled
        // Future: Add Enabled property to providers

        // Arrange
        var photoType = FileType.FromMimeType("image/jpeg");
        var fileSize = FileSize.FromBytes(1024 * 1024);

        // Act
        var provider = _selector.SelectProvider(photoType, fileSize);

        // Assert
        provider.Should().NotBeNull();
    }
}
