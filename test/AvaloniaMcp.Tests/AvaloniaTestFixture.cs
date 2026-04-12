using Avalonia;
using Avalonia.Headless;
using Xunit;

namespace AvaloniaMcp.Tests;

public class AvaloniaTestFixture : IDisposable
{
    private static bool _initialized;
    private static readonly object Lock = new();

    public AvaloniaTestFixture()
    {
        lock (Lock)
        {
            if (!_initialized)
            {
                AppBuilder.Configure<TestApp>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
                _initialized = true;
            }
        }
    }

    public void Dispose() { }
}

public class TestApp : Application { }

[CollectionDefinition("Avalonia")]
public class AvaloniaCollection : ICollectionFixture<AvaloniaTestFixture> { }
