using FlexStorage.Domain.DomainServices;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace FlexStorage.Infrastructure.Plugins;

/// <summary>
/// Loads storage provider plugins from external assemblies.
/// </summary>
public class PluginLoader
{
    private readonly string _pluginsDirectory;
    private readonly ILogger<PluginLoader> _logger;
    private readonly HashSet<string> _loadedProviders = new();

    public PluginLoader(string pluginsDirectory, ILogger<PluginLoader> logger)
    {
        _pluginsDirectory = pluginsDirectory ?? throw new ArgumentNullException(nameof(pluginsDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Discovers all plugin assemblies in the plugins directory.
    /// </summary>
    public IEnumerable<PluginInfo> DiscoverPlugins()
    {
        _logger.LogInformation("Scanning for plugins in directory: {PluginsDirectory}", _pluginsDirectory);

        // Check if directory exists
        if (!Directory.Exists(_pluginsDirectory))
        {
            _logger.LogWarning("Plugins directory does not exist: {PluginsDirectory}", _pluginsDirectory);
            return Enumerable.Empty<PluginInfo>();
        }

        var plugins = new List<PluginInfo>();

        // Get all DLL files in the plugins directory
        var dllFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                // Try to load assembly and discover providers
                var pluginInfo = DiscoverPluginFromAssembly(dllPath);
                if (pluginInfo != null)
                {
                    plugins.Add(pluginInfo);
                    _logger.LogInformation("Discovered plugin: {PluginName} v{Version}",
                        pluginInfo.Name, pluginInfo.Version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plugin from: {AssemblyPath}", dllPath);
            }
        }

        return plugins;
    }

    /// <summary>
    /// Loads a plugin and returns an instance of the storage provider.
    /// </summary>
    public IStorageProvider LoadPlugin(PluginInfo pluginInfo)
    {
        ArgumentNullException.ThrowIfNull(pluginInfo);

        // Check if already loaded
        if (_loadedProviders.Contains(pluginInfo.ProviderTypeName))
        {
            throw new InvalidOperationException(
                $"Provider '{pluginInfo.Name}' (Type: {pluginInfo.ProviderTypeName}) is already loaded.");
        }

        // Validate assembly path exists
        if (!File.Exists(pluginInfo.AssemblyPath))
        {
            throw new FileNotFoundException(
                $"Plugin assembly not found: {pluginInfo.AssemblyPath}",
                pluginInfo.AssemblyPath);
        }

        try
        {
            // Load the assembly
            var assembly = Assembly.LoadFrom(pluginInfo.AssemblyPath);

            // Get the provider type
            var providerType = assembly.GetType(pluginInfo.ProviderTypeName);
            if (providerType == null)
            {
                throw new TypeLoadException(
                    $"Provider type '{pluginInfo.ProviderTypeName}' not found in assembly '{pluginInfo.AssemblyPath}'");
            }

            // Validate the type implements IStorageProvider
            if (!typeof(IStorageProvider).IsAssignableFrom(providerType))
            {
                throw new InvalidOperationException(
                    $"Type '{pluginInfo.ProviderTypeName}' does not implement IStorageProvider");
            }

            // Create instance (assumes parameterless constructor for now)
            var providerInstance = Activator.CreateInstance(providerType) as IStorageProvider;
            if (providerInstance == null)
            {
                throw new InvalidOperationException(
                    $"Failed to instantiate provider '{pluginInfo.ProviderTypeName}'");
            }

            // Mark as loaded
            _loadedProviders.Add(pluginInfo.ProviderTypeName);

            _logger.LogInformation("Successfully loaded plugin: {PluginName}", pluginInfo.Name);

            return providerInstance;
        }
        catch (Exception ex) when (ex is not FileNotFoundException
            && ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to load plugin: {PluginName}", pluginInfo.Name);
            throw;
        }
    }

    /// <summary>
    /// Validates that a type is a valid storage provider plugin.
    /// </summary>
    public bool ValidatePlugin(Type type)
    {
        // Must implement IStorageProvider
        if (!typeof(IStorageProvider).IsAssignableFrom(type))
            return false;

        // Must be a class (not interface or abstract)
        if (!type.IsClass || type.IsAbstract)
            return false;

        // Must have a public parameterless constructor
        var constructor = type.GetConstructor(Type.EmptyTypes);
        if (constructor == null)
            return false;

        return true;
    }

    private PluginInfo? DiscoverPluginFromAssembly(string assemblyPath)
    {
        // Load assembly in reflection-only context
        var assembly = Assembly.LoadFrom(assemblyPath);

        // Find types that implement IStorageProvider
        var providerTypes = assembly.GetTypes()
            .Where(t => typeof(IStorageProvider).IsAssignableFrom(t)
                && t.IsClass
                && !t.IsAbstract)
            .ToList();

        if (!providerTypes.Any())
            return null;

        // For now, take the first provider found
        var providerType = providerTypes.First();

        // Extract version from assembly
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0";

        return new PluginInfo
        {
            Name = providerType.Name,
            Version = version,
            AssemblyPath = assemblyPath,
            ProviderTypeName = providerType.FullName ?? providerType.Name
        };
    }
}
