namespace Smart.CommandLine.Hosting;

using System.CommandLine;
using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1001
internal sealed class CommandHostBuilder : ICommandHostBuilder
{
    private readonly string[] args;

    private readonly ServiceCollection services = new();

    private readonly ConfigurationManager configuration;

    private readonly HostEnvironment environment;

    private readonly LoggingBuilder loggingBuilder;

    private Action<ICommandBuilder>? commandConfiguration;

    private Func<IServiceProvider> createServiceProvider;

    private Action<object> configureContainer = _ => { };

    public ConfigurationManager Configuration => configuration;

    public IHostEnvironment Environment => environment;

    public IServiceCollection Services => services;

    public ILoggingBuilder Logging => loggingBuilder;

    public CommandHostBuilder(string[] args)
    {
        this.args = args;

        // Default service provider factory
        createServiceProvider = () =>
        {
            configureContainer(Services);
            return Services.BuildServiceProvider();
        };

        // Environment
        var contentRootPath = AppContext.BaseDirectory;
        environment = new HostEnvironment
        {
            ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty,
            EnvironmentName = System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production",
            ContentRootPath = contentRootPath,
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath)
        };

        // Configuration
        configuration = new ConfigurationManager();

        // Logging
        loggingBuilder = new LoggingBuilder(services);

        // Add basic services
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(environment);
    }

    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull
    {
        createServiceProvider = () =>
        {
            var containerBuilder = factory.CreateBuilder(Services);
            configureContainer(containerBuilder);
            return factory.CreateServiceProvider(containerBuilder);
        };

        configureContainer = containerBuilder => configure?.Invoke((TContainerBuilder)containerBuilder);
    }

    public ICommandHostBuilder ConfigureCommands(Action<ICommandBuilder> configure)
    {
        commandConfiguration = configure;
        return this;
    }

    public ICommandHost Build()
    {
        // Execute command configuration
        var commandBuilder = new CommandBuilder(services);
        commandConfiguration?.Invoke(commandBuilder);

        var commandDescriptors = commandBuilder.GetCommandDescriptors();
        var rootDescriptor = commandBuilder.GetRootHandlerDescriptor();

        // Setup filters
        var filterTypes = new HashSet<Type>();

        var globalFilters = commandBuilder.GetGlobalFilters();
        if (globalFilters.Descriptors is not null)
        {
            foreach (var filterDescriptor in globalFilters.Descriptors)
            {
                filterTypes.Add(filterDescriptor.FilterType);
            }
        }

        if (rootDescriptor is not null)
        {
            foreach (var filterDescriptor in CommandMetadataProvider.GetFilterDescriptors(rootDescriptor.CommandType))
            {
                filterTypes.Add(filterDescriptor.FilterType);
            }
        }

        foreach (var commandDescriptor in commandDescriptors)
        {
            foreach (var filterDescriptor in CommandMetadataProvider.GetFilterDescriptors(commandDescriptor.CommandType))
            {
                filterTypes.Add(filterDescriptor.FilterType);
            }
        }

        foreach (var filterType in filterTypes)
        {
            services.TryAddTransient(filterType);
        }

        // Service provider
        var serviceProvider = createServiceProvider();

        // Root command
        var customRootCommand = commandBuilder.GetCustomRootCommand();
        var rootCommand = customRootCommand ?? new RootCommand();

        var rootCommandConfiguration = commandBuilder.GetRootCommandConfiguration();
        rootCommandConfiguration?.Invoke(rootCommand);

        // Add command handlers
        if (rootDescriptor is not null)
        {
            SetupCommandHandler(serviceProvider, globalFilters, rootCommand, rootDescriptor);
        }

        foreach (var descriptor in commandDescriptors)
        {
            var command = CreateCommand(serviceProvider, globalFilters, descriptor);
            rootCommand.Subcommands.Add(command);
        }

        return new CommandHostImplement(args, rootCommand, serviceProvider);
    }

    private static Command CreateCommand(IServiceProvider serviceProvider, FilterCollection globalFilters, CommandDescriptor descriptor)
    {
        // Create command
        var (name, description) = CommandMetadataProvider.ResolveCommandMetadata(descriptor.CommandType);
        var command = new Command(name, description);

        // Build executable command
        if (typeof(ICommandHandler).IsAssignableFrom(descriptor.CommandType))
        {
            SetupCommandHandler(serviceProvider, globalFilters, command, descriptor);
        }

        // Add sub commands
        foreach (var subDescriptor in descriptor.SubCommands)
        {
            var subCommand = CreateCommand(serviceProvider, globalFilters, subDescriptor);
            command.Subcommands.Add(subCommand);
        }

        return command;
    }

    private static void SetupCommandHandler(IServiceProvider serviceProvider, FilterCollection globalFilters, Command command, CommandDescriptor descriptor)
    {
        // Build command
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(descriptor.CommandType);
        var builderContext = new CommandActionBuilderContext(descriptor.CommandType, command, serviceProvider);

        actionBuilder(builderContext);

        var operation = builderContext.Operation;
        if (operation is null)
        {
            throw new InvalidOperationException("Operation is not set.");
        }

        // Set action
        var filterPipeline = new FilterPipeline(serviceProvider, globalFilters);

        command.SetAction(async (parseResult, token) =>
        {
            var handler = (ICommandHandler)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.CommandType);
            var commandContext = new CommandContext(descriptor.CommandType, handler, token);

            await filterPipeline.ExecuteAsync(commandContext, ctx => operation(handler, parseResult, ctx)).ConfigureAwait(false);

            return commandContext.ExitCode;
        });
    }
}
#pragma warning restore CA1001

//--------------------------------------------------------------------------------
// Component builders
//--------------------------------------------------------------------------------

