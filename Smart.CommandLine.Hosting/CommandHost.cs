namespace Smart.CommandLine.Hosting;

public static class CommandHost
{
    public static ICommandHostBuilder CreateBuilder(string[] args) =>
        new CommandHostBuilder(args);
}
