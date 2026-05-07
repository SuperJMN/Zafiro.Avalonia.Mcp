using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class ToolPackageDependencyTests
{
    [Fact]
    public void ToolProject_DoesNotReferenceAvaloniaDesktopOrAppHost()
    {
        var projectPath = FindRepositoryFile("src/Zafiro.Avalonia.Mcp.Tool/Zafiro.Avalonia.Mcp.Tool.csproj");
        var projectText = File.ReadAllText(projectPath);

        Assert.DoesNotContain("Avalonia.Desktop", projectText);
        Assert.DoesNotContain("Avalonia.Markup.Xaml.Loader", projectText);
        Assert.DoesNotContain("Zafiro.Avalonia.Mcp.AppHost", projectText);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find '{relativePath}' from '{AppContext.BaseDirectory}'.");
    }
}
