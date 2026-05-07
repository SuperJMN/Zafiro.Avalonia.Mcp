using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Zafiro.Avalonia.Mcp.AppHost;

namespace Zafiro.Avalonia.Mcp.Tool.Preview;

internal static class PreviewHostCommand
{
    public const string CommandName = "__preview-host";

    public static Task<int> Run(string[] args)
    {
        var completion = new TaskCompletionSource<int>();
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(RunCore(args));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                completion.SetResult(1);
            }
        });

        if (OperatingSystem.IsWindows())
        {
            thread.SetApartmentState(ApartmentState.STA);
        }

        thread.Start();
        return completion.Task;
    }

    public static ProcessStartInfo CreateStartInfo(PreviewHostOptions options)
    {
        var start = ResolveCurrentExecutable();
        var startInfo = new ProcessStartInfo(start.FileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(options.AssemblyPath) ?? Environment.CurrentDirectory,
        };

        foreach (var argument in start.PrefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(CommandName);
        startInfo.ArgumentList.Add("--assembly");
        startInfo.ArgumentList.Add(options.AssemblyPath);
        startInfo.ArgumentList.Add("--axaml");
        startInfo.ArgumentList.Add(options.AxamlPath);
        startInfo.ArgumentList.Add("--width");
        startInfo.ArgumentList.Add(options.Width.ToString());
        startInfo.ArgumentList.Add("--height");
        startInfo.ArgumentList.Add(options.Height.ToString());

        if (!string.IsNullOrWhiteSpace(options.EntryType))
        {
            startInfo.ArgumentList.Add("--entry-type");
            startInfo.ArgumentList.Add(options.EntryType);
        }

        return startInfo;
    }

    private static int RunCore(string[] args)
    {
        var options = Parse(args);
        InstallAssemblyResolver(options.AssemblyPath);

        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(options.AssemblyPath);
        var builder = PreviewAppBuilderFactory.Create(assembly, options.EntryType)
            .UseMcpDiagnostics();

        builder.SetupWithClassicDesktopLifetime([], lifetime =>
        {
            lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        });

        if (Application.Current?.ApplicationLifetime is not ClassicDesktopStyleApplicationLifetime lifetime)
        {
            throw new InvalidOperationException("The preview host did not create a classic desktop lifetime.");
        }

        foreach (var window in lifetime.Windows.ToArray())
        {
            window.Close();
        }

        var previewWindow = CreatePreviewWindow(options, assembly, lifetime);
        lifetime.MainWindow = previewWindow;
        previewWindow.Show();

        return lifetime.Start();
    }

    private static Window CreatePreviewWindow(
        PreviewHostOptions options,
        Assembly assembly,
        IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var root = LoadAxaml(options, assembly);
        var title = $"AXAML Preview - {Path.GetFileName(options.AxamlPath)}";

        if (root is Window loadedWindow)
        {
            loadedWindow.Title = string.IsNullOrWhiteSpace(loadedWindow.Title) ? title : loadedWindow.Title;
            loadedWindow.Width = options.Width;
            loadedWindow.Height = options.Height;
            loadedWindow.Closed += (_, _) => lifetime.TryShutdown();
            return loadedWindow;
        }

        if (root is not Control control)
        {
            throw new InvalidOperationException($"AXAML root '{root.GetType().FullName}' is not a Control or Window.");
        }

        var window = new Window
        {
            Title = title,
            Width = options.Width,
            Height = options.Height,
            Content = control,
        };
        window.Closed += (_, _) => lifetime.TryShutdown();
        return window;
    }

    private static object LoadAxaml(PreviewHostOptions options, Assembly assembly)
        => PreviewAxamlLoader.Load(options.AxamlPath, assembly);

    private static PreviewHostOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{key}'.");
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for '{key}'.");
            }

            values[key[2..]] = args[++i];
        }

        var assemblyPath = RequiredFullPath(values, "assembly");
        var axamlPath = RequiredFullPath(values, "axaml");

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("Assembly file does not exist.", assemblyPath);
        }

        if (!File.Exists(axamlPath))
        {
            throw new FileNotFoundException("AXAML file does not exist.", axamlPath);
        }

        return new PreviewHostOptions(
            axamlPath,
            assemblyPath,
            values.GetValueOrDefault("entry-type"),
            ParsePositiveInt(values, "width", 1024),
            ParsePositiveInt(values, "height", 768));
    }

    private static int ParsePositiveInt(Dictionary<string, string> values, string key, int defaultValue)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
    }

    private static string RequiredFullPath(Dictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing required --{key} argument.");
        }

        return Path.GetFullPath(value);
    }

    private static void InstallAssemblyResolver(string assemblyPath)
    {
        var resolver = new AssemblyDependencyResolver(assemblyPath);

        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = resolver.ResolveAssemblyToPath(name);
            return path is null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        };

        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (assembly, libraryName) =>
        {
            var path = resolver.ResolveUnmanagedDllToPath(libraryName);
            return path is null ? IntPtr.Zero : NativeLibrary.Load(path);
        };
    }

    private static CurrentExecutable ResolveCurrentExecutable()
    {
        var processPath = Environment.ProcessPath;
        var processName = processPath is null ? string.Empty : Path.GetFileNameWithoutExtension(processPath);
        var assemblyPath = Assembly.GetExecutingAssembly().Location;

        if (!string.IsNullOrWhiteSpace(processPath) &&
            !string.Equals(processName, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return new CurrentExecutable(processPath, []);
        }

        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new InvalidOperationException("Cannot resolve the current tool assembly path.");
        }

        return new CurrentExecutable(processPath ?? "dotnet", [assemblyPath]);
    }

    private sealed record CurrentExecutable(string FileName, IReadOnlyList<string> PrefixArguments);
}
