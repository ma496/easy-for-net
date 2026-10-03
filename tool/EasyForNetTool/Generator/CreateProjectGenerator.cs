namespace EasyForNetTool.Generator;

using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    /// The PostgreSQL password the Development and Testing connection strings are written with - the
    /// default the project's own <c>docker-compose.yml</c> starts PostgreSQL with.
    /// </summary>
    internal const string DevelopmentDatabasePassword = "postgres";

    /// <summary>
    /// Rewrites the copied <c>appsettings.json</c>, <c>appsettings.Development.json</c> and
    /// <c>appsettings.Testing.json</c> for a new project: its databases, fresh JWT keys for
    /// Development and Testing, its own Redis key prefix so several applications can share one
    /// Redis server (<c>&lt;Name&gt;:</c>, and <c>&lt;Name&gt;Test:</c> for Testing), and a random
    /// password for the seeded administrators in all three files.
    /// </summary>
    /// <remarks>
    /// Development and Testing connect with the <c>docker-compose.yml</c> default password, so the
    /// project runs against <c>docker compose up -d</c> as generated; the tracked
    /// <c>appsettings.json</c> keeps its <c>{password}</c> placeholder.
    /// </remarks>
    /// <param name="backendProjectTargetPath">The generated backend project directory holding the appsettings files.</param>
    /// <param name="pascalCaseProjectName">The PascalCase project name.</param>
    /// <returns>The seeded administrators' password.</returns>
    internal static async Task<string> CustomizeAppSettingsAsync(string backendProjectTargetPath, string pascalCaseProjectName)
    {
        var appSettings = Path.Combine(backendProjectTargetPath, "appsettings.json");
        var developmentSettings = Path.Combine(backendProjectTargetPath, "appsettings.Development.json");
        var testingSettings = Path.Combine(backendProjectTargetPath, "appsettings.Testing.json");
        var connectionString = $"Host=localhost;Port=5432;Database={pascalCaseProjectName};Username=postgres;Password={{password}}";
        var developmentConnectionString = $"Host=localhost;Port=5432;Database={pascalCaseProjectName};Username=postgres;Password={DevelopmentDatabasePassword}";
        var testConnectionString = $"Host=localhost;Port=5432;Database={pascalCaseProjectName}Test;Username=postgres;Password={DevelopmentDatabasePassword}";
        var adminPassword = Secrets.Password(16);

        await JsonPropertyUpdater.UpdateJsonPropertyAsync(appSettings, "ConnectionStrings.DefaultConnection", connectionString);
        await JsonPropertyUpdater.UpdateJsonPropertyAsync(appSettings, "Hangfire.Storage.ConnectionString", connectionString);
        await JsonPropertyUpdater.UpdateJsonPropertyAsync(appSettings, "Redis.InstanceName", $"{pascalCaseProjectName}:");

        await JsonPropertyUpdater.UpdateJsonPropertyAsync(developmentSettings, "Auth.Jwt.Key", Guid.NewGuid().ToString());
        await JsonPropertyUpdater.UpdateJsonPropertyAsync(developmentSettings, "ConnectionStrings.DefaultConnection", developmentConnectionString);
        await JsonPropertyUpdater.UpdateJsonPropertyAsync(developmentSettings, "Hangfire.Storage.ConnectionString", developmentConnectionString);
        await JsonPropertyUpdater.UpdateJsonPropertyAsync(developmentSettings, "Redis.InstanceName", $"{pascalCaseProjectName}:");

        await JsonPropertyUpdater.UpdateJsonPropertyAsync(testingSettings, "Auth.Jwt.Key", Guid.NewGuid().ToString());
        await JsonPropertyUpdater.UpdateJsonPropertyAsync(testingSettings, "ConnectionStrings.DefaultConnection", testConnectionString);
        await JsonPropertyUpdater.UpdateJsonPropertyAsync(testingSettings, "Hangfire.Storage.ConnectionString", testConnectionString);
        await JsonPropertyUpdater.UpdateJsonPropertyAsync(testingSettings, "Redis.InstanceName", $"{pascalCaseProjectName}Test:");

        foreach (var settings in new[] { appSettings, developmentSettings, testingSettings })
        {
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(settings, "Seed.PlatformAdminPassword", adminPassword);
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(settings, "Seed.TenantAdminPassword", adminPassword);
        }

        return adminPassword;
    }

    /// <summary>
    /// Writes the project's root <c>.env</c> - the values <c>docker-compose.prod.yml</c> reads - from
    /// the template's <c>.env.docker.example</c>, keeping its comments: the project's own Compose
    /// project, database and Redis key prefix, fresh random database, Redis and administrator
    /// passwords and JWT signing key, and <c>localhost</c> as the domain so the stack starts as
    /// generated. Values the example leaves for the developer (SMTP) are copied as they are.
    /// </summary>
    /// <param name="examplePath">The template's <c>.env.docker.example</c>.</param>
    /// <param name="envPath">The <c>.env</c> file to create.</param>
    /// <param name="kebabCaseProjectName">The kebab-case project name.</param>
    /// <param name="pascalCaseProjectName">The PascalCase project name.</param>
    internal static async Task WriteDockerEnvAsync(string examplePath, string envPath, string kebabCaseProjectName, string pascalCaseProjectName)
    {
        var snakeCaseProjectName = kebabCaseProjectName.Replace('-', '_');
        var values = new Dictionary<string, string>
        {
            ["PROD_PROJECT_NAME"] = kebabCaseProjectName,
            ["DOMAIN"] = "localhost",
            ["PUBLIC_URL"] = "https://localhost",
            ["POSTGRES_DB"] = snakeCaseProjectName,
            ["POSTGRES_USER"] = snakeCaseProjectName,
            ["POSTGRES_PASSWORD"] = Secrets.Alphanumeric(32),
            ["REDIS_PASSWORD"] = Secrets.Alphanumeric(32),
            ["REDIS_INSTANCE_NAME"] = $"{pascalCaseProjectName}:",
            ["JWT_KEY"] = Secrets.Base64(48),
            // letters and digits only: a "#" could be read as the start of a comment by an env-file parser
            ["SEED_ADMIN_PASSWORD"] = Secrets.Alphanumeric(20),
        };

        var content = await File.ReadAllTextAsync(examplePath);
        foreach (var (key, value) in values)
            content = Regex.Replace(content, $@"(?m)^{Regex.Escape(key)}=[^\r\n]*", $"{key}={value.Replace("$", "$$")}");

        await File.WriteAllTextAsync(envPath, content);
    }

    /// <summary>
    /// Removes every culture but <paramref name="keptLocales"/> from the web app's
    /// <c>i18n/offline-resources.json</c> - the strings bundled for when the API is down, keyed by
    /// culture - so it names exactly the locales the project routes to.
    /// </summary>
    /// <param name="offlineResourcesPath">The path of the generated web app's offline resources file.</param>
    /// <param name="keptLocales">The culture codes to keep.</param>
    internal static async Task KeepOfflineResourceLocalesAsync(string offlineResourcesPath, IReadOnlyCollection<string> keptLocales)
    {
        if (!File.Exists(offlineResourcesPath))
            return;

        var resources = JsonNode.Parse(await File.ReadAllTextAsync(offlineResourcesPath))!.AsObject();
        foreach (var locale in resources.Select(entry => entry.Key).Where(locale => !keptLocales.Contains(locale)).ToList())
            resources.Remove(locale);

        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        await File.WriteAllTextAsync(offlineResourcesPath, resources.ToJsonString(options) + "\n");
    }

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
            CopyFiles(versionedTemplateDir, targetPath, ".editorconfig", ".gitignore", ".gitattributes", "global.json", "package.json", "agentic.config.json",
                "docker-compose.yml", "docker-compose.prod.yml", "docker-compose.coolify.yml", ".env.docker.example");
            // .env is git-ignored in the template, so it is written from the tracked example with this project's values
            await WriteDockerEnvAsync(Path.Combine(versionedTemplateDir, ".env.docker.example"), Path.Combine(targetPath, ".env"), kebabCaseProjectName, pascalCaseProjectName);
            CopyDirectory($"{versionedTemplateDir}/docker", $"{targetPath}/docker", true);
            CopyDirectory($"{versionedTemplateDir}/.config", $"{targetPath}/.config", true);
            CopyDirectory($"{versionedTemplateDir}/.vscode", $"{targetPath}/.vscode", true);
            // the new-project and template-maintenance skills describe working on the template
            // repository itself, so they are of no use inside a generated project; the lessons
            // the task loop recorded are about the template repository too
            CopyDirectory($"{versionedTemplateDir}/.claude", $"{targetPath}/.claude", true, ["new-project", "template-maintenance", "lessons"]);
            WriteEmbeddedFile("new-project-claude.md", Path.Combine(targetPath, "CLAUDE.md"));
            WriteEmbeddedFile("new-project-readme.md", Path.Combine(targetPath, "README.md"));
            // the spec-driven task loop: its engine ships whole, and the places it records work
            // ship empty, so a new project starts with no specs, queue, build records or lessons
            CopyDirectory($"{versionedTemplateDir}/scripts", $"{targetPath}/scripts", true);
            CopyTaskLoopSkeleton(versionedTemplateDir, targetPath);

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
            // update connection strings, the JWT keys, the Redis key prefixes and the seeded administrators' password
            var adminPassword = await CustomizeAppSettingsAsync(backendProjectTargetPath, pascalCaseProjectName);
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
            // replace Easy For Net text with project name in web project and in the shipped backend
            // locale resources - the brand name a caller reads back from GET /localization/resources
            // is one of the strings this project name has to reach.
            var titleCaseProjectName = kebabCaseProjectName.Split('-').Select(x => char.ToUpper(x[0]) + x[1..]).Aggregate((current, next) => current + " " + next);
            await ReplaceInFiles(webTargetPath, @"Easy\s+For\s+Net", titleCaseProjectName, ".json");
            await ReplaceInFiles(Path.Combine(backendProjectTargetPath, "Features", "Localization", "Core", "Resources"), @"Easy\s+For\s+Net", titleCaseProjectName, ".json");
            // update package.json and package-lock.json
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(webTargetPath, "package.json"), "name", kebabCaseProjectName);
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(webTargetPath, "package-lock.json"), "name", kebabCaseProjectName);
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(webTargetPath, "package-lock.json"), "packages..name", kebabCaseProjectName);
            // the workspace scripts and the task loop name the project in briefs, the status line and the scheduled task
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(targetPath, "package.json"), "name", kebabCaseProjectName);
            await JsonPropertyUpdater.UpdateJsonPropertyAsync(Path.Combine(targetPath, "agentic.config.json"), "project.name", kebabCaseProjectName);
            // update CLAUDE.md and README.md
            await ReplaceInFile(Path.Combine(targetPath, "CLAUDE.md"), @"EasyForNet\.slnx", $@"{pascalCaseProjectName}.slnx");
            await ReplaceInFile(Path.Combine(targetPath, "README.md"), @"\{\{Name\}\}", pascalCaseProjectName);
            await ReplaceInFile(Path.Combine(targetPath, "README.md"), @"\{\{name\}\}", kebabCaseProjectName);
            // update the skill guides, which reference the template's namespaces and solution file
            var claudeSkillsPath = Path.Combine(targetPath, ".claude");
            await ReplaceInFiles(claudeSkillsPath, $@"{Regex.Escape(backendProjectRootNamespace)}\.", $@"{pascalCaseProjectName}.", ".md");
            await ReplaceInFiles(claudeSkillsPath, @"EasyForNet\.slnx", $@"{pascalCaseProjectName}.slnx", ".md");

            // Cleanup localization files when multiLanguage is false: the API's shipped resource files
            // decide which cultures exist, and the web's routing locale set must name the same ones.
            if (!argument.MultiLanguage)
            {
                Console.WriteLine("Cleaning up localization files...");

                // 1. Delete non-English shipped locale resources, so the single-language project embeds
                // only what it routes for.
                var localesPath = Path.Combine(backendProjectTargetPath, "Features", "Localization", "Core", "Resources");
                foreach (var locale in new[] { "ur", "zh", "ar", "hi", "es", "fr", "ru" })
                {
                    var filePath = Path.Combine(localesPath, $"{locale}.json");
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }

                // 2. Update config.ts - set locales to only ['en']
                var configTsPath = Path.Combine(webTargetPath, "i18n", "config.ts");
                if (File.Exists(configTsPath))
                {
                    var content = await File.ReadAllTextAsync(configTsPath);
                    content = Regex.Replace(content, @"locales: \[[^\]]+\]", "locales: ['en']");
                    await File.WriteAllTextAsync(configTsPath, content);
                }

                // 3. Keep only English in the web app's offline fallback strings
                await KeepOfflineResourceLocalesAsync(Path.Combine(webTargetPath, "i18n", "offline-resources.json"), ["en"]);
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

            // the task loop reads diffs and refuses a dirty tree, so the project starts as a repository with one commit
            await InitializeGitRepositoryAsync(targetPath);

            Console.WriteLine("Project creation completed successfully!");
            Console.WriteLine();
            Console.WriteLine($"  Seeded accounts: admin and tenantadmin, password {adminPassword}");
            Console.WriteLine("  (the Seed section of the appsettings files; .env holds the production stack's own secrets)");
            Console.WriteLine();
            Console.WriteLine("  Next steps:");
            Console.WriteLine($"    cd {kebabCaseProjectName}");
            Console.WriteLine("    docker compose up -d");
            Console.WriteLine("    dotnet tool restore");
            Console.WriteLine($"    dotnet build {pascalCaseProjectName}.slnx");
            Console.WriteLine("    dotnet ef migrations add Initial --project src/backend/Source");
            Console.WriteLine("    dotnet run --project src/backend/Source");
            Console.WriteLine("    cd src/frontend/web && npm install && npm run dev");
            Console.WriteLine();
            Console.WriteLine("  README.md has the rest.");
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
    /// Makes the generated project a git repository holding everything generated in one commit. The
    /// project is complete either way, so a failure here is reported and generation carries on: with
    /// no git, nothing is done; when the commit is refused (no <c>user.name</c> configured, say), the
    /// repository is left initialized with the command to finish it.
    /// </summary>
    /// <param name="targetPath">The generated project's root directory.</param>
    private static async Task InitializeGitRepositoryAsync(string targetPath)
    {
        if (!IsGitInstalled())
        {
            Console.WriteLine("Git was not found, so no repository was created. Run `git init` in the project before using the task loop.");
            return;
        }

        Console.WriteLine("Creating git repository...");
        try
        {
            await ExecuteCommand("git", $"-C \"{targetPath}\" init");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: could not create a git repository: {ex.Message}");
            return;
        }

        try
        {
            await ExecuteCommand("git", $"-C \"{targetPath}\" add .");
            await ExecuteCommand("git", $"-C \"{targetPath}\" commit -m \"Initial project\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: the repository was created but the initial commit failed: {ex.Message}");
            Console.WriteLine("Finish it with: git add . && git commit -m \"Initial project\"");
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
    /// Lays out the places the spec-driven task loop records its work - <c>specs/</c>, <c>docs/</c>,
    /// the <c>.agent-queue/</c> lanes and the lessons directory - with their guides but none of the
    /// template repository's own specs, build records, queued tasks or lessons.
    /// </summary>
    private static void CopyTaskLoopSkeleton(string templateDir, string targetPath)
    {
        Directory.CreateDirectory(Path.Combine(targetPath, "specs"));
        CopyFiles(Path.Combine(templateDir, "specs"), Path.Combine(targetPath, "specs"), "README.md", "TEMPLATE.md");

        Directory.CreateDirectory(Path.Combine(targetPath, "docs"));
        CopyFiles(Path.Combine(templateDir, "docs"), Path.Combine(targetPath, "docs"), "AGENTIC_WORKFLOW.md");
        foreach (var ledger in new[] { "builds", "capabilities" })
        {
            Directory.CreateDirectory(Path.Combine(targetPath, "docs", ledger));
            CopyFiles(Path.Combine(templateDir, "docs", ledger), Path.Combine(targetPath, "docs", ledger), "README.md");
        }

        foreach (var lane in new[] { "todo", "doing", "done", "failed" })
        {
            var laneDir = Directory.CreateDirectory(Path.Combine(targetPath, ".agent-queue", lane)).FullName;
            File.WriteAllText(Path.Combine(laneDir, ".gitkeep"), string.Empty);
        }
        File.WriteAllText(Path.Combine(targetPath, ".agent-queue", "planned.json"), "{}\n");

        var lessonsDir = Directory.CreateDirectory(Path.Combine(targetPath, ".claude", "memory", "lessons")).FullName;
        File.WriteAllText(Path.Combine(lessonsDir, ".gitkeep"), string.Empty);
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
