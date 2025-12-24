namespace Smart.CommandLine.Hosting;

using System.CommandLine;
using System.ComponentModel;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public interface ICommandHostBuilder
{
    ConfigurationManager Configuration { get; }

    IHostEnvironment Environment { get; }

    IServiceCollection Services { get; }

    ILoggingBuilder Logging { get; }

    void ConfigureContainer<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory,
        Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull;

    ICommandHostBuilder ConfigureCommands(Action<ICommandBuilder> configure);

    ICommandHost Build();
}

public interface ICommandBuilder
{
    ICommandBuilder ConfigureRootCommand(Action<IRootCommandBuilder> configure);

    ICommandBuilder AddCommand<TCommand>(Action<ISubCommandBuilder>? configure = null)
        where TCommand : class;

    // ReSharper disable once UnusedMemberInSuper.Global
    [EditorBrowsable(EditorBrowsableState.Never)]
    ICommandBuilder AddCommand<TCommand>(
        Action<CommandActionBuilderContext>? builder,
        Action<ISubCommandBuilder>? configure = null)
        where TCommand : class;

    ICommandBuilder AddGlobalFilter<TFilter>(int order = 0)
        where TFilter : class, ICommandFilter;

    ICommandBuilder AddGlobalFilter(Type filterType, int order = 0);
}

public interface IRootCommandBuilder
{
    IRootCommandBuilder WithName(string name);

    IRootCommandBuilder WithDescription(string description);

    IRootCommandBuilder UseRootCommand(RootCommand rootCommand);

    IRootCommandBuilder Configure(Action<RootCommand> configure);

    IRootCommandBuilder UseHandler<THandler>()
        where THandler : class, ICommandHandler;

    // ReSharper disable once UnusedMemberInSuper.Global
    [EditorBrowsable(EditorBrowsableState.Never)]
    IRootCommandBuilder UseHandler<THandler>(Action<CommandActionBuilderContext>? builder)
        where THandler : class, ICommandHandler;
}

public interface ISubCommandBuilder
{
    ISubCommandBuilder AddSubCommand<TCommand>(
        Action<ISubCommandBuilder>? configure = null)
        where TCommand : class;

    // ReSharper disable once UnusedMemberInSuper.Global
    [EditorBrowsable(EditorBrowsableState.Never)]
    ISubCommandBuilder AddSubCommand<TCommand>(
        Action<CommandActionBuilderContext>? builder,
        Action<ISubCommandBuilder>? configure = null)
        where TCommand : class;
}
