namespace Smart.CommandLine.Hosting;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    public string Name { get; }

    public string? Description { get; }

    public CommandAttribute(string name)
    {
        Name = name;
    }

    public CommandAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
