namespace Smart.CommandLine.Hosting.Generator;

using Microsoft.CodeAnalysis;

using SourceGenerateHelper;

internal static class Diagnostics
{
    public static DiagnosticDescriptor OptionNotWritable { get; } = new(
        id: "SCL0001",
        title: "Option property is not writable",
        messageFormat: "Option property must have a public setter that is not init-only, and is not bound. property=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor CommandNotReferable { get; } = new(
        id: "SCL0002",
        title: "Command type cannot be referred to",
        messageFormat: "Command type must not be file-local or nested in a private or protected type, and is not registered. type=[{0}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor DefaultValueNotConvertible { get; } = new(
        id: "SCL0003",
        title: "Default value cannot be converted",
        messageFormat: "DefaultValue cannot be converted to the property type, and is not used. property=[{0}], type=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: DiagnosticTags.NotSuppressible);

    public static DiagnosticDescriptor InvalidPropertyValue { get; } = new(
        id: "SCL0004",
        title: "Invalid MSBuild property value",
        messageFormat: "MSBuild property value is not valid, and the default is used. property=[{0}], value=[{1}]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
