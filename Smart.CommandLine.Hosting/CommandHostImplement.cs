namespace Smart.CommandLine.Hosting;

using System.CommandLine;

#pragma warning disable IDE0032
internal sealed class CommandHostImplement : ICommandHost
{
    private readonly string[] args;

    private readonly RootCommand rootCommand;

    private readonly IServiceProvider serviceProvider;

    public IServiceProvider Services => serviceProvider;

    public CommandHostImplement(string[] args, RootCommand rootCommand, IServiceProvider serviceProvider)
    {
        this.args = args;
        this.rootCommand = rootCommand;
        this.serviceProvider = serviceProvider;
    }

    public async ValueTask<int> RunAsync()
    {
        return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        if (serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else if (serviceProvider is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }
        return ValueTask.CompletedTask;
    }
}
#pragma warning restore IDE0032
