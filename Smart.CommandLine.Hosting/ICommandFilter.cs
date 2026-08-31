namespace Smart.CommandLine.Hosting;

public interface ICommandFilter
{
#pragma warning disable CA1716
    ValueTask ExecuteAsync(CommandContext context, CommandDelegate next);
#pragma warning restore CA1716
}
