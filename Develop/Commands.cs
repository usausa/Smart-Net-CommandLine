// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Develop;

using Microsoft.Extensions.Logging;

using Smart.CommandLine.Hosting;

//--------------------------------------------------------------------------------
// Services
//--------------------------------------------------------------------------------

public sealed class GreetService
{
    private readonly ILogger<GreetService> log;

    public GreetService(ILogger<GreetService> log)
    {
        this.log = log;
    }

    public void Execute(string name, string message)
    {
        log.LogInformation("Greeting: {Message}, {Name}!", message, name);
    }
}

//--------------------------------------------------------------------------------
// Filters
//--------------------------------------------------------------------------------

public sealed class ExecutionTimeFilter : ICommandFilter
{
    private readonly ILogger<ExecutionTimeFilter> log;

    public ExecutionTimeFilter(ILogger<ExecutionTimeFilter> log)
    {
        this.log = log;
    }

    public async ValueTask ExecuteAsync(CommandContext context, CommandDelegate next)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        context.Items["ExecutionTime"] = stopwatch.Elapsed;
        log.LogInformation("Command executed in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);
    }
}

public sealed class ExceptionHandlingFilter : ICommandFilter
{
    private readonly ILogger<ExceptionHandlingFilter> log;

    public ExceptionHandlingFilter(ILogger<ExceptionHandlingFilter> log)
    {
        this.log = log;
    }

    public async ValueTask ExecuteAsync(CommandContext context, CommandDelegate next)
    {
#pragma warning disable CA1031
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            log.LogError(exception, "Unhandled exception in command {CommandType}: {Message}", context.CommandType.Name, exception.Message);

            // Set exit code based on exception type
            context.ExitCode = exception switch
            {
                ArgumentException => 400,
                UnauthorizedAccessException => 403,
                FileNotFoundException => 404,
                _ => 500
            };
        }
#pragma warning restore CA1031
    }
}

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

//--------------------------------------------------------------------------------
// Basic commands
//--------------------------------------------------------------------------------

[Command("message", "Basic usage")]
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

[Command("greet", "DI service")]
public sealed class GreetCommand : ICommandHandler
{
    private readonly GreetService greetService;

    public GreetCommand(GreetService greetService)
    {
        this.greetService = greetService;
    }

    [Option<string>("--name", "-n", Description = "Name to greet", Required = true)]
    public string Name { get; set; } = default!;

    [Option<string>("--greeting", "-g", Description = "Greeting message", Required = false, DefaultValue = "Hello")]
    public string Greeting { get; set; } = default!;

    [Option<int>("--count", "-c", Description = "Number of times to greet", Required = false, DefaultValue = 1)]
    public int Count { get; set; }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        for (var i = 0; i < Count; i++)
        {
            greetService.Execute(Name, Greeting);
        }
        return ValueTask.CompletedTask;
    }
}

[Filter<LoggingFilter>]
[Command("filter", "Filter test")]
public sealed class FilterCommand : ICommandHandler
{
    private readonly ILogger<FilterCommand> log;

    public FilterCommand(ILogger<FilterCommand> log)
    {
        this.log = log;
    }

    [Option<string>("--message", "-m", Description = "Message to display", Required = true)]
    public string Message { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        await Task.Delay(50);

        log.LogInformation("Message: {Message}", Message);

        await Task.Delay(50);
    }
}

[Command("exception", "Exception test")]
public sealed class ExceptionCommand : ICommandHandler
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        throw new InvalidOperationException("Something went wrong");
    }
}

//--------------------------------------------------------------------------------
// Sub command
//--------------------------------------------------------------------------------

[Command("user", "User level 1")]
public sealed class UserCommand
{
}

[Command("list", "User level 1-1")]
public sealed class UserListCommand : ICommandHandler
{
    private readonly ILogger<UserListCommand> log;

    public UserListCommand(ILogger<UserListCommand> log)
    {
        this.log = log;
    }

    [Option<int>("--count", "-c", Description = "Number of users", DefaultValue = 1)]
    public int Count { get; set; }

    public ValueTask ExecuteAsync(CommandContext context)
    {
        log.LogInformation("Listing {Count} users", Count);
        for (var i = 1; i <= Count; i++)
        {
            log.LogInformation(" {Index}: User-{No}", i, i);
        }
        return ValueTask.CompletedTask;
    }
}

[Command("add", "User level 1-2")]
public sealed class UserAddCommand : ICommandHandler
{
    private readonly ILogger<UserAddCommand> log;

    public UserAddCommand(ILogger<UserAddCommand> log)
    {
        this.log = log;
    }

    [Option<string>("--user", "-u", Description = "Username to add", Required = true)]
    public string Username { get; set; } = default!;

    [Option<string>("--email", "-e", Description = "User email address", Required = true)]
    public string Email { get; set; } = default!;

    public ValueTask ExecuteAsync(CommandContext context)
    {
        log.LogInformation("Adding user: {Username} ({Email})", Username, Email);
        return ValueTask.CompletedTask;
    }
}

[Command("role", "User level 1-3")]
public sealed class UserRoleCommand
{
}

public abstract class UserRoleCommandBase : ICommandHandler
{
    [Option<string>("--user", "-u", Description = "Username to add", Required = true)]
    public string Username { get; set; } = default!;

    [Option<string>("--role", "-r", Description = "Username to add", Required = true)]
    public string Role { get; set; } = default!;

    public abstract ValueTask ExecuteAsync(CommandContext context);
}

[Command("assign", "User level 1-3-1")]
public sealed class UserRoleAssignCommand : UserRoleCommandBase
{
    private readonly ILogger<UserRoleAssignCommand> log;

    public UserRoleAssignCommand(ILogger<UserRoleAssignCommand> log)
    {
        this.log = log;
    }

    public override ValueTask ExecuteAsync(CommandContext context)
    {
        log.LogInformation("Assign role '{Role}' to user '{Username}'", Role, Username);
        return ValueTask.CompletedTask;
    }
}

[Command("remove", "User level 1-3-2")]
public sealed class UserRoleRemoveCommand : UserRoleCommandBase
{
    private readonly ILogger<UserRoleRemoveCommand> log;

    public UserRoleRemoveCommand(ILogger<UserRoleRemoveCommand> log)
    {
        this.log = log;
    }

    public override ValueTask ExecuteAsync(CommandContext context)
    {
        log.LogInformation("Remove role '{Role}' to user '{Username}'", Role, Username);
        return ValueTask.CompletedTask;
    }
}
