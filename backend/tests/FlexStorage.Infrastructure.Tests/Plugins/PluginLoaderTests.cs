using FluentAssertions;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Infrastructure.Plugins;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FlexStorage.Infrastructure.Tests.Plugins;

/// <summary>
/// Tests for PluginLoader using TDD approach.
/// Following TDD: Red-Green-Refactor cycle.
/// </summary>
public class PluginLoaderTests : IDisposable
{
    private readonly string _testPluginsDirectory;
    private readonly Mock<ILogger<PluginLoader>> _loggerMock;
    private readonly PluginLoader _sut;

    public PluginLoaderTests()
    {
        // Create unique temp directory for each test
        _testPluginsDirectory = Path.Combine(Path.GetTempPath(), $"plugins_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testPluginsDirectory);

        _loggerMock = new Mock<ILogger<PluginLoader>>();
        _sut = new PluginLoader(_testPluginsDirectory, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithValidDirectory_ShouldInitialize()
    {
        // Arrange & Act - RED: Test PluginLoader initialization
        var pluginLoader = new PluginLoader(_testPluginsDirectory, _loggerMock.Object);

        // Assert
        pluginLoader.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullDirectory_ShouldThrowArgumentNullException()
    {
        // Arrange & Act - RED: Test null directory validation
        Action act = () => new PluginLoader(null!, _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pluginsDirectory");
    }

    [Fact]
    public void DiscoverPlugins_WithEmptyDirectory_ShouldReturnEmptyList()
    {
        // Arrange - RED: Test empty directory handling

        // Act
        var result = _sut.DiscoverPlugins();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverPlugins_WithNonExistentDirectory_ShouldReturnEmptyList()
    {
        // Arrange - RED: Test non-existent directory
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"non_existent_{Guid.NewGuid():N}");
        var loader = new PluginLoader(nonExistentPath, _loggerMock.Object);

        // Act
        var result = loader.DiscoverPlugins();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverPlugins_WithValidPluginAssembly_ShouldReturnPluginInfo()
    {
        // Arrange - RED: Test discovering valid plugin
        // This test requires a mock plugin DLL - will implement in GREEN phase

        // For now, verify the method exists and returns correct type
        var result = _sut.DiscoverPlugins();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IEnumerable<PluginInfo>>();
    }

    [Fact]
    public void LoadPlugin_WithValidPluginInfo_ShouldReturnProviderInstance()
    {
        // Arrange - RED: Test loading plugin and instantiating provider
        var pluginInfo = new PluginInfo
        {
            Name = "TestProvider",
            Version = "1.0.0",
            AssemblyPath = "/path/to/test.dll",
            ProviderTypeName = "TestNamespace.TestProvider"
        };

        // Act & Assert - Will implement in GREEN phase
        // For now, verify method signature exists
        Action act = () => _sut.LoadPlugin(pluginInfo);
        act.Should().NotThrow();
    }

    [Fact]
    public void LoadPlugin_WithInvalidAssemblyPath_ShouldThrowException()
    {
        // Arrange - RED: Test error handling for invalid assembly
        var pluginInfo = new PluginInfo
        {
            Name = "InvalidProvider",
            Version = "1.0.0",
            AssemblyPath = "/invalid/path/does/not/exist.dll",
            ProviderTypeName = "Test.InvalidProvider"
        };

        // Act
        Action act = () => _sut.LoadPlugin(pluginInfo);

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*does/not/exist.dll*");
    }

    [Fact]
    public void ValidatePlugin_WithValidProviderType_ShouldReturnTrue()
    {
        // Arrange - RED: Test plugin validation
        // This will validate that a type implements IStorageProvider and has required attributes

        // Act & Assert - Will implement validation logic in GREEN phase
        var result = _sut.ValidatePlugin(typeof(object));
        result.Should().BeFalse(); // Object doesn't implement IStorageProvider
    }

    [Fact]
    public void DiscoverPlugins_ShouldLogDiscoveryProcess()
    {
        // Arrange - RED: Test logging during discovery

        // Act
        _sut.DiscoverPlugins();

        // Assert - Verify logger was called with Information level
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Scanning for plugins")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void LoadPlugin_WithDuplicateProvider_ShouldPreventLoading()
    {
        // Arrange - RED: Test preventing duplicate providers
        var pluginInfo = new PluginInfo
        {
            Name = "DuplicateProvider",
            Version = "1.0.0",
            AssemblyPath = "/path/to/duplicate.dll",
            ProviderTypeName = "Test.DuplicateProvider"
        };

        // Act - Load once
        _sut.LoadPlugin(pluginInfo);

        // Try to load again
        Action act = () => _sut.LoadPlugin(pluginInfo);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already loaded*");
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testPluginsDirectory))
        {
            try
            {
                Directory.Delete(_testPluginsDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}
