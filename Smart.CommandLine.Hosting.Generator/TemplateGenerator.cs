namespace Smart.CommandLine.Hosting.Generator;

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

using Smart.CommandLine.Hosting.Generator.Models;

using SourceGenerateHelper;

[Generator]
public sealed class TemplateGenerator : IIncrementalGenerator
{
    private const string EnableInterceptorOptionName = "build_property.EnableSmartCommandLineHostingGenerator";

    private const string AddCommandMethodName = "AddCommand";
    private const string AddSubCommandMethodName = "AddSubCommand";
    private const string UseHandlerMethodName = "UseHandler";

    private const string CommandBuilderFullName = "Smart.CommandLine.Hosting.ICommandBuilder";
    private const string SubCommandBuilderFullName = "Smart.CommandLine.Hosting.ISubCommandBuilder";
    private const string RootCommandBuilderFullName = "Smart.CommandLine.Hosting.IRootCommandBuilder";

    private const string CommandAttributeFullName = "Smart.CommandLine.Hosting.CommandAttribute";
    private const string BaseOptionAttributeFullName = "Smart.CommandLine.Hosting.BaseOptionAttribute";
    private const string OptionAttributeFullName = "Smart.CommandLine.Hosting.OptionAttribute";

    // TODO

    // ------------------------------------------------------------
    // Initialize
    // ------------------------------------------------------------

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
        {
            context.AddSource("InterceptsLocationAttribute.g.cs", SourceText.From(InterceptsLocationAttributeSource, Encoding.UTF8));
        });

        // Read option property
        var settingProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => SelectSetting(provider));

        // Find invocations
        var invocationProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsTargetInvocation(node),
                transform: static (context, _) => GetInvocationModel(context))
            .Where(static x => x is not null)
            .Collect();

        var combined = settingProvider.Combine(invocationProvider);

        // Execute
        context.RegisterSourceOutput(combined, static (context, source) =>
        {
            var (setting, invocations) = source;

            if (!setting.Enable)
            {
                return;
            }

            if (invocations.IsEmpty)
            {
                return;
            }

            Execute(context, invocations!);
        });
    }

    // ------------------------------------------------------------
    // Parser
    // ------------------------------------------------------------

    private static GeneratorSetting SelectSetting(AnalyzerConfigOptionsProvider provider)
    {
        var enable = provider.GlobalOptions.TryGetValue(EnableInterceptorOptionName, out var value) &&
                     bool.TryParse(value, out var result) &&
                     result;
        return new GeneratorSetting(enable);
    }

    private static bool IsTargetInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        // Check method access (e.g., builder.AddCommand<T>(), builder.AddSubCommand<T>(), builder.UseHandler<T>())
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        // Check generic method
        if (memberAccess.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        // Check method name and no argument
        var methodName = genericName.Identifier.Text;
        return (invocation.ArgumentList.Arguments.Count == 0) &&
               (methodName is AddCommandMethodName or AddSubCommandMethodName or UseHandlerMethodName);
    }

    private static Result<InvocationModel>? GetInvocationModel(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetOperation(invocation) is not IInvocationOperation operation)
        {
            return null;
        }

        // Check method
        var method = operation.TargetMethod;
        if (!method.IsGenericMethod ||
            (method.TypeArguments.Length != 1) ||
            ((method.Name != AddCommandMethodName) && (method.Name != AddSubCommandMethodName) && (method.Name != UseHandlerMethodName)))
        {
            return null;
        }

        // Check containing type
        var containingType = method.OriginalDefinition.ContainingType;
        if ((containingType.ToDisplayString() != CommandBuilderFullName) &&
            (containingType.ToDisplayString() != SubCommandBuilderFullName) &&
            (containingType.ToDisplayString() != RootCommandBuilderFullName))
        {
            return null;
        }

        // Get interceptable location
        var interceptableLocation = context.SemanticModel.GetInterceptableLocation(invocation);
        if (interceptableLocation is null)
        {
            return null;
        }

        // Get information
        var typeArgument = method.TypeArguments[0];
        var receiverType = operation.Instance!.Type!;

        var command = ExtractCommandModel(typeArgument);

        // TODO property check ?

        return Results.Success(new InvocationModel(
            interceptableLocation,
            typeArgument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            typeArgument.Name,
            receiverType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            method.Name,
            command,
            // TODO
            new EquatableArray<OptionModel>([])));
    }

    private static CommandModel? ExtractCommandModel(ITypeSymbol typeSymbol)
    {
        var attribute = typeSymbol.GetAttributes()
            .FirstOrDefault(static x => x.AttributeClass?.ToDisplayString() == CommandAttributeFullName);
        if (attribute is null)
        {
            return null;
        }

        var name = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty
            : string.Empty;
        var description = attribute.ConstructorArguments.Length > 1
            ? attribute.ConstructorArguments[1].Value?.ToString()
            : null;

        return new CommandModel(name, description);
    }

    private static EquatableArray<string> ExtractStringArray(TypedConstant arrayConstant)
    {
        if (arrayConstant is { Kind: TypedConstantKind.Array, Values.IsEmpty: false })
        {
            var result = new List<string>();

            foreach (var element in arrayConstant.Values)
            {
                if (element.Value is string str)
                {
                    result.Add(str);
                }
            }

            return new EquatableArray<string>(result.ToArray());
        }

        return new EquatableArray<string>([]);
    }

    // ------------------------------------------------------------
    // Generator
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ImmutableArray<Result<InvocationModel>> invocations)
    {
        foreach (var info in invocations.SelectError())
        {
            context.ReportDiagnostic(info);
        }

        var builder = new SourceBuilder();

        builder.AutoGenerated();
        builder.EnableNullable();

        // namespace
        builder.Append("namespace Smart.CommandLine.Hosting.Generated;").NewLine();
        builder.NewLine();

        // using
        builder.Append("using System;").NewLine();
        builder.Append("using System.Runtime.CompilerServices;").NewLine();
        builder.NewLine();
        builder.Append("using Smart.CommandLine.Hosting;").NewLine();
        builder.NewLine();

        // class
        builder.Append("internal static partial class BuilderInterceptors").NewLine();
        builder.BeginScope();

        var targetInvocations = invocations.SelectValue().ToList();
        for (var i = 0; i < targetInvocations.Count; i++)
        {
            var invocation = targetInvocations[i];
            var methodName = $"{invocation.MethodName}_Interceptor_{i}";
            var localFunctionName = $"Builder_{i}";

            builder.Indent().Append($"// {invocation.InterceptableLocation.GetDisplayLocation()}").NewLine();
            builder.Indent().Append($"//[InterceptsLocation({invocation.InterceptableLocation.Version}, @\"{invocation.InterceptableLocation.Data}\")]").NewLine();
            builder.Indent().Append($"internal static void {methodName}(this {invocation.ReceiverType} builder)").NewLine();
            builder.BeginScope();

            // TODO

            builder.EndScope();
        }

        builder.EndScope();

        context.AddSource("Interceptors.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    //{

    //    var builder = new SourceBuilder();
    //    foreach (var group in methods.SelectValue().GroupBy(static x => new { x.Namespace, x.ClassName }))
    //    {
    //        context.CancellationToken.ThrowIfCancellationRequested();

    //        builder.Clear();
    //        BuildSource(builder, option, group.ToList());

    //        var filename = MakeFilename(group.Key.Namespace, group.Key.ClassName);
    //        var source = builder.ToString();
    //        context.AddSource(filename, SourceText.From(source, Encoding.UTF8));
    //    }
    //}

    //private static void BuildSource(SourceBuilder builder, OptionModel option, List<InvokeModel> methods)
    //{
    //    var ns = methods[0].Namespace;
    //    var className = methods[0].ClassName;
    //    var isValueType = methods[0].IsValueType;

    //    builder.AutoGenerated();
    //    builder.EnableNullable();
    //    builder.NewLine();

    //    // namespace
    //    if (!String.IsNullOrEmpty(ns))
    //    {
    //        builder.Namespace(ns);
    //        builder.NewLine();
    //    }

    //    // class
    //    builder
    //        .Indent()
    //        .Append("partial ")
    //        .Append(isValueType ? "struct " : "class ")
    //        .Append(className)
    //        .NewLine();
    //    builder.BeginScope();

    //    builder.Indent().Append("// Option: ").Append(option.Value).NewLine();

    //    var first = true;
    //    foreach (var method in methods)
    //    {
    //        if (first)
    //        {
    //            first = false;
    //        }
    //        else
    //        {
    //            builder.NewLine();
    //        }

    //        // method
    //        builder
    //            .Indent()
    //            .Append(method.MethodAccessibility.ToText())
    //            .Append(" static partial void ")
    //            .Append(method.MethodName)
    //            .Append("()")
    //            .NewLine();
    //        builder.BeginScope();

    //        builder
    //            .Indent()
    //            .Append("Console.WriteLine(\"Hello world.\");")
    //            .NewLine();

    //        builder.EndScope();
    //    }

    //    builder.EndScope();
    //}

    //// ------------------------------------------------------------
    //// Helper
    //// ------------------------------------------------------------

    // TODO

    // ------------------------------------------------------------
    // Attribute
    // ------------------------------------------------------------

    private const string InterceptsLocationAttributeSource = @"#nullable enable

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class InterceptsLocationAttribute : Attribute
{
    public InterceptsLocationAttribute(int version, string data)
    {
        Version = version;
        Data = data;
    }

    public int Version { get; }
    public string Data { get; }
}
";
}

// TODO
// location
// TypeArgument: global::Develop.TestCommand
// TypeName: TestCommand
// ReceiverType: global::Smart.CommandLine.Hosting.ICommandBuilder ...
// MethodName: AddCommand, AddSubCommand, UseHandler
internal sealed record InvocationModel(
    InterceptableLocation InterceptableLocation,
    string TypeArgument,
    string TypeName,
    string ReceiverType,
    string MethodName,
    CommandModel? CommandInfo,
    EquatableArray<OptionModel> Options);

internal sealed record CommandModel(
    string CommandName,
    string? CommandDescription);

internal sealed record OptionModel(
    string PropertyName,
    string PropertyType,
    bool IsGeneric,
    string? GenericTypeArgument,
    int Order,
    int HierarchyLevel,
    int PropertyIndex,
    string Name,
    EquatableArray<string> Aliases,
    string? Description,
    bool Required,
    string? CompletionsElementType, // TODO GenericTypeArgumentで代用可能
    EquatableArray<string>? Completions);
