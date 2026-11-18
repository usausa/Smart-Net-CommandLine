namespace Smart.CommandLine.Hosting;

using System.CommandLine;
using System.CommandLine.Help;

using Microsoft.Extensions.DependencyInjection;

public sealed class OptionAliasTests
{
    //--------------------------------------------------------------------------------
    // Duplicate
    //--------------------------------------------------------------------------------

    [Fact]
    public void BuildWithDuplicateAliasAddedByConfigurationThrows()
    {
        // Arrange
        var builder = CommandHost.CreateBuilder([]);
        builder.ConfigureCommands(static commands => commands.ConfigureRootCommand(static root => root.Configure(static command =>
        {
            command.Options.Add(new Option<string>("--light", "-l"));
            command.Options.Add(new Option<string>("--level", "-l"));
        })));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => { _ = builder.Build(); });

        // Assert
        Assert.EndsWith("], alias=[-l], options=[--light, --level]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildWithSameAliasInSiblingCommandsSucceeds()
    {
        // Arrange
        var recorder = new AliasRecorder();
        var builder = CommandHost.CreateBuilder(["second", "-n", "value"]);
        builder.Services.AddSingleton(recorder);
        builder.ConfigureCommands(static commands =>
        {
            commands.AddCommand<AliasFirstCommand>();
            commands.AddCommand<AliasSecondCommand>();
        });

        await using var host = builder.Build();

        // Act
        var exitCode = await host.RunAsync();

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("value", recorder.Name);
    }

    //--------------------------------------------------------------------------------
    // Root options
    //--------------------------------------------------------------------------------

    [Fact]
    public void BuildWithAliasConflictingHelpOptionThrows()
    {
        // Arrange
        var builder = CommandHost.CreateBuilder([]);
        builder.ConfigureCommands(static commands => commands.AddCommand<AliasHostCommand>());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => { _ = builder.Build(); });

        // Assert
        Assert.EndsWith(" connect], alias=[-h], options=[--host, --help]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildWithAliasRemovedFromHelpOptionBindsCommandOption()
    {
        // Arrange
        var recorder = new AliasRecorder();
        var builder = CommandHost.CreateBuilder(["connect", "-h", "server"]);
        builder.Services.AddSingleton(recorder);
        builder.ConfigureCommands(static commands =>
        {
            commands.ConfigureRootCommand(static root => root.Configure(static command =>
            {
                var help = command.Options.OfType<HelpOption>().First();
                help.Aliases.Remove("-h");
                help.Aliases.Remove("/h");
            }));
            commands.AddCommand<AliasHostCommand>();
        });

        await using var host = builder.Build();

        // Act
        var exitCode = await host.RunAsync();

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("server", recorder.Host);
    }

    [Fact]
    public void BuildWithRootOptionConflictingVersionOptionThrows()
    {
        // Arrange
        var builder = CommandHost.CreateBuilder([]);
        builder.ConfigureCommands(static commands => commands.ConfigureRootCommand(static root => root.UseHandler<AliasVersionHandler>()));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => { _ = builder.Build(); });

        // Assert
        Assert.EndsWith("], alias=[--version], options=[--version, --version]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildWithAliasConflictingRecursiveOptionThrows()
    {
        // Arrange
        var builder = CommandHost.CreateBuilder([]);
        builder.ConfigureCommands(static commands =>
        {
            commands.ConfigureRootCommand(static root => root.Configure(static command =>
            {
                command.Options.Add(new Option<bool>("--verbose", "-v") { Recursive = true });
            }));
            commands.AddCommand<AliasGroupCommand>(static group => group.AddSubCommand<AliasValueCommand>());
        });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => { _ = builder.Build(); });

        // Assert
        Assert.EndsWith(" group value], alias=[-v], options=[--value, --verbose]", exception.Message, StringComparison.Ordinal);
    }
}

//--------------------------------------------------------------------------------
// Probe
//--------------------------------------------------------------------------------

public sealed class AliasRecorder
{
    public string? Host { get; set; }

    public string? Name { get; set; }
}

[Command("first", "First sibling command")]
public sealed class AliasFirstCommand : ICommandHandler
{
    [Option<string>("--name", "-n")]
    public string Name { get; set; } = default!;

    public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
}

[Command("second", "Second sibling command")]
public sealed class AliasSecondCommand : ICommandHandler
{
    private readonly AliasRecorder recorder;

    public AliasSecondCommand(AliasRecorder recorder)
    {
        this.recorder = recorder;
    }

    [Option<string>("--name", "-n")]
    public string Name { get; set; } = default!;

    public ValueTask ExecuteAsync(CommandContext context)
    {
        recorder.Name = Name;
        return ValueTask.CompletedTask;
    }
}

[Command("connect", "Host alias command")]
public sealed class AliasHostCommand : ICommandHandler
{
    private readonly AliasRecorder recorder;

    public AliasHostCommand(AliasRecorder recorder)
    {
        this.recorder = recorder;
    }

    [Option<string>("--host", "-h")]
    public string Host { get; set; } = default!;

    public ValueTask ExecuteAsync(CommandContext context)
    {
        recorder.Host = Host;
        return ValueTask.CompletedTask;
    }
}

public sealed class AliasVersionHandler : ICommandHandler
{
    [Option<string>("--version")]
    public string Version { get; set; } = default!;

    public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
}

[Command("group", "Group command")]
public sealed class AliasGroupCommand;

[Command("value", "Value alias command")]
public sealed class AliasValueCommand : ICommandHandler
{
    [Option<string>("--value", "-v")]
    public string Value { get; set; } = default!;

    public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
}
