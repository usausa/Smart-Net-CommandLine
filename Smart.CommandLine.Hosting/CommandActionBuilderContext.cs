namespace Smart.CommandLine.Hosting;

using System.CommandLine;

public sealed class CommandActionBuilderContext
{
    private readonly Command command;

    public Type CommandType { get; }

    public IServiceProvider ServiceProvider { get; }

    public CommandActionDelegate? Operation { get; set; }

    public CommandActionBuilderContext(Type commandType, Command command, IServiceProvider serviceProvider)
    {
        this.command = command;
        CommandType = commandType;
        ServiceProvider = serviceProvider;
    }

    public void AddOption(Option option)
    {
        command.Options.Add(option);
    }
}
