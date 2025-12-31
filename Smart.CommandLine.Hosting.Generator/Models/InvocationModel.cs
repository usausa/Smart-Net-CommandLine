namespace Smart.CommandLine.Hosting.Generator.Models;

using SourceGenerateHelper;

internal sealed record InvocationModel(
    string TypeFullName,
    bool ImplementsHandler,
    CommandModel? CommandInfo,
    EquatableArray<FilterModel> Filters,
    EquatableArray<OptionModel> Options);
