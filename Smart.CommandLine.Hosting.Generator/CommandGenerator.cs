namespace Smart.CommandLine.Hosting.Generator;

using Microsoft.CodeAnalysis;

[Generator]
public sealed class CommandGenerator : IIncrementalGenerator
{
    //private const string EnableInterceptorOptionName = "build_property.EnableSmartCommandLineHostingGenerator";

    //private const string AddCommandMethodName = "AddCommand";
    //private const string AddSubCommandMethodName = "AddSubCommand";
    //private const string UseHandlerMethodName = "UseHandler";

    //private const string CommandBuilderFullName = "Smart.CommandLine.Hosting.ICommandBuilder";
    //private const string SubCommandBuilderFullName = "Smart.CommandLine.Hosting.ISubCommandBuilder";
    //private const string RootCommandBuilderFullName = "Smart.CommandLine.Hosting.IRootCommandBuilder";

    //private const string CommandAttributeFullName = "Smart.CommandLine.Hosting.CommandAttribute";
    //private const string BaseOptionAttributeFullName = "Smart.CommandLine.Hosting.BaseOptionAttribute";
    //private const string OptionAttributeFullName = "Smart.CommandLine.Hosting.OptionAttribute";

    //private const string CommandHandlerFullName = "Smart.CommandLine.Hosting.ICommandHandler";

    // ------------------------------------------------------------
    // Initialize
    // ------------------------------------------------------------

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //context.RegisterPostInitializationOutput(static context =>
        //{
        //    context.AddSource("InterceptsLocationAttribute.g.cs", SourceText.From(InterceptsLocationAttributeSource, Encoding.UTF8));
        //});

        //// Read option property
        //var settingProvider = context.AnalyzerConfigOptionsProvider
        //    .Select(static (provider, _) => SelectSetting(provider));

        //// Find invocations
        //var invocationProvider = context.SyntaxProvider
        //    .CreateSyntaxProvider(
        //        predicate: static (node, _) => IsTargetInvocation(node),
        //        transform: static (context, _) => GetInvocationModel(context))
        //    .Where(static x => x is not null)
        //    .Collect();

        //var combined = settingProvider.Combine(invocationProvider);

        //// Execute
        //context.RegisterSourceOutput(combined, static (context, source) =>
        //{
        //    var (setting, invocations) = source;

        //    if (!setting.Enable)
        //    {
        //        return;
        //    }

        //    if (invocations.IsEmpty)
        //    {
        //        return;
        //    }

