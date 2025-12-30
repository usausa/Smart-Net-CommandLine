namespace Smart.CommandLine.Hosting.Generator.Models;

using SourceGenerateHelper;

internal sealed record OptionModel(
    string PropertyName,
    string PropertyType,
    int Order,
    int HierarchyLevel,
    int PropertyIndex,
    string Name,
    EquatableArray<string> Aliases,
    string? Description,
    bool Required,
    string? DefaultValue,
    EquatableArray<string> Completions);
