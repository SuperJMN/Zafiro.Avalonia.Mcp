using System.Text.Json;
using Xunit;
using Zafiro.Avalonia.Mcp.Tool.Connection;
using Zafiro.Avalonia.Mcp.Tool.Preview;
using Zafiro.Avalonia.Mcp.Tool.Tools;

namespace Zafiro.Avalonia.Mcp.Tests.Preview;

public sealed class PreviewAxamlMultiAssemblyIntegrationTests
{
    [Fact]
    public async Task PreviewAxaml_ConnectsToDesktopHost_AndLoadsAxamlFromReferencedUiAssembly()
    {
        using var fixture = new MultiAssemblyPreviewFixture();
        using var pool = new ConnectionPool();
        using var previews = new PreviewProcessManager(
            discoveryTimeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromMilliseconds(100));

        try
        {
            var previewResult = await PreviewTools.PreviewAxaml(
                pool,
                previews,
                new PreviewTargetResolver(new DotnetProcessRunner()),
                fixture.ViewPath,
                projectPath: fixture.HostProjectPath,
                entryType: "PreviewFixture.Host.Program",
                width: 420,
                height: 240);

            using var previewDocument = JsonDocument.Parse(previewResult);
            Assert.False(previewDocument.RootElement.TryGetProperty("error", out _), previewResult);
            Assert.True(previewDocument.RootElement.GetProperty("connected").GetBoolean());

            var screenText = await TreeTools.GetScreenText(pool);

            Assert.Contains(MultiAssemblyPreviewFixture.VisibleText, screenText);
        }
        finally
        {
            PreviewTools.ClosePreview(pool, previews);
        }
    }

    [Fact]
    public async Task PreviewAxaml_HeadlessBackend_ConnectsWithoutDisplay_AndSupportsNonPixelInspection()
    {
        using var fixture = new MultiAssemblyPreviewFixture();
        using var displayEnvironment = DisplayEnvironmentScope.Clear();
        using var pool = new ConnectionPool();
        using var previews = new PreviewProcessManager(
            discoveryTimeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromMilliseconds(100));

        try
        {
            var previewResult = await PreviewTools.PreviewAxaml(
                pool,
                previews,
                new PreviewTargetResolver(new DotnetProcessRunner()),
                fixture.ViewPath,
                projectPath: fixture.HostProjectPath,
                entryType: "PreviewFixture.Host.Program",
                width: 420,
                height: 240,
                backend: "headless");

            using var previewDocument = JsonDocument.Parse(previewResult);
            Assert.False(previewDocument.RootElement.TryGetProperty("error", out _), previewResult);
            Assert.True(previewDocument.RootElement.GetProperty("connected").GetBoolean());
            Assert.Equal("headless", previewDocument.RootElement.GetProperty("backend").GetString());

            var screenText = await TreeTools.GetScreenText(pool);
            var snapshot = await TreeTools.GetSnapshot(pool, visibleOnly: false);
            var tree = await TreeTools.GetTree(pool, depth: 4);
            var layout = await DiagnosticTools.GetLayoutInfo(pool, "TextBlock");

            Assert.Contains(MultiAssemblyPreviewFixture.VisibleText, screenText);
            Assert.Contains(MultiAssemblyPreviewFixture.VisibleText, snapshot);
            Assert.Contains("\"type\":\"Window\"", tree);
            Assert.Contains("\"bounds\"", layout);
        }
        finally
        {
            PreviewTools.ClosePreview(pool, previews);
        }
    }

    private sealed class DisplayEnvironmentScope : IDisposable
    {
        private static readonly string[] VariableNames =
        [
            "DISPLAY",
            "WAYLAND_DISPLAY",
            "XAUTHORITY",
            "XDG_RUNTIME_DIR",
            "XDG_SESSION_TYPE",
            "XDG_CURRENT_DESKTOP",
            "DESKTOP_SESSION",
            "DBUS_SESSION_BUS_ADDRESS",
            "GDK_BACKEND",
        ];

        private readonly Dictionary<string, string?> previousValues;

        private DisplayEnvironmentScope(Dictionary<string, string?> previousValues)
        {
            this.previousValues = previousValues;
        }

        public static DisplayEnvironmentScope Clear()
        {
            var previousValues = VariableNames.ToDictionary(
                variable => variable,
                Environment.GetEnvironmentVariable,
                StringComparer.Ordinal);

            foreach (var variableName in VariableNames)
            {
                Environment.SetEnvironmentVariable(variableName, null);
            }

            return new DisplayEnvironmentScope(previousValues);
        }

        public void Dispose()
        {
            foreach (var (variableName, value) in previousValues)
            {
                Environment.SetEnvironmentVariable(variableName, value);
            }
        }
    }

    private sealed class MultiAssemblyPreviewFixture : IDisposable
    {
        public const string VisibleText = "Visible from referenced UI assembly";

        public MultiAssemblyPreviewFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "avalonia-mcp-multi-assembly-preview", Guid.NewGuid().ToString("N"));
            var uiDirectory = Path.Combine(Root, "PreviewFixture.Ui");
            var hostDirectory = Path.Combine(Root, "PreviewFixture.Host");
            Directory.CreateDirectory(Path.Combine(uiDirectory, "Views"));
            Directory.CreateDirectory(hostDirectory);

            UiProjectPath = Path.Combine(uiDirectory, "PreviewFixture.Ui.csproj");
            HostProjectPath = Path.Combine(hostDirectory, "PreviewFixture.Host.csproj");
            ViewPath = Path.Combine(uiDirectory, "Views", "PreviewView.axaml");

            File.WriteAllText(UiProjectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Avalonia" Version="12.0.2" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(ViewPath, $$"""
                <UserControl xmlns="https://github.com/avaloniaui"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             x:Class="PreviewFixture.Ui.Views.PreviewView">
                  <Border Padding="12">
                    <TextBlock Text="{{VisibleText}}" />
                  </Border>
                </UserControl>
                """);
            File.WriteAllText(Path.Combine(uiDirectory, "Views", "PreviewView.axaml.cs"), """
                using Avalonia.Controls;

                namespace PreviewFixture.Ui.Views;

                public partial class PreviewView : UserControl
                {
                    public PreviewView()
                    {
                        InitializeComponent();
                    }
                }
                """);
            File.WriteAllText(HostProjectPath, $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="{{UiProjectPath}}" />
                    <PackageReference Include="Avalonia.Desktop" Version="12.0.2" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(hostDirectory, "Program.cs"), """
                using Avalonia;
                using Avalonia.Controls.ApplicationLifetimes;

                namespace PreviewFixture.Host;

                public static class Program
                {
                    public static AppBuilder BuildAvaloniaApp() =>
                        AppBuilder.Configure<App>()
                            .UsePlatformDetect();

                    public static void Main(string[] args) =>
                        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                }

                public sealed class App : Application;
                """);
        }

        public string Root { get; }
        public string UiProjectPath { get; }
        public string HostProjectPath { get; }
        public string ViewPath { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
