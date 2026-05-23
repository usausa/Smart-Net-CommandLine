namespace Smart.CommandLine.Hosting;

using System.Diagnostics.CodeAnalysis;

#pragma warning disable CA1711
internal sealed class FilterCollection
{
    internal List<FilterDescriptor>? Descriptors { get; set; }

    public void Add<TFilter>(int order = 0)
        where TFilter : ICommandFilter
    {
        Descriptors ??= [];
        Descriptors.Add(new FilterDescriptor(typeof(TFilter), order));
    }

    [RequiresUnreferencedCode("Type-based filter registration may not be preserved by the trimmer. Use Add<TFilter>() instead.")]
    public void Add(Type filterType, int order = 0)
    {
        if (!typeof(ICommandFilter).IsAssignableFrom(filterType))
        {
            throw new ArgumentException($"Type must implement '{typeof(ICommandFilter).FullName}' interface.", nameof(filterType));
        }

        Descriptors ??= [];
        Descriptors.Add(new FilterDescriptor(filterType, order));
    }
}
#pragma warning restore CA1711
