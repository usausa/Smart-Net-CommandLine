namespace Smart.CommandLine.Hosting;

internal sealed class CommandDescriptor
{
    public Type CommandType { get; }

    public List<CommandDescriptor> SubCommands
    {
        get
        {
            field ??= [];
            return field;
        }
    }

    public CommandDescriptor(Type commandType)
    {
        CommandType = commandType;
    }
}
