namespace Smart.CommandLine.Hosting;

using Microsoft.Extensions.DependencyInjection;

public sealed class CommandScopeTests
{
    //--------------------------------------------------------------------------------
    // Scope
    //--------------------------------------------------------------------------------

    [Fact]
    public async Task RunAsyncCreatesScopePerExecution()
    {
        // Arrange
        var recorder = new ScopeRecorder();
        await using var host = CreateBuilder(recorder, ["run"]).Build();

        // Act
        await host.RunAsync();
        await host.RunAsync();

        // Assert
        Assert.Equal(2, recorder.Handlers.Count);
        Assert.Equal(2, recorder.Filters.Count);
        Assert.NotSame(recorder.Handlers[0], recorder.Handlers[1]);

        // Scoped service is created per execution
        Assert.NotSame(recorder.Handlers[0].Scoped, recorder.Handlers[1].Scoped);

        // Handler and filter share the same scope within an execution
        Assert.Same(recorder.Handlers[0].Scoped, recorder.Filters[0].Scoped);
        Assert.Same(recorder.Handlers[1].Scoped, recorder.Filters[1].Scoped);

        // Singleton service is shared between executions
        Assert.Same(recorder.Handlers[0].Singleton, recorder.Handlers[1].Singleton);
    }

    [Fact]
    public async Task RunAsyncDisposesScopedServicesAtEndOfExecution()
    {
        // Arrange
        var recorder = new ScopeRecorder();
        await using var host = CreateBuilder(recorder, ["run"]).Build();

        // Act
        await host.RunAsync();

        // Assert
        var handler = recorder.Handlers[0];
        Assert.True(handler.Disposed);
        Assert.True(handler.Scoped.Disposed);
        Assert.True(handler.Transient.Disposed);
        Assert.True(recorder.Filters[0].Disposed);
        Assert.False(handler.Singleton.Disposed);
    }

    [Fact]
    public async Task DisposeAsyncDisposesSingletonAfterExecution()
    {
        // Arrange
        var recorder = new ScopeRecorder();
        var host = CreateBuilder(recorder, ["run"]).Build();

        await host.RunAsync();

        var singleton = recorder.Handlers[0].Singleton;
        Assert.False(singleton.Disposed);

        // Act
        await host.DisposeAsync();

        // Assert
        Assert.True(singleton.Disposed);
    }

    [Fact]
    public async Task CommandContextServiceProviderIsExecutionScope()
    {
        // Arrange
        var recorder = new ScopeRecorder();
        await using var host = CreateBuilder(recorder, ["run"]).Build();

        // Act
        await host.RunAsync();

        // Assert
        var provider = Assert.Single(recorder.ContextProviders);
        Assert.NotSame(host.Services, provider);
        Assert.Same(recorder.Handlers[0].Scoped, recorder.ContextScoped[0]);
    }

    //--------------------------------------------------------------------------------
    // Validation
    //--------------------------------------------------------------------------------

    [Fact]
    public void BuildWithDevelopmentEnvironmentDetectsCapturedScopedService()
    {
        // Arrange
        var builder = CommandHost.CreateBuilder([]);
        builder.Environment.EnvironmentName = "Development";
        builder.Services.AddScoped<ScopedProbe>();
        builder.Services.AddSingleton<CaptiveSingleton>();

        // Act & Assert
        Assert.Throws<AggregateException>(() => { _ = builder.Build(); });
    }

    [Fact]
    public async Task BuildWithProductionEnvironmentIgnoresCapturedScopedService()
    {
        // Arrange
        var builder = CommandHost.CreateBuilder([]);
        builder.Environment.EnvironmentName = "Production";
        builder.Services.AddScoped<ScopedProbe>();
        builder.Services.AddSingleton<CaptiveSingleton>();

        // Act
        await using var host = builder.Build();

        // Assert
        var captive = host.Services.GetRequiredService<CaptiveSingleton>();
        Assert.NotNull(captive.Scoped);
    }

    //--------------------------------------------------------------------------------
    // Registration
    //--------------------------------------------------------------------------------

