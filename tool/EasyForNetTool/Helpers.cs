using System.Reflection;
using System.Text.RegularExpressions;

namespace EasyForNetTool;

/// <summary>
/// Provides utility helper methods for the EasyForNet tool.
/// </summary>
public static class Helpers
{
    /// <summary>
    /// Retrieves the assembly version from the entry assembly's informational version attribute.
    /// </summary>
    /// <returns>The semantic version string (major.minor.patch).</returns>
    public static string GetVersion()
    {
        var versionString = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrEmpty(versionString))
            throw new Exception("No version found.");
        var pattern = @"\d+\.\d+\.\d+";
        var match = Regex.Match(versionString, pattern);
        if (match.Success)
            return match.Value;

        throw new Exception("No version found.");
    }

    /// <summary>
    /// Determines whether the current build is a production (Release) build.
    /// </summary>
    /// <returns>True if Release build, false if Debug build.</returns>
    public static bool IsProduction()
    {
#if DEBUG
        return false;
#else
        return true;
#endif
    }
    /// <summary>
    /// Reads a .csproj file in the specified directory and extracts the project name and root namespace.
    /// </summary>
    /// <param name="path">The directory to search for a .csproj file.</param>
    /// <returns>A tuple containing the project name and root namespace, or (null, null) if no .csproj is found.</returns>
    public static (string? projectName, string? rootNamespace) GetProjectInfo(string path)
    {
        var projectPath = Directory.GetFiles(path, "*.csproj").FirstOrDefault();
        if (string.IsNullOrEmpty(projectPath))
            return (null, null);

        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var rootNamespace = projectName;
        var content = File.ReadAllText(projectPath);
        var match = Regex.Match(content, @"<RootNamespace>(.*?)</RootNamespace>");
        if (match.Success)
            rootNamespace = match.Groups[1].Value;

        return (projectName, rootNamespace);
    }
}
