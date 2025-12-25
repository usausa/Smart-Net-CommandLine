# Smart.CommandLine - System.CommandLine hosting

[![NuGet](https://img.shields.io/nuget/v/Usa.Smart.CommandLine.Hosting.svg)](https://www.nuget.org/packages/Usa.Smart.CommandLine.Hosting/)

## What is this?

- Command-line application (CLI) development framework inspired by ASP.NET Core hosting model
- Microsoft.Extensions ecosystem integration
- POCO-based command definition
- Attribute-based option mapping
- Hierarchical sub-command structure
- Global and command-specific filters for cross-cutting concerns (logging, exception handling, performance monitoring)

## Usage example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Smart.CommandLine.Hosting;

// Command host
var builder = CommandHost.CreateBuilder(args)
    .UseDefaults();

// Logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss.fff ";
        options.IncludeScopes = true;
    });
});

// Services
builder.Services.AddSingleton<GreetService>();

// Commands
builder.ConfigureCommands(commands =>
{
    commands.ConfigureRootCommand(root =>
    {
        root.WithDescription("Sample CLI tool");
    });

    commands.AddGlobalFilter<ExecutionTimeFilter>(order: -100);
    commands.AddGlobalFilter<ExceptionHandlingFilter>(order: Int32.MaxValue);

    commands.AddCommand<MessageCommand>();
    commands.AddCommand<GreetCommand>();
    commands.AddCommand<FilterCommand>();
    commands.AddCommand<ExceptionCommand>();
    commands.AddCommand<UserCommand>(user =>
    {
        user.AddSubCommand<UserListCommand>();
        user.AddSubCommand<UserAddCommand>();
        user.AddSubCommand<UserRoleCommand>(role =>
        {
            role.AddSubCommand<UserRoleAssignCommand>();
            role.AddSubCommand<UserRoleRemoveCommand>();
        });
    });
});

var host = builder.Build();
return await host.RunAsync();
```

## Feature

### Command

- Example

```csharp
[Command("message", Description = "Display text")]
public sealed class MessageCommand : ICommandHandler
{
    private readonly ILogger<MessageCommand> log;

    public MessageCommand(ILogger<MessageCommand> log)
    {
        this.log = log;
    }

    [Option<string>("--text", "-t", Description = "Text to show", Required = true)]
    public string Text { get; set; } = default!;

    public ValueTask ExecuteAsync(CommandContext context)
    {
        log.LogInformation("Show {Text}", Text);
        return ValueTask.CompletedTask;
    }
}
```

### Filter

- Example

```csharp
public sealed class LoggingFilter : ICommandFilter
{
    private readonly ILogger<LoggingFilter> log;

    public LoggingFilter(ILogger<LoggingFilter> log)
    {
        this.log = log;
    }

    public async ValueTask ExecuteAsync(CommandContext context, CommandDelegate next)
    {
        log.LogInformation("Start command: {CommandType}", context.CommandType.Name);

        try
        {
            await next(context);
        }
        finally
        {
            log.LogInformation("End command: {CommandType}", context.CommandType.Name);
        }
    }
}
```

# TODO

- [ ] Source Generator based builder
