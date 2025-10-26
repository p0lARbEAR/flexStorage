using FluentAssertions;
using FlexStorage.Domain.ValueObjects;
using Xunit;

namespace FlexStorage.Domain.Tests.ValueObjects;

public class FileSizeTests
{
    [Fact]
    public void Should_CreateValidFileSize_WithBytes()
    {
        // Arrange
        var bytes = 1024L;

        // Act
        var fileSize = FileSize.FromBytes(bytes);

        // Assert
        fileSize.Should().NotBeNull();
        fileSize.Bytes.Should().Be(bytes);
    }

    [Fact]
    public void Should_RejectNegativeFileSize()
    {
        // Arrange
        var bytes = -100L;

        // Act
        Action act = () => FileSize.FromBytes(bytes);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be greater than zero*");
    }

    [Fact]
    public void Should_RejectZeroFileSize()
    {
        // Arrange
        var bytes = 0L;

        // Act
        Action act = () => FileSize.FromBytes(bytes);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be greater than zero*");
    }

    [Fact]
    public void Should_ConvertBytesToKB_Correctly()
    {
        // Arrange
        var fileSize = FileSize.FromBytes(2048);

        // Act
        var kb = fileSize.ToKilobytes();

        // Assert
        kb.Should().Be(2.0);
    }

    [Fact]
    public void Should_ConvertBytesToMB_Correctly()
    {
        // Arrange
        var fileSize = FileSize.FromBytes(1048576); // 1 MB

        // Act
        var mb = fileSize.ToMegabytes();

        // Assert
        mb.Should().Be(1.0);
    }

    [Fact]
    public void Should_ConvertBytesToGB_Correctly()
    {
        // Arrange
        var fileSize = FileSize.FromBytes(1073741824); // 1 GB

        // Act
        var gb = fileSize.ToGigabytes();

        // Assert
        gb.Should().Be(1.0);
    }

    [Fact]
    public void Should_CompareFileSizes_Equality()
    {
        // Arrange
        var fileSize1 = FileSize.FromBytes(1024);
        var fileSize2 = FileSize.FromBytes(1024);

        // Act & Assert
        fileSize1.Should().Be(fileSize2);
        (fileSize1 == fileSize2).Should().BeTrue();
        (fileSize1 != fileSize2).Should().BeFalse();
    }

    [Fact]
    public void Should_CompareFileSizes_GreaterThan()
    {
        // Arrange
        var largerFileSize = FileSize.FromBytes(2048);
        var smallerFileSize = FileSize.FromBytes(1024);

        // Act & Assert
        (largerFileSize > smallerFileSize).Should().BeTrue();
        (largerFileSize >= smallerFileSize).Should().BeTrue();
        (smallerFileSize > largerFileSize).Should().BeFalse();
    }

    [Fact]
    public void Should_CompareFileSizes_LessThan()
    {
        // Arrange
        var smallerFileSize = FileSize.FromBytes(1024);
        var largerFileSize = FileSize.FromBytes(2048);

        // Act & Assert
        (smallerFileSize < largerFileSize).Should().BeTrue();
        (smallerFileSize <= largerFileSize).Should().BeTrue();
        (largerFileSize < smallerFileSize).Should().BeFalse();
    }

    [Fact]
    public void Should_EnforceMaximumSizeLimit_5GB()
    {
        // Arrange
        var maxSize = 5L * 1024 * 1024 * 1024; // 5 GB
        var overMaxSize = maxSize + 1;

        // Act
        Action act = () => FileSize.FromBytes(overMaxSize);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot exceed*");
    }

    [Fact]
    public void Should_AllowMaximumSizeLimit_5GB()
    {
        // Arrange
        var maxSize = 5L * 1024 * 1024 * 1024; // 5 GB

        // Act
        var fileSize = FileSize.FromBytes(maxSize);

        // Assert
        fileSize.Bytes.Should().Be(maxSize);
    }

    [Fact]
    public void Should_ReturnHumanReadableFormat_Bytes()
    {
        // Arrange
        var fileSize = FileSize.FromBytes(512);

        // Act
        var humanReadable = fileSize.ToHumanReadable();

        // Assert
        humanReadable.Should().Be("512 B");
    }

    [Fact]
    public void Should_ReturnHumanReadableFormat_KB()
    {
        // Arrange
        var fileSize = FileSize.FromBytes(1536); // 1.5 KB

        // Act
        var humanReadable = fileSize.ToHumanReadable();

        // Assert
        humanReadable.Should().Be("1.50 KB");
    }

    [Fact]
    public void Should_ReturnHumanReadableFormat_MB()
    {
        // Arrange
        var fileSize = FileSize.FromBytes(1572864); // 1.5 MB

        // Act
        var humanReadable = fileSize.ToHumanReadable();

        // Assert
        humanReadable.Should().Be("1.50 MB");
    }

    [Fact]
    public void Should_ReturnHumanReadableFormat_GB()
    {
        // Arrange
        var fileSize = FileSize.FromBytes(1610612736); // 1.5 GB

        // Act
        var humanReadable = fileSize.ToHumanReadable();

        // Assert
        humanReadable.Should().Be("1.50 GB");
    }

    [Fact]
    public void Should_ImplementIComparable()
    {
        // Arrange
        var fileSize1 = FileSize.FromBytes(1024);
        var fileSize2 = FileSize.FromBytes(2048);

        // Act
        var comparison = fileSize1.CompareTo(fileSize2);

        // Assert
        comparison.Should().BeLessThan(0);
    }
}
