namespace Smart.CommandLine.Hosting;

using System.Diagnostics.CodeAnalysis;

internal sealed class FilterPipeline
{
    private readonly IServiceProvider serviceProvider;

    private readonly FilterCollection globalFilters;

    public FilterPipeline(IServiceProvider serviceProvider, FilterCollection globalFilters)
    {
        this.serviceProvider = serviceProvider;
        this.globalFilters = globalFilters;
    }

    [RequiresUnreferencedCode("Uses reflection fallback for filter descriptors when Source Generator is not applied.")]
    [RequiresDynamicCode("Uses reflection fallback. Use Source Generator to avoid this.")]
    public ValueTask ExecuteAsync(CommandContext context, Func<CommandContext, ValueTask> action)
    {
        // Collect filters
        var filters = globalFilters.Descriptors is not null
            ? new List<FilterDescriptor>(globalFilters.Descriptors)
            : [];
        filters.AddRange(CommandMetadataProvider.GetFilterDescriptors(context.CommandType));

        if (filters.Count == 0)
        {
            // Without pipeline
            return action(context);
        }

        filters.Sort(static (x, y) => x.Order.CompareTo(y.Order));

        // Create pipeline
        CommandDelegate pipeline = ctx => action(ctx);
        for (var i = filters.Count - 1; i >= 0; i--)
        {
            if (serviceProvider.GetService(filters[i].FilterType) is ICommandFilter commandFilter)
            {
                var next = pipeline;
                pipeline = ctx => commandFilter.ExecuteAsync(ctx, next);
            }
        }

        // With pipeline
        return pipeline(context);
    }
}
