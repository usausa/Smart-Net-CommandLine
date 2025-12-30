using Smart.CommandLine.Hosting.Generator.Models;

namespace Smart.CommandLine.Hosting.Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using SourceGenerateHelper;
using System.Collections.Immutable;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

[Generator]
public sealed class CommandGenerator : IIncrementalGenerator
{
    private const string EnableInterceptorOptionName = "build_property.EnableSmartCommandLineHostingGenerator";

    private const string AddCommandMethodName = "AddCommand";
    private const string AddSubCommandMethodName = "AddSubCommand";
    private const string UseHandlerMethodName = "UseHandler";

    private const string CommandBuilderFullName = "Smart.CommandLine.Hosting.ICommandBuilder";
    private const string SubCommandBuilderFullName = "Smart.CommandLine.Hosting.ISubCommandBuilder";
    private const string RootCommandBuilderFullName = "Smart.CommandLine.Hosting.IRootCommandBuilder";

    private const string CommandAttributeFullName = "Smart.CommandLine.Hosting.CommandAttribute";
    private const string FilterAttributeFullName = "Smart.CommandLine.Hosting.FilterAttribute";
    private const string BaseOptionAttributeFullName = "Smart.CommandLine.Hosting.BaseOptionAttribute";

    // ------------------------------------------------------------
    // Initialize
    // ------------------------------------------------------------

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Read setting
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

        // Check method name
        var methodName = genericName.Identifier.Text;
        return methodName is AddCommandMethodName or AddSubCommandMethodName or UseHandlerMethodName;
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

        // Get type argument
        var typeArgument = method.TypeArguments[0];

        // Get receiver type
        var receiverType = operation.Instance?.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty;

        // Extract command metadata
        var commandInfo = ExtractCommandMetadata(typeArgument);

        // Extract filter metadata
        var filters = ExtractFilterMetadata(typeArgument);

        // Extract option metadata
        var options = ExtractOptionMetadata(typeArgument);