    [Fact]
    public async Task AddCommandDoesNotOverrideExistingRegistration()
    {
        // Arrange
        var recorder = new ScopeRecorder();
        var builder = CommandHost.CreateBuilder(["solo"]);
        builder.Environment.EnvironmentName = "Production";
        builder.Services.AddSingleton(recorder);
        builder.Services.AddSingleton<RegistrationProbeCommand>();
        builder.ConfigureCommands(static commands => commands.AddCommand<RegistrationProbeCommand>());

        await using var host = builder.Build();

        // Act
        await host.RunAsync();
        await host.RunAsync();

        // Assert
        var descriptor = Assert.Single(builder.Services, static x => x.ServiceType == typeof(RegistrationProbeCommand));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(2, recorder.Executions.Count);
        Assert.Same(recorder.Executions[0], recorder.Executions[1]);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static ICommandHostBuilder CreateBuilder(ScopeRecorder recorder, string[] args)
    {
        var builder = CommandHost.CreateBuilder(args);
        builder.Environment.EnvironmentName = "Production";

        builder.Services.AddSingleton(recorder);
        builder.Services.AddSingleton<SingletonProbe>();
        builder.Services.AddScoped<ScopedProbe>();
        builder.Services.AddTransient<TransientProbe>();

        builder.ConfigureCommands(static commands =>
        {
            commands.AddGlobalFilter<ScopeProbeFilter>();
            commands.AddCommand<ScopeProbeCommand>();
        });

        return builder;
    }
}

//--------------------------------------------------------------------------------
// Probe
//--------------------------------------------------------------------------------

public sealed class ScopeRecorder
{
    public List<ScopeProbeCommand> Handlers { get; } = [];

    public List<ScopeProbeFilter> Filters { get; } = [];

    public List<IServiceProvider> ContextProviders { get; } = [];

    public List<ScopedProbe> ContextScoped { get; } = [];

    public List<RegistrationProbeCommand> Executions { get; } = [];
}

public sealed class SingletonProbe : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

public sealed class ScopedProbe : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

public sealed class TransientProbe : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

public sealed class CaptiveSingleton
{
    public ScopedProbe Scoped { get; }

    public CaptiveSingleton(ScopedProbe scoped)
    {
        Scoped = scoped;
    }
}

public sealed class ScopeProbeFilter : ICommandFilter, IDisposable
{
    public ScopedProbe Scoped { get; }

    public bool Disposed { get; private set; }

    public ScopeProbeFilter(ScopeRecorder recorder, ScopedProbe scoped)
    {
        Scoped = scoped;
        recorder.Filters.Add(this);
    }

    public ValueTask ExecuteAsync(CommandContext context, CommandDelegate next) => next(context);

    public void Dispose() => Disposed = true;
}

[Command("run", "Scope probe command")]
public sealed class ScopeProbeCommand : ICommandHandler, IDisposable
{
    private readonly ScopeRecorder recorder;

    public ScopedProbe Scoped { get; }

    public TransientProbe Transient { get; }

    public SingletonProbe Singleton { get; }

    public bool Disposed { get; private set; }

    public ScopeProbeCommand(ScopeRecorder recorder, ScopedProbe scoped, TransientProbe transient, SingletonProbe singleton)
    {
        this.recorder = recorder;
        Scoped = scoped;
        Transient = transient;
        Singleton = singleton;
        recorder.Handlers.Add(this);
    }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        recorder.ContextProviders.Add(context.ServiceProvider);
        recorder.ContextScoped.Add(context.ServiceProvider.GetRequiredService<ScopedProbe>());
        return ValueTask.CompletedTask;
    }

    public void Dispose() => Disposed = true;
}

[Command("solo", "Registration probe command")]
public sealed class RegistrationProbeCommand : ICommandHandler
{
    private readonly ScopeRecorder recorder;

    public RegistrationProbeCommand(ScopeRecorder recorder)
    {
        this.recorder = recorder;
    }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        recorder.Executions.Add(this);
        return ValueTask.CompletedTask;
    }
}
