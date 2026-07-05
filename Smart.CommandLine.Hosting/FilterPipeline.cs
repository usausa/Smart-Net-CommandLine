namespace Smart.CommandLine.Hosting;

using System.Diagnostics.CodeAnalysis;

internal sealed class FilterPipeline
{
    private readonly IServiceProvider serviceProvider;

    private readonly FilterDescriptor[] descriptors;

    public FilterPipeline(IServiceProvider serviceProvider, FilterDescriptor[] descriptors)
    {
        this.serviceProvider = serviceProvider;
        this.descriptors = descriptors;
    }

    public ValueTask ExecuteAsync(CommandContext context, Func<CommandContext, ValueTask> action)
    {
        if (descriptors.Length == 0)
        {
            // Without pipeline
            return action(context);
        }

        // Create pipeline
        CommandDelegate pipeline = ctx => action(ctx);
        for (var i = descriptors.Length - 1; i >= 0; i--)
        {
            if (serviceProvider.GetService(descriptors[i].FilterType) is ICommandFilter commandFilter)
            {
                var next = pipeline;
                pipeline = ctx => commandFilter.ExecuteAsync(ctx, next);
            }
        }

        // With pipeline
        return pipeline(context);
    }

    [RequiresUnreferencedCode("Uses reflection fallback for filter descriptors when Source Generator is not applied.")]
    public static FilterDescriptor[] BuildDescriptors(FilterCollection globalFilters, Type commandType)
    {
        var commandDescriptors = CommandMetadataProvider.GetFilterDescriptors(commandType);
        var globalDescriptors = globalFilters.Descriptors;

        var globalCount = globalDescriptors?.Count ?? 0;
        if ((globalCount == 0) && (commandDescriptors.Count == 0))
        {
            return [];
        }

        var merged = new List<FilterDescriptor>(globalCount + commandDescriptors.Count);
        if (globalDescriptors is not null)
        {
            merged.AddRange(globalDescriptors);
        }
        merged.AddRange(commandDescriptors);

        // Stable sort by order (global filters keep priority over command filters on equal order).
        return merged.OrderBy(static x => x.Order).ToArray();
    }
}
