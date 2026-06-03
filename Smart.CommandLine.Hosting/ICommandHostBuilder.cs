namespace Smart.CommandLine.Hosting;

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;

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

    ICommandBuilder AddCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(Action<ISubCommandBuilder>? configure = null)
        where TCommand : class;

    ICommandBuilder AddGlobalFilter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFilter>(int order = 0)
        where TFilter : class, ICommandFilter;

    ICommandBuilder AddGlobalFilter(Type filterType, int order = 0);
}

public interface IRootCommandBuilder
{
    IRootCommandBuilder WithName(string name);

    IRootCommandBuilder WithDescription(string description);

    IRootCommandBuilder UseRootCommand(RootCommand rootCommand);

    IRootCommandBuilder Configure(Action<RootCommand> configure);

    IRootCommandBuilder UseHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
        where THandler : class, ICommandHandler;
}

public interface ISubCommandBuilder
{
    ISubCommandBuilder AddSubCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(
        Action<ISubCommandBuilder>? configure = null)
        where TCommand : class;
}
