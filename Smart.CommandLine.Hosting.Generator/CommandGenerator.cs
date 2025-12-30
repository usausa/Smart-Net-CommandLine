namespace Smart.CommandLine.Hosting.Generator;

using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

using Smart.CommandLine.Hosting.Generator.Models;

using SourceGenerateHelper;

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
        var containingTypeName = method.OriginalDefinition.ContainingType.ToDisplayString();
        if ((containingTypeName != CommandBuilderFullName) &&
            (containingTypeName != SubCommandBuilderFullName) &&
            (containingTypeName != RootCommandBuilderFullName))
        {
            return null;
        }

        var typeArgument = method.TypeArguments[0];
        return Results.Success(new InvocationModel(
            typeArgument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            ExtractFilterModels(typeArgument),
            ExtractCommandModel(typeArgument),
            ExtractOptionModels(typeArgument)));
    }

    private static EquatableArray<FilterModel> ExtractFilterModels(ITypeSymbol typeSymbol)
    {
        var filters = new List<FilterModel>();

        // Check current type and all base types for FilterAttribute
        var currentType = typeSymbol;
        while (currentType is not null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var attribute in currentType.GetAttributes())
            {
                if (attribute.AttributeClass is null)
                {
                    continue;
                }

                // Check if it's FilterAttribute<TFilter> (generic type)
                if (!attribute.AttributeClass.IsGenericType)
                {
                    continue;
                }

                // Get the unbound generic type and check its full name
                var unboundGenericType = attribute.AttributeClass.OriginalDefinition;
                var fullName = $"{unboundGenericType.ContainingNamespace.ToDisplayString()}.{unboundGenericType.Name}";

                if (fullName != FilterAttributeFullName)
                {
                    continue;
                }

                // Get Order property
                var order = 0;
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg is { Key: "Order", Value.Value: int orderValue })
                    {
                        order = orderValue;
                        break;
                    }
                }

                // Get TFilter type (from generic argument)
                string? filterType = null;
                if (attribute.AttributeClass.TypeArguments.Length > 0)
                {
                    filterType = attribute.AttributeClass.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }

                if (filterType is not null)
                {
                    filters.Add(new FilterModel(filterType, order));
                }
            }

            currentType = currentType.BaseType;
        }

        return new EquatableArray<FilterModel>(filters.ToArray());
    }

    private static CommandModel? ExtractCommandModel(ITypeSymbol typeSymbol)
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
        return new CommandModel(name, description);
    }

    private static EquatableArray<OptionModel> ExtractOptionModels(ITypeSymbol typeSymbol)
    {
        var options = new List<OptionModel>();

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
                    var aliases = Array.Empty<string>();
                    string? description = null;
                    var required = false;
                    string? defaultValue = null;
                    var completions = Array.Empty<string>();

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

                    // Get the syntax node for the attribute
                    var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;

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
                                // Extract from syntax to preserve original code
                                if (attributeSyntax?.ArgumentList is not null)
                                {
                                    foreach (var argument in attributeSyntax.ArgumentList.Arguments)
                                    {
                                        if (argument.NameEquals?.Name.Identifier.Text == "DefaultValue")
                                        {
                                            defaultValue = argument.Expression.ToString();
                                            break;
                                        }
                                    }
                                }
                                break;
                            case "Completions":
                                // Get the syntax node for completions
                                if (attributeSyntax?.ArgumentList is not null)
                                {
                                    foreach (var argument in attributeSyntax.ArgumentList.Arguments)
                                    {
                                        if (argument.NameEquals?.Name.Identifier.Text == "Completions")
                                        {
                                            completions = ExtractCompletionsPropertyFromSyntax(argument.Expression);
                                            break;
                                        }
                                    }
                                }
                                break;
                        }
                    }

                    // Get property type
                    var propertyType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    options.Add(new OptionModel(
                        property.Name,
                        propertyType,
                        order,
                        hierarchyLevel,
                        propertyIndex,
                        name,
                        new EquatableArray<string>(aliases),
                        description,
                        required,
                        defaultValue,
                        new EquatableArray<string>(completions)));

                    propertyIndex++;
                }
            }

            currentType = currentType.BaseType;
            hierarchyLevel--;
        }

        return new EquatableArray<OptionModel>(options.ToArray());
    }

    private static string[] ExtractStringArray(TypedConstant arrayConstant)
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

            return result.ToArray();
        }

        return [];
    }

    private static string[] ExtractCompletionsPropertyFromSyntax(ExpressionSyntax expression)
    {
        var completions = new List<string>();

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

        return completions.ToArray();
    }

    // ------------------------------------------------------------
    // Execute
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ImmutableArray<Result<InvocationModel>> invocations)
    {
        // TODO error ?
        //foreach (var result in invocations)
        //{
        //    if (result.Value is not null)
        //    {
        //        successfulInvocations.Add(result.Value);
        //    }
        //    else if (result.Error is not null)
        //    {
        //        context.ReportDiagnostic(result.Error);
        //    }
        //}

        var targetInvocations = invocations.SelectValue().ToList();
        if (targetInvocations.Count == 0)
        {
            return;
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

        // Generate metadata registration for each invocation
        foreach (var invocation in targetInvocations)
        {
            // AddCommandModel
            if (invocation.CommandInfo is not null)
            {
                builder
                    .Indent()
                    .Append("global::Smart.CommandLine.Hosting.CommandMetadataProvider.AddCommandMetadata<")
                    .Append(invocation.TypeFullName)
                    .Append(">(");
                builder
                    .Append('"')
                    .Append(invocation.CommandInfo.Name)
                    .Append('"');
                if (!string.IsNullOrEmpty(invocation.CommandInfo.Description))
                {
                    builder
                        .Append(", ")
                        .Append('"')
                        .Append(invocation.CommandInfo.Description!)
                        .Append('"');
                }
                builder
                    .Append(");")
                    .NewLine();
            }

            // AddFilterDescriptor
            foreach (var filter in invocation.Filters.ToArray())
            {
                builder
                    .Indent()
                    .Append("global::Smart.CommandLine.Hosting.CommandMetadataProvider.AddFilterDescriptor<")
                    .Append(invocation.TypeFullName)
                    .Append(", ")
                    .Append(filter.FilterType)
                    .Append(">(")
                    .Append($"{filter.Order}")
                    .Append(");")
                    .NewLine();
            }

            // AddActionBuilder
            var options = invocation.Options.ToArray();
            if (options.Length > 0)
            {
                GenerateActionBuilder(builder, invocation);
            }

            builder.NewLine();
        }

        builder.EndScope();

        builder.EndScope();

        context.AddSource(
            "CommandInitializer.g.cs",
            SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private static void GenerateActionBuilder(SourceBuilder builder, InvocationModel invocation)
    {
        // Sort options by Order, HierarchyLevel, PropertyIndex
        var sortedOptions = invocation.Options.ToArray()
            .OrderBy(o => o.Order)
            .ThenBy(o => o.HierarchyLevel)
            .ThenBy(o => o.PropertyIndex)
            .ToList();

        builder
            .Indent()
            .Append("global::Smart.CommandLine.Hosting.CommandMetadataProvider.AddActionBuilder<")
            .Append(invocation.TypeFullName)
            .Append(">(context =>")
            .NewLine();
        builder
            .Indent()
            .Append("{")
            .NewLine();

        // Generate option variables
        for (var i = 0; i < sortedOptions.Count; i++)
        {
            var option = sortedOptions[i];
            var optionVar = $"option{i + 1}";

            builder
                .Indent()
                .Indent()
                .Append("var ")
                .Append(optionVar)
                .Append(" = new global::System.CommandLine.Option<")
                .Append(option.PropertyType)
                .Append(">(\"")
                .Append(option.Name)
                .Append("\"");

            // Add aliases
            var aliases = option.Aliases.ToArray();
            if (aliases.Length > 0)
            {
                foreach (var alias in aliases)
                {
                    builder
                        .Append(", \"")
                        .Append(alias)
                        .Append("\"");
                }
            }

            builder
                .Append(");")
                .NewLine();

            // Set description
            if (!string.IsNullOrEmpty(option.Description))
            {
                builder
                    .Indent()
                    .Indent()
                    .Append(optionVar)
                    .Append(".Description = \"")
                    .Append(option.Description!)
                    .Append("\";")
                    .NewLine();
            }

            // Set required
            if (option.Required)
            {
                builder
                    .Indent()
                    .Indent()
                    .Append(optionVar)
                    .Append(".Required = true;")
                    .NewLine();
            }

            // Set default value
            if (option.DefaultValue is not null)
            {
                builder
                    .Indent()
                    .Indent()
                    .Append(optionVar)
                    .Append(".DefaultValueFactory = _ => ")
                    .Append(option.DefaultValue)
                    .Append(";")
                    .NewLine();
            }

            // Set completions
            var completions = option.Completions.ToArray();
            if (completions.Length > 0)
            {
                builder
                    .Indent()
                    .Indent()
                    .Append(optionVar)
                    .Append(".CompletionSources.Add(new string[] { ");
                for (var j = 0; j < completions.Length; j++)
                {
                    if (j > 0)
                    {
                        builder.Append(", ");
                    }
                    builder.Append(completions[j]);
                }
                builder
                    .Append(" });")
                    .NewLine();
            }

            // Add option to context
            builder
                .Indent()
                .Indent()
                .Append("context.AddOption(")
                .Append(optionVar)
                .Append(");")
                .NewLine();

            if (i < sortedOptions.Count - 1)
            {
                builder.NewLine();
            }
        }

        builder.NewLine();

        // Generate Operation
        builder
            .Indent()
            .Indent()
            .Append("context.Operation = (command, result, commandContext) =>")
            .NewLine();
        builder
            .Indent()
            .Indent()
            .Append("{")
            .NewLine();

        builder
            .Indent()
            .Indent()
            .Indent()
            .Append("var target = (")
            .Append(invocation.TypeFullName)
            .Append(")commandContext.Command;")
            .NewLine();

        if (sortedOptions.Count > 0)
        {
            builder.NewLine();
        }

        // Set property values
        for (var i = 0; i < sortedOptions.Count; i++)
        {
            var option = sortedOptions[i];
            var optionVar = $"option{i + 1}";

            builder
                .Indent()
                .Indent()
                .Indent()
                .Append("target.")
                .Append(option.PropertyName)
                .Append(" = result.GetValue(")
                .Append(optionVar)
                .Append(")!;")
                .NewLine();
        }

        if (sortedOptions.Count > 0)
        {
            builder.NewLine();
        }

        // Execute command
        builder
            .Indent()
            .Indent()
            .Indent()
            .Append("return command.ExecuteAsync(commandContext);")
            .NewLine();

        builder
            .Indent()
            .Indent()
            .Append("};")
            .NewLine();

        builder
            .Indent()
            .Append("});")
            .NewLine();
    }

    // ------------------------------------------------------------
    // Models
    // ------------------------------------------------------------

    internal sealed record InvocationModel(
        string TypeFullName,
        EquatableArray<FilterModel> Filters,
        CommandModel? CommandInfo,
        EquatableArray<OptionModel> Options);

    internal sealed record CommandModel(
        string Name,
        string? Description);

    internal sealed record FilterModel(
        string FilterType,
        int Order);

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
}
