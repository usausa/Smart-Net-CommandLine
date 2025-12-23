// ReSharper disable MemberCanBePrivate.Global
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

[Command("message", Description = "Basic usage")]
public sealed class MessageCommand : ICommand
{
    private readonly ILogger<MessageCommand> log;

    public MessageCommand(ILogger<MessageCommand> logger)
    {
        log = logger;
    }

    [Option<string>("--text", "-t", Description = "Text to show", IsRequired = true)]
    public string Text { get; set; } = default!;

    public ValueTask ExecuteAsync(CommandContext context)
    {
        log.LogInformation("Show {Text}", Text);
        return ValueTask.CompletedTask;
    }
}

[Command("greet", Description = "DI service")]
public sealed class GreetCommand : ICommand
{
    private readonly GreetService greetService;

    public GreetCommand(GreetService greetService)
    {
        this.greetService = greetService;
    }

    [Option<string>("--name", "-n", Description = "Name to greet", IsRequired = true)]
    public string Name { get; set; } = default!;

    [Option<string>("--greeting", "-g", Description = "Greeting message", IsRequired = false, DefaultValue = "Hello")]
    public string Greeting { get; set; } = default!;

    [Option<int>("--count", "-c", Description = "Number of times to greet", IsRequired = false, DefaultValue = 1)]
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
[Command("filter", Description = "Filter test")]
public sealed class FilterCommand : ICommand
{
    private readonly ILogger<FilterCommand> log;

    public FilterCommand(ILogger<FilterCommand> logger)
    {
        log = logger;
    }

    [Option<string>("--message", "-m", Description = "Message to display", IsRequired = true)]
    public string Message { get; set; } = default!;

    public async ValueTask ExecuteAsync(CommandContext context)
    {
        await Task.Delay(50);

        log.LogInformation("Message: {Message}", Message);

        await Task.Delay(50);
    }
}

[Command("exception", Description = "Exception testt")]
public sealed class ExceptionCommand : ICommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        throw new InvalidOperationException("Something went wrong");
    }
}

//--------------------------------------------------------------------------------
// Sub command
//--------------------------------------------------------------------------------

// TODO
