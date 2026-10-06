namespace EasyForNetTool.Tests.Generator;

using EasyForNetTool.Generator;

/// <summary>
/// Unit tests for <see cref="CreateProjectGenerator.CopyTaskLoopSkeleton"/>, run against the template's own
/// task-loop files, so a new project starts with the loop's guides and an empty shared queue - and none
/// of the template repository's own work.
/// </summary>
public class CopyTaskLoopSkeletonTests
{
    /// <summary>
    /// Tests that the skeleton holds exactly the guides and an empty planned.json - no lane, and no build record.
    /// </summary>
    [Fact]
    public void Should_Lay_Out_Only_The_Shared_Skeleton()
    {
        // Arrange
        var templateDir = Path.GetDirectoryName(Path.GetDirectoryName(CustomizeAppSettingsTests.FindTemplateFile("specs", "TEMPLATE.md")))!;
        var target = Path.Combine(Path.GetTempPath(), "efn-skeleton-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Act
            CreateProjectGenerator.CopyTaskLoopSkeleton(templateDir, target);

            // Assert
            var files = Directory.GetFiles(target, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(target, f).Replace('\\', '/'))
                .Order()
                .ToArray();
            Assert.Equal(
                [
                    ".agent-queue/planned.json",
                    ".claude/memory/lessons/.gitkeep",
                    "docs/AGENTIC_WORKFLOW.md",
                    "docs/capabilities/README.md",
                    "specs/README.md",
                    "specs/TEMPLATE.md",
                ],
                files);
            Assert.Equal("{}\n", File.ReadAllText(Path.Combine(target, ".agent-queue", "planned.json")));
            Assert.False(Directory.Exists(Path.Combine(target, "docs", "builds")));
        }
        finally
        {
            if (Directory.Exists(target))
                Directory.Delete(target, recursive: true);
        }
    }
}
