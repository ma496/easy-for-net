namespace EasyForNetTool.Generator;

using System.Diagnostics;
using System.Text.RegularExpressions;
using EasyForNetTool.Extensions;
using EasyForNetTool.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

/// <summary>
/// Generates a complete EasyForNet project by cloning the template repository,
/// copying files, and customizing namespaces, settings, and project names.
/// </summary>
public class CreateProjectGenerator : CodeGeneratorBase<CreateProjectArgument>
{
    /// <summary>
    /// Generates a new project from the template repository with the specified name and options.
    /// </summary>
    /// <param name="argument">The create-project argument containing name, output path, and multi-language flag.</param>
    public override async Task Generate(CreateProjectArgument argument)
    {
        var version = Helpers.GetVersion();
        var templateBaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyForNet",
            "Templates"
        );
        var versionedTemplateDir = Path.Combine(templateBaseDir, version);
        var gitUrl = "https://github.com/ma496/EasyForNet.git";
        var createdTemplateCache = false;

        try
        {
            if (!Directory.Exists(versionedTemplateDir))
            {
                createdTemplateCache = true;
                Console.WriteLine("Creating template directory...");
                Directory.CreateDirectory(versionedTemplateDir);

                Console.WriteLine("Cloning template repository...");
                try
                {
                    await ExecuteCommand("git", $"clone {gitUrl} \"{versionedTemplateDir}\"");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to clone template repository. Please ensure Git is installed and accessible. Error: {ex.Message}", ex);
                }

                Console.WriteLine($"Checking out version {version}...");
                try
                {
                    await ExecuteCommand("git", $"-C \"{versionedTemplateDir}\" checkout tags/v{version}");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to checkout version {version}. The version might not exist. Error: {ex.Message}", ex);
                }
            }

            Console.WriteLine("Copying template files...");
            var backendProjectPath = Path.Combine(versionedTemplateDir, "src", "backend");
            var webProjectPath = Path.Combine(versionedTemplateDir, "src", "frontend", "web");
            var pascalCaseProjectName = argument.Name.ToPascalCase();
            var kebabCaseProjectName = argument.Name.ToKebabCase();
            var targetPath = Path.Combine(Directory.GetCurrentDirectory(), argument.Output ?? string.Empty, kebabCaseProjectName);
            var srcTargetPath = Path.Combine(targetPath, "src");
            var backendTargetPath = Path.Combine(srcTargetPath, "backend");
            var backendProjectTargetPath = Path.Combine(backendTargetPath, "Source");
            var backendTestProjectTargetPath = Path.Combine(backendTargetPath, "Tests");
            var webTargetPath = Path.Combine(srcTargetPath, "frontend", "web");

            if (Directory.Exists(targetPath))
            {
                throw new UserFriendlyException($"Directory '{targetPath}' already exists. Please choose a different project name or location.");
            }

            Directory.CreateDirectory(targetPath);
            if (!Directory.Exists(backendTargetPath))
                Directory.CreateDirectory(backendTargetPath);
            if (!Directory.Exists(webTargetPath))
                Directory.CreateDirectory(webTargetPath);

            Console.WriteLine("Copying project files...");
            CopyDirectory(backendProjectPath, backendTargetPath, true, ["Migrations"]);
            CopyFrom($"{backendProjectPath}/Source", $"{backendTargetPath}/Source", "appsettings.json", "appsettings.Development.json");
            CopyFrom($"{backendProjectPath}/Source", $"{backendTargetPath}/Source", "appsettings.json", "appsettings.Testing.json");
            CopyDirectory(webProjectPath, webTargetPath, true);
            // .env.development is git-ignored in the template, so seed it from the tracked example file
            CopyFrom(webProjectPath, webTargetPath, ".env.example", ".env.development");
            CopyFiles(versionedTemplateDir, targetPath, ".editorconfig", ".gitignore", "global.json");
            CopyDirectory($"{versionedTemplateDir}/.config", $"{targetPath}/.config", true);
            CopyDirectory($"{versionedTemplateDir}/.vscode", $"{targetPath}/.vscode", true);
            WriteEmbeddedFile("new-project-claude.md", Path.Combine(targetPath, "CLAUDE.md"));

            Console.WriteLine("Customizing project files...");
            var (backendProjectName, backendProjectRootNamespace) = Helpers.GetProjectInfo(backendProjectTargetPath);
            if (string.IsNullOrEmpty(backendProjectName) || string.IsNullOrEmpty(backendProjectRootNamespace))
            {
                throw new UserFriendlyException($"Failed to get root namespace from project '{backendProjectTargetPath}'. csproj file is not found.");
            }
            var (backendTestProjectName, backendTestProjectRootNamespace) = Helpers.GetProjectInfo(backendTestProjectTargetPath);
            if (string.IsNullOrEmpty(backendTestProjectName) || string.IsNullOrEmpty(backendTestProjectRootNamespace))
            {
                throw new UserFriendlyException($"Failed to get root namespace from project '{backendTestProjectTargetPath}'. csproj file is not found.");
            }
            // update connection string
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(backendProjectTargetPath, "appsettings.json"), "ConnectionStrings.DefaultConnection", $"Host=localhost;Port=5432;Database={pascalCaseProjectName};Username=postgres;Password={{password}}");
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(backendProjectTargetPath, "appsettings.json"), "Hangfire.Storage.ConnectionString", $"Host=localhost;Port=5432;Database={pascalCaseProjectName};Username=postgres;Password={{password}}");
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(backendProjectTargetPath, "appsettings.Development.json"), "Auth.Jwt.Key", Guid.NewGuid().ToString());
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(backendProjectTargetPath, "appsettings.Development.json"), "ConnectionStrings.DefaultConnection", $"Host=localhost;Port=5432;Database={pascalCaseProjectName};Username=postgres;Password={{password}}");
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(backendProjectTargetPath, "appsettings.Development.json"), "Hangfire.Storage.ConnectionString", $"Host=localhost;Port=5432;Database={pascalCaseProjectName};Username=postgres;Password={{password}}");
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(backendProjectTargetPath, "appsettings.Testing.json"), "Auth.Jwt.Key", Guid.NewGuid().ToString());
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(backendProjectTargetPath, "appsettings.Testing.json"), "ConnectionStrings.DefaultConnection", $"Host=localhost;Port=5432;Database={pascalCaseProjectName}Test;Username=postgres;Password={{password}}");
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(backendProjectTargetPath, "appsettings.Testing.json"), "Hangfire.Storage.ConnectionString", $"Host=localhost;Port=5432;Database={pascalCaseProjectName}Test;Username=postgres;Password={{password}}");
            // update Meta.cs
            await ReplaceInFile(Path.Combine(backendProjectTargetPath, "Meta.cs"), $@"InternalsVisibleTo\s*\(\s*""{Regex.Escape(backendTestProjectName)}""\s*\)", $@"InternalsVisibleTo(""{pascalCaseProjectName}.Tests"")");
            // update Program.cs
            await ReplaceInFile(Path.Combine(backendProjectTargetPath, "Program.cs"), $@"c\.Binding\.ReflectionCache\.AddFrom{Regex.Escape(backendProjectName)}", $@"c.Binding.ReflectionCache.AddFrom{pascalCaseProjectName}");
            // update project name
            RenameFile(backendProjectTargetPath, $"{backendProjectName}.csproj", $"{pascalCaseProjectName}.csproj");
            await AdjustNamespaceAsync(backendProjectTargetPath, backendProjectRootNamespace, pascalCaseProjectName);
            // update FeatureDependencyTests.cs
            await ReplaceInFile(Path.Combine(backendTestProjectTargetPath, "Architect", "FeatureDependencyTests.cs"), $@"{Regex.Escape(backendProjectRootNamespace)}", $@"{pascalCaseProjectName}");
            // update test project name
            await ReplaceInFile(Path.Combine(backendTestProjectTargetPath, $"{backendTestProjectName}.csproj"), @$"{Regex.Escape("Backend")}\.csproj", $@"{pascalCaseProjectName}.csproj");
            RenameFile(backendTestProjectTargetPath, $"{backendTestProjectName}.csproj", $"{pascalCaseProjectName}.Tests.csproj");
            await AdjustNamespaceAsync(backendTestProjectTargetPath, backendProjectRootNamespace, pascalCaseProjectName);
            await AdjustNamespaceAsync(backendTestProjectTargetPath, backendTestProjectRootNamespace, $"{pascalCaseProjectName}.Tests");
            // replace Easy For Net text with project name in web project
            await ReplaceInFiles(webTargetPath, @"Easy\s+For\s+Net", $@"{kebabCaseProjectName.Split('-').Select(x => char.ToUpper(x[0]) + x[1..]).Aggregate((current, next) => current + " " + next)}", ".json");
            // update package.json and package-lock.json
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(webTargetPath, "package.json"), "name", kebabCaseProjectName);
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(webTargetPath, "package-lock.json"), "name", kebabCaseProjectName);
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(webTargetPath, "package-lock.json"), "packages..name", kebabCaseProjectName);
            // update CLAUDE.md
            await ReplaceInFile(Path.Combine(targetPath, "CLAUDE.md"), @"EasyForNet\.slnx", $@"{pascalCaseProjectName}.slnx");

            // Cleanup localization files when multiLanguage is false
            if (!argument.MultiLanguage)
            {
                Console.WriteLine("Cleaning up localization files...");

                // 1. Delete non-English locale files (ur, zh, ar, hi, es, fr, ru)
                var localesPath = Path.Combine(webTargetPath, "public", "locales");
                foreach (var locale in new[] { "ur", "zh", "ar", "hi", "es", "fr", "ru" })
                {
                    var filePath = Path.Combine(localesPath, $"{locale}.json");
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }

                // 2. Update server.ts - keep only en dictionary
                var serverTsPath = Path.Combine(webTargetPath, "i18n", "server.ts");
                if (File.Exists(serverTsPath))
                {
                    var content = await File.ReadAllTextAsync(serverTsPath);
                    content = Regex.Replace(content,
                        @"const dictionaries = \{[^}]+\}",
                        @"const dictionaries = {
  en: () => import('../public/locales/en.json').then((module) => module.default),
}");
                    await File.WriteAllTextAsync(serverTsPath, content);
                }

                // 3. Update config.ts - set locales to only ['en']
                var configTsPath = Path.Combine(webTargetPath, "i18n", "config.ts");
                if (File.Exists(configTsPath))
                {
                    var content = await File.ReadAllTextAsync(configTsPath);
                    content = Regex.Replace(content, @"locales: \[[^\]]+\]", "locales: ['en']");
                    await File.WriteAllTextAsync(configTsPath, content);
                }

                // 4. Update themeConfigSlice.tsx - remove non-English languages
                var themeConfigPath = Path.Combine(webTargetPath, "store", "slices", "themeConfigSlice.tsx");
                if (File.Exists(themeConfigPath))
                {
                    var content = await File.ReadAllTextAsync(themeConfigPath);
                    content = Regex.Replace(content,
                        @"languageList: \[[^\]]+\]",
                        @"languageList: [
    { code: 'en', name: 'English', isRTL: false },
  ]");
                    await File.WriteAllTextAsync(themeConfigPath, content);
                }
            }

            // create solution file
            Console.WriteLine("Creating solution file...");
            var solutionPath = Path.Combine(targetPath, $"{pascalCaseProjectName}.slnx");
            await ExecuteCommand("dotnet", $"new sln -n {pascalCaseProjectName} -o \"{Path.GetDirectoryName(solutionPath)}\" -f slnx");

            // Add projects to solution
            var projectFiles = Directory.GetFiles(backendTargetPath, "*.csproj", SearchOption.AllDirectories);
            foreach (var projectFile in projectFiles)
            {
                Console.WriteLine($"Adding project: {Path.GetFileName(projectFile)}");
                await ExecuteCommand("dotnet", $"sln \"{solutionPath}\" add \"{projectFile}\"");
            }

            Console.WriteLine("Project creation completed successfully!");
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (createdTemplateCache && Directory.Exists(versionedTemplateDir))
            {
                try
                {
                    Directory.Delete(versionedTemplateDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            throw new Exception($"Failed to generate project: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes an external command and waits for completion, throwing on failure or timeout.
    /// </summary>
    /// <param name="command">The command name (e.g., "git", "dotnet").</param>
    /// <param name="arguments">The command arguments.</param>
    private static async Task ExecuteCommand(string command, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (command == "git" && !IsGitInstalled())
            {
                throw new Exception("Git is not installed or not accessible in the system PATH.");
            }

            process.Start();

            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
            var processTask = process.WaitForExitAsync();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            var completedTask = await Task.WhenAny(processTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                process.Kill();
                throw new Exception($"Command timed out after 5 minutes: {command} {arguments}");
            }

            if (process.ExitCode != 0)
                throw new Exception($"Command failed with exit code {process.ExitCode}:\nCommand: {command} {arguments}\nError: {error}\nOutput: {output}");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new Exception($"Failed to execute command: {command} {arguments}\nError: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether Git is installed and accessible in the system PATH.
    /// </summary>
    private static bool IsGitInstalled()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return process?.WaitForExit(5000) == true && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Recursively copies a directory from source to target, optionally ignoring certain subdirectory names.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string targetDir, bool recursive, string[]? ignoreDirectories = null)
    {
        var dir = new DirectoryInfo(sourceDir);
        var dirs = dir.GetDirectories()
            .Where(d => ignoreDirectories is null || !ignoreDirectories.Contains(d.Name))
            .ToArray();

        Directory.CreateDirectory(targetDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(targetDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        if (recursive)
        {
            foreach (var subDir in dirs)
            {
                var newTargetDir = Path.Combine(targetDir, subDir.Name);
                CopyDirectory(subDir.FullName, newTargetDir, true, ignoreDirectories);
            }
        }
    }

    /// <summary>
    /// Renames a file within the specified directory.
    /// </summary>
    private static void RenameFile(string directory, string oldName, string newName)
    {
        var filePath = Path.Combine(directory, oldName);
        var newFilePath = Path.Combine(directory, newName);
        if (filePath != newFilePath)
            File.Move(filePath, newFilePath);
    }

    /// <summary>
    /// Replaces text matching a regular expression in a file with the specified replacement.
    /// </summary>
    private static async Task ReplaceInFile(string filePath, string regularExpression, string replacement)
    {
        var text = await File.ReadAllTextAsync(filePath);
        if (Regex.IsMatch(text, regularExpression))
        {
            text = Regex.Replace(text, regularExpression, replacement);
            await File.WriteAllTextAsync(filePath, text);
        }
    }

    /// <summary>
    /// Replaces text matching a regular expression in all files with the given extensions within a directory.
    /// </summary>
    private static async Task ReplaceInFiles(string directory, string regularExpression, string replacement, params string[] extensions)
    {
        foreach (var file in Directory
            .EnumerateFiles(directory, $"*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f))))
        {
            await ReplaceInFile(file, regularExpression, replacement);
        }

    }

    /// <summary>
    /// Adjusts all C# namespaces in a directory from an old root namespace to a new one using Roslyn syntax rewriting.
    /// </summary>
    public static async Task AdjustNamespaceAsync(string directory, string oldRoot, string newRoot)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var text = await File.ReadAllTextAsync(file);

            var tree = CSharpSyntaxTree.ParseText(text);
            var root = await tree.GetRootAsync();

            var rewriter = new NamespaceRewriter(oldRoot, newRoot);
            var newRootNode = rewriter.Visit(root);

            // Create a workspace to format the document
            using var workspace = new AdhocWorkspace();
            var formattedNode = Formatter.Format(newRootNode, workspace);

            await File.WriteAllTextAsync(file, formattedNode.ToFullString());
        }
    }

    /// <summary>
    /// Copies specific files by name from a source directory to a target directory.
    /// </summary>
    private static void CopyFiles(string sourceDirectory, string targetDirectory, params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            var sourceFilePath = Path.Combine(sourceDirectory, fileName);
            var targetFilePath = Path.Combine(targetDirectory, fileName);

            if (File.Exists(sourceFilePath))
            {
                File.Copy(sourceFilePath, targetFilePath, overwrite: true);
            }
            else
            {
                throw new Exception($"File '{fileName}' does not exist in the source directory '{sourceDirectory}'.");
            }
        }
    }

    /// <summary>
    /// Writes a file that is embedded in the tool assembly to the specified target path.
    /// </summary>
    /// <param name="resourceFileName">The name of the embedded file, as declared by its logical name.</param>
    /// <param name="targetFilePath">The full path of the file to create.</param>
    private static void WriteEmbeddedFile(string resourceFileName, string targetFilePath)
    {
        var assembly = typeof(CreateProjectGenerator).Assembly;
        var resourceName = $"{assembly.GetName().Name}.{resourceFileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new Exception($"Embedded file '{resourceName}' was not found in the tool package.");
        using var fileStream = File.Create(targetFilePath);
        stream.CopyTo(fileStream);
    }

    /// <summary>
    /// Copies a single file from a source directory to a target directory with a different name.
    /// </summary>
    private static void CopyFrom(string sourceDirectory, string targetDirectory, string from, string to)
    {
        var sourceFilePath = Path.Combine(sourceDirectory, from);
        var targetFilePath = Path.Combine(targetDirectory, to);

        if (File.Exists(sourceFilePath))
        {
            File.Copy(sourceFilePath, targetFilePath, overwrite: true);
        }
        else
        {
            throw new Exception($"File '{from}' does not exist in the source directory '{sourceDirectory}'.");
        }
    }
}