        //    Execute(context, invocations!);
        //});
    }

    //// ------------------------------------------------------------
    //// Parser
    //// ------------------------------------------------------------

    //private static GeneratorSetting SelectSetting(AnalyzerConfigOptionsProvider provider)
    //{
    //    var enable = provider.GlobalOptions.TryGetValue(EnableInterceptorOptionName, out var value) &&
    //                 bool.TryParse(value, out var result) &&
    //                 result;
    //    return new GeneratorSetting(enable);
    //}

    //private static bool IsTargetInvocation(SyntaxNode node)
    //{
    //    if (node is not InvocationExpressionSyntax invocation)
    //    {
    //        return false;
    //    }

    //    // Check method access (e.g., builder.AddCommand<T>(), builder.AddSubCommand<T>(), builder.UseHandler<T>())
    //    if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
    //    {
    //        return false;
    //    }

    //    // Check generic method
    //    if (memberAccess.Name is not GenericNameSyntax genericName)
    //    {
    //        return false;
    //    }

    //    // Check method name
    //    var methodName = genericName.Identifier.Text;
    //    return methodName is AddCommandMethodName or AddSubCommandMethodName or UseHandlerMethodName;
    //}

    //private static Result<InvocationModel>? GetInvocationModel(GeneratorSyntaxContext context)
    //{
    //    var invocation = (InvocationExpressionSyntax)context.Node;

    //    if (context.SemanticModel.GetOperation(invocation) is not IInvocationOperation operation)
    //    {
    //        return null;
    //    }

    //    // Check method
    //    var method = operation.TargetMethod;
    //    if (!method.IsGenericMethod ||
    //        (method.TypeArguments.Length != 1) ||
    //        ((method.Name != AddCommandMethodName) && (method.Name != AddSubCommandMethodName) && (method.Name != UseHandlerMethodName)))
    //    {
    //        return null;
    //    }

    //    // Check containing type
    //    var containingType = method.OriginalDefinition.ContainingType;
    //    if ((containingType.ToDisplayString() != CommandBuilderFullName) &&
    //        (containingType.ToDisplayString() != SubCommandBuilderFullName) &&
    //        (containingType.ToDisplayString() != RootCommandBuilderFullName))
    //    {
    //        return null;
    //    }

    //    // Exclude methods with Action<CommandActionBuilderContext> as first parameter
    //    if (method.Parameters.Length > 0)
    //    {
    //        var firstParam = method.Parameters[0];
    //        if (firstParam.Type is INamedTypeSymbol { TypeArguments.Length: 1 } namedType)
    //        {
    //            var typeArg = namedType.TypeArguments[0];
    //            if (typeArg.ToDisplayString() == "Smart.CommandLine.Hosting.CommandActionBuilderContext")
    //            {
    //                return null;
    //            }
    //        }
    //    }

    //    // Get type argument
    //    var typeArgument = method.TypeArguments[0];

    //    // Check attribute
    //    var command = ExtractCommandModel(typeArgument);
    //    if (command is null)
    //    {
    //        // TODO error ?
    //        return null;
    //    }

    //    // Check if is ICommandHandler
    //    if (!IsHandler(typeArgument))
    //    {
    //        return null;
    //    }

    //    // Get interceptable location
    //    var interceptableLocation = context.SemanticModel.GetInterceptableLocation(invocation);
    //    if (interceptableLocation is null)
    //    {
    //        return null;
    //    }

    //    // Get information
    //    var receiverType = operation.Instance!.Type!;

    //    // TODO property check ?

    //    return Results.Success(new InvocationModel(
    //        interceptableLocation,
    //        typeArgument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
    //        typeArgument.Name,
    //        receiverType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
    //        method.Name,
    //        command,
    //        // TODO
    //        new EquatableArray<OptionModel>([])));
    //}

    //private static CommandModel? ExtractCommandModel(ITypeSymbol typeSymbol)
    //{
    //    var attribute = typeSymbol.GetAttributes()
    //        .FirstOrDefault(static x => x.AttributeClass?.ToDisplayString() == CommandAttributeFullName);
    //    if (attribute is null)
    //    {
    //        return null;
    //    }

    //    var name = attribute.ConstructorArguments.Length > 0
    //        ? attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty
    //        : string.Empty;
    //    var description = attribute.ConstructorArguments.Length > 1
    //        ? attribute.ConstructorArguments[1].Value?.ToString()
    //        : null;

    //    return new CommandModel(name, description);
    //}

    //private static bool IsHandler(ITypeSymbol typeSymbol)
    //{
    //    foreach (var @interface in typeSymbol.AllInterfaces)
    //    {
    //        if (@interface.ToDisplayString() == CommandHandlerFullName)
    //        {
    //            return true;
    //        }
    //    }

    //    return false;
    //}

    //private static EquatableArray<string> ExtractStringArray(TypedConstant arrayConstant)
    //{
    //    if (arrayConstant is { Kind: TypedConstantKind.Array, Values.IsEmpty: false })
    //    {
    //        var result = new List<string>();

    //        foreach (var element in arrayConstant.Values)
    //        {
    //            if (element.Value is string str)
    //            {
    //                result.Add(str);
    //            }
    //        }

    //        return new EquatableArray<string>(result.ToArray());
    //    }

    //    return new EquatableArray<string>([]);
    //}

    //// ------------------------------------------------------------
    //// Generator
    //// ------------------------------------------------------------

    //private static void Execute(SourceProductionContext context, ImmutableArray<Result<InvocationModel>> invocations)
    //{
    //    foreach (var info in invocations.SelectError())
    //    {
    //        context.ReportDiagnostic(info);
    //    }

    //    var builder = new SourceBuilder();

    //    builder.AutoGenerated();
    //    builder.EnableNullable();

    //    // namespace
    //    builder.Append("namespace Smart.CommandLine.Hosting.Generated;").NewLine();
    //    builder.NewLine();

    //    // using
    //    builder.Append("using System;").NewLine();
    //    builder.Append("using System.Runtime.CompilerServices;").NewLine();
    //    builder.NewLine();
    //    builder.Append("using Smart.CommandLine.Hosting;").NewLine();
    //    builder.NewLine();

    //    // class
    //    builder.Append("internal static partial class BuilderInterceptors").NewLine();
    //    builder.BeginScope();

    //    var targetInvocations = invocations.SelectValue().ToList();
    //    for (var i = 0; i < targetInvocations.Count; i++)
    //    {
    //        var invocation = targetInvocations[i];
    //        var methodName = $"{invocation.MethodName}_Interceptor_{i}";

    //        builder.Indent().Append($"// {invocation.InterceptableLocation.GetDisplayLocation()}").NewLine();
    //        builder.Indent().Append($"//[InterceptsLocation({invocation.InterceptableLocation.Version}, @\"{invocation.InterceptableLocation.Data}\")]").NewLine();
    //        builder.Indent().Append($"internal static void {methodName}(this {invocation.ReceiverType} builder)").NewLine();
    //        builder.BeginScope();

    //        // Builder local function
    //        builder.Indent().Append("static void Builder(CommandActionBuilderContext context)").NewLine();
    //        builder.BeginScope();

    //        // Create options
    //        // TODO options
    //        builder.Indent().Append("//var option1 = new Option<string>(\"test\")").NewLine();
    //        builder.Indent().Append("//context.AddOption(option1);").NewLine();
    //        builder.NewLine();

    //        // Delegate
    //        builder.Indent().Append("context.Operation = (command, result, commandContext) =>").NewLine();
    //        builder.Indent().Append("{").NewLine();
    //        builder.IndentLevel++;

    //        builder.Indent().Append($"var instance = ({invocation.TypeArgument})commandContext.Command;").NewLine();

    //        // Set options
    //        // TODO options
    //        builder.Indent().Append("//instance.Greeting = result.GetValue(option1)!;").NewLine();
    //        builder.NewLine();

    //        builder.Indent().Append("return command.ExecuteAsync(commandContext);").NewLine();

    //        builder.IndentLevel--;
    //        builder.Indent().Append("};").NewLine();

    //        builder.EndScope();

    //        builder.NewLine();

    //        // Call with builder
    //        builder.Append($"        builder.{invocation.MethodName}<{invocation.TypeArgument}>(Builder);").NewLine();

    //        builder.EndScope();

    //        if (i < invocations.Length - 1)
    //        {
    //            builder.NewLine();
    //        }
    //    }

    //    builder.EndScope();

    //    context.AddSource("Interceptors.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    //}

    // ------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------

    // TODO
    // location
    // TypeArgument: global::Develop.TestCommand
    // TypeName: TestCommand
    // ReceiverType: global::Smart.CommandLine.Hosting.ICommandBuilder ...
    // MethodName: AddCommand, AddSubCommand, UseHandler
    //internal sealed record InvocationModel(
    //    InterceptableLocation InterceptableLocation,
    //    string TypeArgument,
    //    string TypeName,
    //    string ReceiverType,
    //    string MethodName,
    //    CommandModel CommandInfo,
    //    EquatableArray<OptionModel> Options);

    //internal sealed record CommandModel(
    //    string CommandName,
    //    string? CommandDescription);

    //internal sealed record OptionModel(
    //    string PropertyName,
    //    string PropertyType,
    //    bool IsGeneric,
    //    string? GenericTypeArgument,
    //    int Order,
    //    int HierarchyLevel,
    //    int PropertyIndex,
    //    string Name,
    //    EquatableArray<string> Aliases,
    //    string? Description,
    //    bool Required,
    //    string? CompletionsElementType, // TODO 代替？
    //    EquatableArray<string>? Completions);
}
