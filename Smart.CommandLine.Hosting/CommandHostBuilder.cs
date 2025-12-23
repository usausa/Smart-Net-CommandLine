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

    public CommandHostBuilder(string[] args, bool useDefaults = true)
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

        // Default setting
        if (useDefaults)
        {
            this.UseDefaultConfiguration();
            this.UseDefaultLogging();
        }
        else
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole();
            });
        }

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

        foreach (var descriptor in commandDescriptors)
        {
            foreach (var attribute in descriptor.CommandType.GetCustomAttributes<FilterAttribute>())
            {
                filterTypes.Add(attribute.FilterType);
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

        // Add commands
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
        var attribute = descriptor.CommandType.GetCustomAttribute<CommandAttribute>()!;
        var command = new Command(attribute.Name, attribute.Description);

        // Build executable command
        if (typeof(ICommand).IsAssignableFrom(descriptor.CommandType))
        {
            // Build command
            var actionBuilder = descriptor.ActionBuilder ?? CommandActionBuilderHelper.CreateReflectionBasedDelegate(descriptor.CommandType);
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
                var commandInstance = (ICommand)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.CommandType);
                var commandContext = new CommandContext(descriptor.CommandType, commandInstance, token);

                await filterPipeline.ExecuteAsync(commandContext, ctx => operation(commandInstance, parseResult, ctx)).ConfigureAwait(false);

                return commandContext.ExitCode;
            });
        }

        // Add sub commands
        foreach (var subDescriptor in descriptor.SubCommands)
        {
            var subCommand = CreateCommand(serviceProvider, globalFilters, subDescriptor);
            command.Subcommands.Add(subCommand);
        }

        return command;
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

    private readonly List<CommandDescriptor> commandDescriptors = new();

    private readonly FilterCollection globalFilters = new();

    public CommandBuilder(IServiceCollection services)
    {
        this.services = services;
    }

    public ICommandBuilder ConfigureRootCommand(Action<IRootCommandBuilder> configure)
    {
        var rootConfigurator = new RootCommandBuilder();
        configure(rootConfigurator);

        rootCommand = rootConfigurator.GetRootCommand();
        rootCommandConfiguration = rootConfigurator.GetConfiguration();

        return this;
    }

    public ICommandBuilder AddCommand<TCommand>(Action<ISubCommandBuilder>? configure = null)
        where TCommand : class
    {
        return AddCommand<TCommand>(null, configure);
    }

    public ICommandBuilder AddCommand<TCommand>(Action<CommandActionBuilderContext>? builder, Action<ISubCommandBuilder>? configure = null)
        where TCommand : class
    {
        if (typeof(ICommand).IsAssignableFrom(typeof(TCommand)))
        {
            services.AddTransient<TCommand>();
        }

        var descriptor = new CommandDescriptor(typeof(TCommand), builder);

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

    internal List<CommandDescriptor> GetCommandDescriptors() => commandDescriptors;

    internal FilterCollection GetGlobalFilters() => globalFilters;
}

internal sealed class RootCommandBuilder : IRootCommandBuilder
{
    private string? name;

    private string? description;

    private RootCommand? rootCommand;

    private Action<RootCommand>? configure;

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
        return AddSubCommand<TCommand>(null, configure);
    }

    public ISubCommandBuilder AddSubCommand<TCommand>(Action<CommandActionBuilderContext>? builder, Action<ISubCommandBuilder>? configure = null)
        where TCommand : class
    {
        if (typeof(ICommand).IsAssignableFrom(typeof(TCommand)))
        {
            services.AddTransient<TCommand>();
        }

        var descriptor = new CommandDescriptor(typeof(TCommand), builder);

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
