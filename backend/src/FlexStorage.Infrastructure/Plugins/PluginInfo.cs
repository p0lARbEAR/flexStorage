namespace FlexStorage.Infrastructure.Plugins;

/// <summary>
/// Contains information about a discovered storage provider plugin.
/// </summary>
public class PluginInfo
{
    /// <summary>
    /// Name of the provider (e.g., "S3GlacierDeepArchive").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Version of the plugin.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Full path to the plugin assembly DLL.
    /// </summary>
    public required string AssemblyPath { get; init; }

    /// <summary>
    /// Fully qualified type name of the provider class.
    /// </summary>
    public required string ProviderTypeName { get; init; }

    /// <summary>
    /// Optional description of the plugin.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional author information.
    /// </summary>
    public string? Author { get; init; }
}
