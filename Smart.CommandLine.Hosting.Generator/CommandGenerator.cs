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
    private const string CommandHandlerFullName = "Smart.CommandLine.Hosting.ICommandHandler";

    private const string DescriptionPropertyName = "Description";
    private const string RequiredPropertyName = "Required";
    private const string DefaultValuePropertyName = "DefaultValue";
    private const string CompletionsPropertyName = "Completions";

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
        if (provider.GlobalOptions.TryGetValue(EnableInterceptorOptionName, out var value) && !String.IsNullOrEmpty(value))
        {
            return new GeneratorSetting(Boolean.TryParse(value, out var result) && result);
        }

        return new GeneratorSetting(true);
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

    private static InvocationModel? GetInvocationModel(GeneratorSyntaxContext context)
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
        var command = ExtractCommandModel(typeArgument);
        if (command is null)
        {
            return null;
        }

        var isImplementsHandler = IsImplementsHandler(typeArgument);
        var filters = ExtractFilterModels(typeArgument);
        var options = ExtractOptionModels(typeArgument);

        return new InvocationModel(
            typeArgument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            isImplementsHandler,
            command,
            filters,
            options);
    }

    private static bool IsImplementsHandler(ITypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(static x => x.ToDisplayString() == CommandHandlerFullName);
    }

    private static EquatableArray<FilterModel> ExtractFilterModels(ITypeSymbol typeSymbol)
    {
        var filters = new List<FilterModel>();

        var currentType = typeSymbol;
        while ((currentType is not null) && (currentType.SpecialType != SpecialType.System_Object))
        {
            foreach (var attribute in currentType.GetAttributes())
            {
                if (attribute.AttributeClass is null)
                {
                    continue;
                }

                // Check FilterAttribute<TFilter>
                if (!attribute.AttributeClass.IsGenericType)
                {
                    continue;
                }

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
                if (attribute.AttributeClass.TypeArguments.Length > 0)
                {
                    var filterType = attribute.AttributeClass.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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

    private static EquatableArray<OptionModel> ExtractOptionModels(ITypeSymbol typeSymbol)
    {
        var options = new List<OptionModel>();

        var hierarchyLevel = 0;
        var currentType = typeSymbol;
        while ((currentType is not null) && (currentType.SpecialType != SpecialType.System_Object))
        {
            var propertyIndex = 0;
            foreach (var member in currentType.GetMembers())
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

                    // Check BaseOptionAttribute derived
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

                    // Get property type
                    var propertyType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

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
                        if (attribute.ConstructorArguments[0].Type?.SpecialType == SpecialType.System_Int32)
                        {
                            // With order
                            order = attribute.ConstructorArguments[0].Value is int orderValue ? orderValue : int.MaxValue;
                            name = attribute.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
                            if (attribute.ConstructorArguments.Length >= 3)
                            {
                                aliases = ExtractAliases(attribute.ConstructorArguments[2]);
                            }
                        }
                        else
                        {
                            // No order
                            name = attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
                            if (attribute.ConstructorArguments.Length >= 2)
                            {
                                aliases = ExtractAliases(attribute.ConstructorArguments[1]);
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
                            case DescriptionPropertyName:
                                description = namedArg.Value.Value?.ToString();
                                break;
                            case RequiredPropertyName:
                                required = namedArg.Value.Value is true;
                                break;
                        }
                    }

                    // Extract from syntax to preserve original code
                    if (attributeSyntax?.ArgumentList is not null)
                    {
                        foreach (var argument in attributeSyntax.ArgumentList.Arguments)
                        {
                            if (argument.NameEquals?.Name.Identifier.Text == DefaultValuePropertyName)
                            {
                                defaultValue = argument.Expression.ToString();
                            }
                            else if (argument.NameEquals?.Name.Identifier.Text == CompletionsPropertyName)
                            {
                                completions = ExtractCompletionsFromSyntax(argument.Expression);
                                break;
                            }
                        }
                    }

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

    private static string[] ExtractAliases(TypedConstant arrayConstant)
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

    private static string[] ExtractCompletionsFromSyntax(ExpressionSyntax expression)
    {
        var completions = new List<string>();

        // Check for implicit array creation: new[] { ... }
        if (expression is ImplicitArrayCreationExpressionSyntax arrayCreation)
        {
            foreach (var element in arrayCreation.Initializer.Expressions)
            {
                completions.Add(element.ToString());
            }
        }
        // Check for collection expression: [ ... ]
        else if (expression is CollectionExpressionSyntax collectionExpression)
        {
            foreach (var element in collectionExpression.Elements)
            {
                if (element is ExpressionElementSyntax expressionElement)
                {
                    completions.Add(expressionElement.Expression.ToString());
                }
            }
        }

        return completions.ToArray();
    }

    // ------------------------------------------------------------
    // Execute
    // ------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ImmutableArray<InvocationModel> invocations)
    {
        if (invocations.IsEmpty)
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
        foreach (var invocation in invocations)
        {
            builder
                .Indent()
                .Append("// ")
                .Append(invocation.TypeFullName)
                .NewLine();

            // AddCommandModel
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
            if (invocation.ImplementsHandler)
            {
                GenerateActionBuilder(builder, invocation);
            }

            builder.NewLine();
        }

        builder.EndScope();

        builder.EndScope();

        // Add source
        context.AddSource("CommandInitializer.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private static void GenerateActionBuilder(SourceBuilder builder, InvocationModel invocation)
    {
        var options = invocation.Options.ToArray();
        var sortedOptions = options.Length > 0
            ? options.OrderBy(static x => x.Order).ThenBy(static x => x.HierarchyLevel).ThenBy(static x => x.PropertyIndex).ToList()
            : [];

        builder
            .Indent()
            .Append("global::Smart.CommandLine.Hosting.CommandMetadataProvider.AddActionBuilder<")
            .Append(invocation.TypeFullName)
            .Append(">(static context =>")
            .NewLine();
        builder
            .Indent()
            .Append("{")
            .NewLine();
        builder.IndentLevel++;

        // Generate option variables
        for (var i = 0; i < sortedOptions.Count; i++)
        {
            var option = sortedOptions[i];
            var optionVar = $"option{i}";

            builder
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
                    .Append(optionVar)
                    .Append(".Required = true;")
                    .NewLine();
            }

            // Set default value
            if (option.DefaultValue is not null)
            {
                builder
                    .Indent()
                    .Append(optionVar)
                    .Append(".DefaultValueFactory = static _ => ")
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
                    .Append(optionVar)
                    .Append(".CompletionSources.Add([");
                for (var j = 0; j < completions.Length; j++)
                {
                    if (j > 0)
                    {
                        builder.Append(", ");
                    }
                    builder.Append(completions[j]);
                }
                builder
                    .Append("]);")
                    .NewLine();
            }

            // Add option to context
            builder
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
            .Append("context.Operation = (command, result, commandContext) =>")
            .NewLine();
        builder
            .Indent()
            .Append("{")
            .NewLine();
        builder.IndentLevel++;

        if (sortedOptions.Count > 0)
        {
            builder
                .Indent()
                .Append("var target = (")
                .Append(invocation.TypeFullName)
                .Append(")commandContext.Command;")
                .NewLine();

            builder.NewLine();
        }

        // Set property values
        for (var i = 0; i < sortedOptions.Count; i++)
        {
            var option = sortedOptions[i];
            var optionVar = $"option{i}";

            builder
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
            .Append("return command.ExecuteAsync(commandContext);")
            .NewLine();

        builder.IndentLevel--;
        builder
            .Indent()
            .Append("};")
            .NewLine();

        builder.IndentLevel--;
        builder
            .Indent()
            .Append("});")
            .NewLine();
    }
}
