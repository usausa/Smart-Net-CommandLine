namespace Smart.CommandLine.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

public sealed class CommandHostTests
{
    //--------------------------------------------------------------------------------
    // Dispose
    //--------------------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsyncServiceProviderDisposesRegisteredSingletons()
    {
        // Arrange
        var builder = CommandHost.CreateBuilder([]);
        builder.Services.AddSingleton<DisposableProbe>();

        var host = builder.Build();
        var probe = host.Services.GetRequiredService<DisposableProbe>();

        // Act
        await host.DisposeAsync();

        // Assert
        Assert.True(probe.Disposed);
    }

    [Fact]
    public async Task DisposeAsyncConfigurationSourceDisposesCustomProvider()
    {
        // Arrange
        var source = new TrackingConfigurationSource();
        var builder = CommandHost.CreateBuilder([]);
        ((IConfigurationBuilder)builder.Configuration).Add(source);

        var host = builder.Build();

        // Act
        await host.DisposeAsync();

        // Assert
        Assert.True(source.Provider.Disposed);
    }

    [Fact]
    public async Task DisposeAsyncJsonFileWithReloadOnChangeReleasesFileWatcher()
    {
        // Arrange
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "appsettings.json");
        await File.WriteAllTextAsync(file, """{"Key":"Value"}""", TestContext.Current.CancellationToken);

        var builder = CommandHost.CreateBuilder([]);
        using var fileProvider = new TrackingPhysicalFileProvider(dir);
        builder.Configuration.AddJsonFile(fileProvider, "appsettings.json", optional: false, reloadOnChange: true);

        var host = builder.Build();

        var config = host.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("Value", config["Key"]);

        await host.DisposeAsync();

        Assert.True(fileProvider.Disposed);
        File.Delete(file);
        Directory.Delete(dir);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task DisposeAsyncContentRootFileProviderDisposesProvider()
    {
        var builder = CommandHost.CreateBuilder([]);
        var originalProvider = Assert.IsType<PhysicalFileProvider>(builder.Environment.ContentRootFileProvider);
        var fileProvider = new TrackingPhysicalFileProvider(builder.Environment.ContentRootPath);
        builder.Environment.ContentRootFileProvider = fileProvider;
        originalProvider.Dispose();
        _ = fileProvider.Watch("*");

        var host = builder.Build();
        await host.DisposeAsync();

        Assert.True(fileProvider.Disposed);
    }

    [Fact]
    public async Task DisposeAsyncRepeatedLifecycleNoLeak()
    {
        // Arrange
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "appsettings.json");
        await File.WriteAllTextAsync(file, """{"Key":"Value"}""", TestContext.Current.CancellationToken);

        // Act: 10 cycles
        for (var i = 0; i < 10; i++)
        {
            var builder = CommandHost.CreateBuilder([]);
            builder.Configuration.AddJsonFile(file, optional: false, reloadOnChange: true);
            var host = builder.Build();
            await host.DisposeAsync();
        }

        // Assert: no leaked watcher locks — delete succeeds
        File.Delete(file);
        Directory.Delete(dir);
        Assert.False(Directory.Exists(dir));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    public sealed class DisposableProbe : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class TrackingPhysicalFileProvider(string root) : PhysicalFileProvider(root)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Disposed = disposing;
        }
    }

    private sealed class TrackingConfigurationSource : IConfigurationSource
    {
        public TrackingProvider Provider { get; } = new();

        public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
    }

    public sealed class TrackingProvider : ConfigurationProvider, IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}

public sealed class CommandHostBuilderEnvironmentTests
{
    //--------------------------------------------------------------------------------
    // Environment
    //--------------------------------------------------------------------------------

    [Fact]
    public void CreateBuilderWithDotnetEnvironmentVarSetsEnvironmentName()
    {
        // Arrange
        var original = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "FromVar");

            // Act
            var builder = CommandHost.CreateBuilder([]);

            // Assert
            Assert.Equal("FromVar", builder.Environment.EnvironmentName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", original);
        }
    }

    [Fact]
    public void CreateBuilderEmptyArgsNoEnvVarDefaultsToProduction()
    {
        // Arrange
        var original = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);

            // Act
            var builder = CommandHost.CreateBuilder([]);

            // Assert
            Assert.Equal("Production", builder.Environment.EnvironmentName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", original);
        }
    }
}
