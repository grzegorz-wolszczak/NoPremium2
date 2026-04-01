namespace NoPremium2.Config;

/// <summary>
/// Resolves a file path that may be absolute or relative.
/// Relative paths are resolved against two base directories and must be unambiguous.
/// </summary>
public sealed class PathResolver
{
    private readonly string _configFileDir;
    private readonly string _appBaseDir;

    /// <param name="configFilePath">Full path to the config.json file (used to derive its directory).</param>
    /// <param name="appBaseDir">Directory where the application binary lives (AppContext.BaseDirectory).</param>
    public PathResolver(string configFilePath, string appBaseDir)
    {
        _configFileDir = Path.GetDirectoryName(Path.GetFullPath(configFilePath))
            ?? throw new ArgumentException("Cannot determine config file directory", nameof(configFilePath));
        _appBaseDir = Path.GetFullPath(appBaseDir);
    }

    /// <summary>
    /// Resolves <paramref name="rawPath"/> to an existing file's absolute path.
    /// </summary>
    /// <exception cref="FileNotFoundException">File not found in any candidate location.</exception>
    /// <exception cref="InvalidOperationException">File found in both relative locations (ambiguous).</exception>
    public string Resolve(string rawPath)
    {
        if (Path.IsPathRooted(rawPath))
        {
            // Absolute path — use as-is
            var abs = Path.GetFullPath(rawPath);
            if (!File.Exists(abs))
                throw new FileNotFoundException(
                    $"File not found: {abs}", abs);
            return abs;
        }

        // Relative path: probe both candidate directories
        var candidateFromConfig = Path.GetFullPath(Path.Combine(_configFileDir, rawPath));
        var candidateFromApp    = Path.GetFullPath(Path.Combine(_appBaseDir, rawPath));

        bool existsInConfig = File.Exists(candidateFromConfig);
        bool existsInApp    = File.Exists(candidateFromApp);

        if (existsInConfig && existsInApp &&
            !string.Equals(candidateFromConfig, candidateFromApp, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Ambiguous relative path '{rawPath}': file found in both locations. " +
                $"Use an absolute path to remove the ambiguity.\n" +
                $"  (1) {candidateFromConfig}\n" +
                $"  (2) {candidateFromApp}");
        }

        if (existsInConfig) return candidateFromConfig;
        if (existsInApp)    return candidateFromApp;

        throw new FileNotFoundException(
            $"File '{rawPath}' not found in either candidate location:\n" +
            $"  (1) {candidateFromConfig}\n" +
            $"  (2) {candidateFromApp}",
            rawPath);
    }
}
