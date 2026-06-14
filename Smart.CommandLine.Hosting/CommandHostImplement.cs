namespace Smart.CommandLine.Hosting;

using System.CommandLine;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

#pragma warning disable IDE0032
internal sealed class CommandHostImplement : ICommandHost
{
    private readonly string[] args;

    private readonly RootCommand rootCommand;

    private readonly IServiceProvider serviceProvider;

    private readonly IConfigurationRoot configuration;

    private readonly IHostEnvironment environment;

    public IServiceProvider Services => serviceProvider;

    public CommandHostImplement(string[] args, RootCommand rootCommand, IServiceProvider serviceProvider, IConfigurationRoot configuration, IHostEnvironment environment)
    {
        this.args = args;
        this.rootCommand = rootCommand;
        this.serviceProvider = serviceProvider;
        this.configuration = configuration;
        this.environment = environment;
    }

    public async ValueTask<int> RunAsync()
    {
        return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (configuration is IConfigurationBuilder configurationBuilder)
        {
            foreach (var fileProvider in configurationBuilder.Sources.OfType<FileConfigurationSource>().Select(static x => x.FileProvider).Distinct())
            {
                (fileProvider as IDisposable)?.Dispose();
            }
        }

        (configuration as IDisposable)?.Dispose();
        (environment.ContentRootFileProvider as IDisposable)?.Dispose();
    }
}
#pragma warning restore IDE0032