        return Results.Success(new InvocationModel(
            typeArgument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            typeArgument.Name,
            receiverType,
            method.Name,
            commandInfo,
            filters,
            options));
    }

    private static CommandMetadata? ExtractCommandMetadata(ITypeSymbol typeSymbol)
    {
        var attribute = typeSymbol.GetAttributes()
            .FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == CommandAttributeFullName);

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

        return new CommandMetadata(name, description);
    }

    private static ImmutableArray<FilterMetadata> ExtractFilterMetadata(ITypeSymbol typeSymbol)
    {
        var filters = new List<FilterMetadata>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is null)
            {
                continue;
            }

            // Check if it's FilterAttribute or FilterAttribute<TFilter>
            var baseType = attribute.AttributeClass;
            while (baseType is not null)
            {
                if (baseType.ToDisplayString() == FilterAttributeFullName)
                {
                    break;
                }
                baseType = baseType.BaseType;
            }

            if (baseType is null)
            {
                continue;
            }

            // Get Order property
            var order = 0;
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "Order" && namedArg.Value.Value is int orderValue)
                {
                    order = orderValue;
                    break;
                }
            }

            // Get TFilter type (from generic argument)
            string? filterType = null;
            if (attribute.AttributeClass is INamedTypeSymbol { IsGenericType: true } namedType &&
                namedType.TypeArguments.Length > 0)
            {
                filterType = namedType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            if (filterType is not null)
            {
                filters.Add(new FilterMetadata(order, filterType));
            }
        }

        return filters.ToImmutableArray();
    }

    private static ImmutableArray<OptionMetadata> ExtractOptionMetadata(ITypeSymbol typeSymbol)
    {
        var options = new List<OptionMetadata>();

        // Get all properties including base classes
        var currentType = typeSymbol;
        var hierarchyLevel = 0;
        while (currentType is not null && currentType.SpecialType != SpecialType.System_Object)
        {
            var members = currentType.GetMembers();
            var propertyIndex = 0;

            foreach (var member in members)
            {
                if (member is not IPropertySymbol property)
                {
                    continue;
                }

                foreach (var attribute in property.GetAttributes())
                {
                    if (attribute.AttributeClass is null)
                    {
                        continue;
                    }

                    // Check if it's BaseOptionAttribute or derived
                    var baseType = attribute.AttributeClass;
                    while (baseType is not null)
                    {
                        if (baseType.ToDisplayString() == BaseOptionAttributeFullName)
                        {
                            break;
                        }
                        baseType = baseType.BaseType;
                    }

                    if (baseType is null)
                    {
                        continue;
                    }

                    // Extract option information
                    var order = int.MaxValue;
                    var name = string.Empty;
                    var aliases = ImmutableArray<string>.Empty;
                    string? description = null;
                    var required = false;
                    object? defaultValue = null;
                    ImmutableArray<string> completions = ImmutableArray<string>.Empty;

                    // Constructor arguments: order, name, aliases
                    if (attribute.ConstructorArguments.Length >= 2)
                    {
                        // Check if first arg is int (order) or string (name)
                        if (attribute.ConstructorArguments[0].Type?.SpecialType == SpecialType.System_Int32)
                        {
                            order = attribute.ConstructorArguments[0].Value is int orderValue ? orderValue : int.MaxValue;
                            name = attribute.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
                            if (attribute.ConstructorArguments.Length >= 3)
                            {
                                aliases = ExtractStringArray(attribute.ConstructorArguments[2]);
                            }
                        }
                        else
                        {
                            name = attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
                            if (attribute.ConstructorArguments.Length >= 2)
                            {
                                aliases = ExtractStringArray(attribute.ConstructorArguments[1]);
                            }
                        }
                    }

                    // Named arguments
                    foreach (var namedArg in attribute.NamedArguments)
                    {
                        switch (namedArg.Key)
                        {
                            case "Description":
                                description = namedArg.Value.Value?.ToString();
                                break;
                            case "Required":
                                required = namedArg.Value.Value is bool reqValue && reqValue;
                                break;
                            case "DefaultValue":
                                defaultValue = namedArg.Value.Value;
                                break;
                            case "Completions":
                                // Get generic type argument if attribute is OptionAttribute<T>
                                ITypeSymbol? genericTypeArgument = null;
                                if (attribute.AttributeClass is INamedTypeSymbol { IsGenericType: true } namedType &&
                                    namedType.TypeArguments.Length > 0)
                                {
                                    genericTypeArgument = namedType.TypeArguments[0];
                                }

                                // Get the syntax node for the attribute
                                if (attribute.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax attributeSyntax &&
                                    attributeSyntax.ArgumentList is not null)
                                {
                                    foreach (var argument in attributeSyntax.ArgumentList.Arguments)
                                    {
                                        if (argument.NameEquals?.Name.Identifier.Text == "Completions")
                                        {
                                            completions = ExtractCompletionsPropertyFromSyntax(argument.Expression, genericTypeArgument);
                                            break;
                                        }
                                    }
                                }
                                break;
                        }
                    }

                    options.Add(new OptionMetadata(
                        property.Name,
                        order,
                        hierarchyLevel,
                        propertyIndex,
                        name,
                        aliases,
                        description,
                        required,
                        defaultValue,
                        completions));

                    propertyIndex++;
                }
            }

            currentType = currentType.BaseType;
            hierarchyLevel--;
        }

        return options.ToImmutableArray();
    }

    private static ImmutableArray<string> ExtractStringArray(TypedConstant arrayConstant)
    {
        if (arrayConstant is { Kind: TypedConstantKind.Array, Values.IsEmpty: false })
        {
            var result = ImmutableArray.CreateBuilder<string>();

            foreach (var element in arrayConstant.Values)
            {
                if (element.Value is string str)
                {
                    result.Add(str);
                }
            }

            return result.ToImmutable();
        }

        return ImmutableArray<string>.Empty;
    }

    private static ImmutableArray<string> ExtractCompletionsPropertyFromSyntax(ExpressionSyntax expression, ITypeSymbol? genericTypeArgument)
    {
        var completions = ImmutableArray.CreateBuilder<string>();

        // Check for implicit array creation: new[] { ... }
        if (expression is ImplicitArrayCreationExpressionSyntax arrayCreation)
        {
            foreach (var element in arrayCreation.Initializer.Expressions)
            {
                // Get the text of the element as written in source
                completions.Add(element.ToString());
            }
        }
        // Check for collection expression: [ ... ] (C# 12+)
        else if (expression is CollectionExpressionSyntax collectionExpression)
        {
            foreach (var element in collectionExpression.Elements)
            {
                if (element is ExpressionElementSyntax expressionElement)
                {
                    // Get the text of the element as written in source
                    completions.Add(expressionElement.Expression.ToString());
                }
            }
        }

        return completions.Count > 0 ? completions.ToImmutable() : ImmutableArray<string>.Empty;
    }

    // ------------------------------------------------------------
    // Execute
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ImmutableArray<Result<InvocationModel>> invocations)
    {
        foreach (var info in invocations.SelectError())
        {
            context.ReportDiagnostic(info);
        }

        // Build initializer source
        var builder = new SourceBuilder();

        builder.AutoGenerated();
        builder.EnableNullable();
        builder.NewLine();

        // class
        builder
            .Indent()
            .Append("internal static class CommandInitializer")
            .NewLine();
        builder.BeginScope();

        // method
        builder
            .Indent()
            .Append("[global::System.Runtime.CompilerServices.ModuleInitializer]")
            .NewLine();
        builder
            .Indent()
            .Append("public static void Initialize()")
            .NewLine();
        builder.BeginScope();

        // TODO


        builder.EndScope();

        builder.EndScope();

        context.AddSource(
            "CommandInitializer.g.cs",
            SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    // ------------------------------------------------------------
    // Models
    // ------------------------------------------------------------

    internal sealed record InvocationModel(
        string TypeFullName,
        string TypeName,
        string ReceiverType,
        string MethodName,
        CommandMetadata? CommandInfo,
        ImmutableArray<FilterMetadata> Filters,
        ImmutableArray<OptionMetadata> Options);

    internal sealed record CommandMetadata(
        string Name,
        string? Description);

    internal sealed record FilterMetadata(
        int Order,
        string FilterType);

    internal sealed record OptionMetadata(
        string PropertyName,
        int Order,
        int HierarchyLevel,
        int PropertyIndex,
        string Name,
        ImmutableArray<string> Aliases,
        string? Description,
        bool Required,
        object? DefaultValue,
        ImmutableArray<string> Completions);
}