internal sealed class HostEnvironment : IHostEnvironment
{
    public string ApplicationName { get; set; } = default!;

    public string EnvironmentName { get; set; } = default!;

    public string ContentRootPath { get; set; } = default!;

    public IFileProvider ContentRootFileProvider { get; set; } = default!;
}

internal sealed class LoggingBuilder : ILoggingBuilder
{
    public IServiceCollection Services { get; }

    public LoggingBuilder(IServiceCollection services)
    {
        Services = services;
    }
}

//--------------------------------------------------------------------------------
// Command builders
//--------------------------------------------------------------------------------

internal sealed class CommandBuilder : ICommandBuilder
{
    private readonly IServiceCollection services;

    private RootCommand? rootCommand;

    private Action<RootCommand>? rootCommandConfiguration;

    private CommandDescriptor? rootDescriptor;

    private readonly List<CommandDescriptor> commandDescriptors = new();

    private readonly FilterCollection globalFilters = new();

    public CommandBuilder(IServiceCollection services)
    {
        this.services = services;
    }

    public ICommandBuilder ConfigureRootCommand(Action<IRootCommandBuilder> configure)
    {
        var rootConfigurator = new RootCommandBuilder(services);
        configure(rootConfigurator);

        rootCommand = rootConfigurator.GetRootCommand();
        rootCommandConfiguration = rootConfigurator.GetConfiguration();
        rootDescriptor = rootConfigurator.GetRootHandlerDescriptor();

        return this;
    }

    public ICommandBuilder AddCommand<TCommand>(Action<ISubCommandBuilder>? configure = null)
        where TCommand : class
    {
        if (typeof(ICommandHandler).IsAssignableFrom(typeof(TCommand)))
        {
            services.AddTransient<TCommand>();
        }

        var descriptor = new CommandDescriptor(typeof(TCommand));

        if (configure is not null)
        {
            var subConfigurator = new SubCommandBuilder(services);
            configure(subConfigurator);
            descriptor.SubCommands.AddRange(subConfigurator.GetDescriptors());
        }

        commandDescriptors.Add(descriptor);
        return this;
    }

    public ICommandBuilder AddGlobalFilter<TFilter>(int order = 0)
        where TFilter : class, ICommandFilter
    {
        globalFilters.Add<TFilter>(order);
        services.TryAddTransient<TFilter>();
        return this;
    }

    public ICommandBuilder AddGlobalFilter(Type filterType, int order = 0)
    {
        globalFilters.Add(filterType, order);
        services.TryAddTransient(filterType);
        return this;
    }

    internal RootCommand? GetCustomRootCommand() => rootCommand;

    internal Action<RootCommand>? GetRootCommandConfiguration() => rootCommandConfiguration;

    internal CommandDescriptor? GetRootHandlerDescriptor() => rootDescriptor;

    internal List<CommandDescriptor> GetCommandDescriptors() => commandDescriptors;

    internal FilterCollection GetGlobalFilters() => globalFilters;
}

internal sealed class RootCommandBuilder : IRootCommandBuilder
{
    private readonly IServiceCollection services;

    private string? name;

    private string? description;

    private RootCommand? rootCommand;

    private Action<RootCommand>? configure;

    private CommandDescriptor? rootDescriptor;

    public RootCommandBuilder(IServiceCollection services)
    {
        this.services = services;
    }

    // ReSharper disable once ParameterHidesMember
    public IRootCommandBuilder WithName(string name)
    {
        this.name = name;
        return this;
    }

    // ReSharper disable once ParameterHidesMember
    public IRootCommandBuilder WithDescription(string description)
    {
        this.description = description;
        return this;
    }

    // ReSharper disable once ParameterHidesMember
    public IRootCommandBuilder UseRootCommand(RootCommand rootCommand)
    {
        this.rootCommand = rootCommand;
        return this;
    }

    // ReSharper disable once ParameterHidesMember
    public IRootCommandBuilder Configure(Action<RootCommand> configure)
    {
        this.configure = configure;
        return this;
    }

    public IRootCommandBuilder UseHandler<THandler>()
        where THandler : class, ICommandHandler
    {
        services.AddTransient<THandler>();
        rootDescriptor = new CommandDescriptor(typeof(THandler));
        return this;
    }

    internal RootCommand? GetRootCommand()
    {
        if (rootCommand is not null)
        {
            return rootCommand;
        }

        if (name is not null)
        {
            return new RootCommand(name);
        }

        return null;
    }

    internal Action<RootCommand>? GetConfiguration()
    {
        if ((description is null) && (configure is null))
        {
            return null;
        }

        return command =>
        {
            if (description is not null)
            {
                command.Description = description;
            }

            configure?.Invoke(command);
        };
    }

    internal CommandDescriptor? GetRootHandlerDescriptor() => rootDescriptor;
}

internal sealed class SubCommandBuilder : ISubCommandBuilder
{
    private readonly IServiceCollection services;

    private readonly List<CommandDescriptor> descriptors = [];

    public SubCommandBuilder(IServiceCollection services)
    {
        this.services = services;
    }

    public ISubCommandBuilder AddSubCommand<TCommand>(Action<ISubCommandBuilder>? configure = null)
        where TCommand : class
    {
        if (typeof(ICommandHandler).IsAssignableFrom(typeof(TCommand)))
        {
            services.AddTransient<TCommand>();
        }

        var descriptor = new CommandDescriptor(typeof(TCommand));

        if (configure is not null)
        {
            var subConfigurator = new SubCommandBuilder(services);
            configure(subConfigurator);
            descriptor.SubCommands.AddRange(subConfigurator.descriptors);
        }

        descriptors.Add(descriptor);
        return this;
    }

    internal List<CommandDescriptor> GetDescriptors() => descriptors;
}
