namespace Smart.CommandLine.Hosting;

public interface ICommandFilter
{
    ValueTask ExecuteAsync(CommandContext context, CommandDelegate next);
}
