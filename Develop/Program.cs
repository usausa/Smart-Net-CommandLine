using Develop;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Smart.CommandLine.Hosting;

// Command host
var builder = CommandHost.CreateBuilder(args);

Console.WriteLine($"Application: {builder.Environment.ApplicationName}");
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

// Logging
builder.Services.AddLogging(logging =>
{
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

        commands.AddGlobalFilter<ExecutionTimeFilter>(order: -100);
        commands.AddGlobalFilter<ExceptionHandlingFilter>(order: Int32.MaxValue);

        commands.AddCommand<MessageCommand>();
        commands.AddCommand<GreetCommand>();
        commands.AddCommand<FilterCommand>();
        commands.AddCommand<ExceptionCommand>();
        // TODO
        //commands.AddCommand<UserCommand>(user =>
        //{
        //    user.AddSubCommand<UserListCommand>();
        //    user.AddSubCommand<UserAddCommand>();
        //    user.AddSubCommand<UserRoleCommand>(role =>
        //    {
        //        role.AddSubCommand<UserRoleAssignCommand>();
        //        role.AddSubCommand<UserRoleRemoveCommand>();
        //    });
        //});
    });
});

var host = builder.Build();

var exitCode = await host.RunAsync();
Console.WriteLine($"ExitCode: {exitCode}");
return exitCode;
