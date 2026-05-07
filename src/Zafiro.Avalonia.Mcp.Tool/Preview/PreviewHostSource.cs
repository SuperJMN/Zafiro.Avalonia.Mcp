namespace Zafiro.Avalonia.Mcp.Tool.Preview;

internal static class PreviewHostSource
{
    public const string Code = """
        using System.Reflection;
        using System.Runtime.InteropServices;
        using System.Runtime.Loader;
        using Avalonia;
        using Avalonia.Controls;
        using Avalonia.Controls.ApplicationLifetimes;
        using Avalonia.Markup.Xaml;
        using Zafiro.Avalonia.Mcp.AppHost;

        [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Zafiro.Avalonia.Mcp.Tests")]

        internal static class Program
        {
            [STAThread]
            public static int Main(string[] args)
            {
                try
                {
                    return PreviewHost.Run(args);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }
        }

        internal static class PreviewHost
        {
            public static int Run(string[] args)
            {
                var options = Parse(args);
                InstallAssemblyResolver(options.AssemblyPath);

                var appAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(options.AssemblyPath);
                var xamlAssembly = SamePath(options.AssemblyPath, options.XamlAssemblyPath)
                    ? appAssembly
                    : AssemblyLoadContext.Default.LoadFromAssemblyPath(options.XamlAssemblyPath);
                var builder = PreviewAppBuilderFactory.Create(appAssembly, options.EntryType)
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

                var previewWindow = CreatePreviewWindow(options, xamlAssembly, lifetime);
                lifetime.MainWindow = previewWindow;
                previewWindow.Show();

                return lifetime.Start();
            }

            private static Window CreatePreviewWindow(
                PreviewHostOptions options,
                Assembly xamlAssembly,
                IClassicDesktopStyleApplicationLifetime lifetime)
            {
                var root = PreviewAxamlLoader.Load(options.AxamlPath, xamlAssembly);
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
                var xamlAssemblyPath = values.TryGetValue("xaml-assembly", out var explicitXamlAssemblyPath)
                    ? Path.GetFullPath(explicitXamlAssemblyPath)
                    : assemblyPath;
                var axamlPath = RequiredFullPath(values, "axaml");

                if (!File.Exists(assemblyPath))
                {
                    throw new FileNotFoundException("Assembly file does not exist.", assemblyPath);
                }

                if (!File.Exists(xamlAssemblyPath))
                {
                    throw new FileNotFoundException("AXAML local assembly file does not exist.", xamlAssemblyPath);
                }

                if (!File.Exists(axamlPath))
                {
                    throw new FileNotFoundException("AXAML file does not exist.", axamlPath);
                }

                return new PreviewHostOptions(
                    axamlPath,
                    assemblyPath,
                    xamlAssemblyPath,
                    values.GetValueOrDefault("entry-type"),
                    ParsePositiveInt(values, "width", 1024),
                    ParsePositiveInt(values, "height", 768));
            }

            private static bool SamePath(string left, string right)
                => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

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

                AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, libraryName) =>
                {
                    var path = resolver.ResolveUnmanagedDllToPath(libraryName);
                    path ??= FindNativeLibraryInAppBase(libraryName);
                    return path is null ? IntPtr.Zero : NativeLibrary.Load(path);
                };
            }

            private static string? FindNativeLibraryInAppBase(string libraryName)
            {
                var runtimesDirectory = Path.Combine(AppContext.BaseDirectory, "runtimes");
                if (!Directory.Exists(runtimesDirectory))
                {
                    return null;
                }

                foreach (var nativeDirectory in Directory.EnumerateDirectories(runtimesDirectory, "native", SearchOption.AllDirectories))
                {
                    var direct = Path.Combine(nativeDirectory, libraryName);
                    if (File.Exists(direct))
                    {
                        return direct;
                    }

                    var platformName = NativeLibraryName(libraryName);
                    var platformPath = Path.Combine(nativeDirectory, platformName);
                    if (File.Exists(platformPath))
                    {
                        return platformPath;
                    }
                }

                return null;
            }

            private static string NativeLibraryName(string libraryName)
            {
                if (OperatingSystem.IsWindows())
                {
                    return libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? libraryName : $"{libraryName}.dll";
                }

                if (OperatingSystem.IsMacOS())
                {
                    var name = libraryName.StartsWith("lib", StringComparison.Ordinal) ? libraryName : $"lib{libraryName}";
                    return name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.dylib";
                }

                var linuxName = libraryName.StartsWith("lib", StringComparison.Ordinal) ? libraryName : $"lib{libraryName}";
                return linuxName.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ? linuxName : $"{linuxName}.so";
            }
        }

        internal static class PreviewAppBuilderFactory
        {
            public static AppBuilder Create(Assembly assembly, string? entryType)
            {
                if (!string.IsNullOrWhiteSpace(entryType))
                {
                    var type = FindType(assembly, entryType);
                    return CreateFromType(type);
                }

                var builderTypes = assembly.GetTypes()
                    .Where(HasBuildAvaloniaApp)
                    .ToArray();

                if (builderTypes.Length == 1)
                {
                    return InvokeBuildAvaloniaApp(builderTypes[0]);
                }

                if (builderTypes.Length > 1)
                {
                    throw new InvalidOperationException(
                        "Multiple BuildAvaloniaApp entry types were found. Pass entryType. Candidates: " +
                        string.Join(", ", builderTypes.Select(x => x.FullName)));
                }

                var applicationTypes = assembly.GetTypes()
                    .Where(type => type is { IsAbstract: false } && typeof(Application).IsAssignableFrom(type))
                    .ToArray();

                return applicationTypes.Length switch
                {
                    1 => ConfigureApplication(applicationTypes[0]).UsePlatformDetect(),
                    0 => throw new InvalidOperationException("No BuildAvaloniaApp method or Application subclass was found. Pass entryType."),
                    _ => throw new InvalidOperationException(
                        "Multiple Application subclasses were found. Pass entryType. Candidates: " +
                        string.Join(", ", applicationTypes.Select(x => x.FullName)))
                };
            }

            private static AppBuilder CreateFromType(Type type)
            {
                if (HasBuildAvaloniaApp(type))
                {
                    return InvokeBuildAvaloniaApp(type);
                }

                if (typeof(Application).IsAssignableFrom(type))
                {
                    return ConfigureApplication(type).UsePlatformDetect();
                }

                throw new InvalidOperationException(
                    $"Entry type '{type.FullName}' must expose a static parameterless BuildAvaloniaApp method or derive from Avalonia.Application.");
            }

            private static Type FindType(Assembly assembly, string entryType)
            {
                var exact = assembly.GetType(entryType, throwOnError: false, ignoreCase: false);
                if (exact is not null)
                {
                    return exact;
                }

                var matches = assembly.GetTypes()
                    .Where(type => string.Equals(type.Name, entryType, StringComparison.Ordinal) ||
                                   string.Equals(type.FullName, entryType, StringComparison.Ordinal))
                    .ToArray();

                return matches.Length switch
                {
                    1 => matches[0],
                    0 => throw new InvalidOperationException($"Entry type '{entryType}' was not found in '{assembly.GetName().Name}'."),
                    _ => throw new InvalidOperationException($"Entry type '{entryType}' is ambiguous. Use the full type name.")
                };
            }

            private static bool HasBuildAvaloniaApp(Type type)
            {
                var method = GetBuildAvaloniaApp(type);
                return method is not null &&
                       method.GetParameters().Length == 0 &&
                       typeof(AppBuilder).IsAssignableFrom(method.ReturnType);
            }

            private static AppBuilder InvokeBuildAvaloniaApp(Type type)
            {
                var method = GetBuildAvaloniaApp(type)
                             ?? throw new InvalidOperationException($"Entry type '{type.FullName}' does not expose BuildAvaloniaApp.");

                return method.Invoke(null, null) as AppBuilder
                       ?? throw new InvalidOperationException($"Entry type '{type.FullName}' returned null from BuildAvaloniaApp.");
            }

            private static MethodInfo? GetBuildAvaloniaApp(Type type)
                => type.GetMethod(
                    "BuildAvaloniaApp",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null,
                    types: [],
                    modifiers: null);

            private static AppBuilder ConfigureApplication(Type applicationType)
            {
                var configure = typeof(AppBuilder)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(method => method.Name == nameof(AppBuilder.Configure) &&
                                      method.IsGenericMethodDefinition &&
                                      method.GetParameters().Length == 0);

                return (AppBuilder)configure.MakeGenericMethod(applicationType).Invoke(null, null)!;
            }
        }

        internal static class PreviewAxamlLoader
        {
            public static object Load(string axamlPath, Assembly assembly)
            {
                using var stream = File.OpenRead(axamlPath);
                var uri = new Uri(axamlPath, UriKind.Absolute);

                var root = AvaloniaRuntimeXamlLoader.Load(stream, assembly, rootInstance: null, uri, designMode: true)
                           ?? throw new InvalidOperationException($"AXAML loader returned null for '{axamlPath}'.");

                if (root is Control control && Design.GetDataContext(control) is { } designDataContext)
                {
                    control.DataContext = designDataContext;
                }

                return root;
            }
        }

        internal sealed record PreviewHostOptions(
            string AxamlPath,
            string AssemblyPath,
            string XamlAssemblyPath,
            string? EntryType,
            int Width,
            int Height);
        """;
}
