namespace Smart.CommandLine.Hosting;

using System.Collections.Concurrent;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

public static class CommandMetadataProvider
{
    //--------------------------------------------------------------------------------
    // Command info
    //--------------------------------------------------------------------------------

    private static readonly ConcurrentDictionary<Type, (string Name, string? Description)> CommandMetadata = new();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void AddCommandMetadata<TCommand>(string name, string? description = null)
    {
        var commandType = typeof(TCommand);
        CommandMetadata[commandType] = (name, description);
    }

    [RequiresUnreferencedCode("Reflection fallback is used when Source Generator is not applied. Use Source Generator to avoid this.")]
    internal static (string Name, string? Description) ResolveCommandMetadata(Type type)
    {
        if (CommandMetadata.TryGetValue(type, out var data))
        {
            return data;
        }

        var attribute = type.GetCustomAttribute<CommandAttribute>();
        if (attribute is null)
        {
            throw new InvalidOperationException($"Type must be annotated with the [Command] attribute to be used as a command. type=['{type.FullName}]");
        }

        return (attribute.Name, attribute.Description);
    }

    //--------------------------------------------------------------------------------
    // Filter descriptors
    //--------------------------------------------------------------------------------

    private static readonly ConcurrentDictionary<Type, List<FilterDescriptor>> FilterDescriptors = new();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void AddFilterDescriptor<TTarget, TFilter>(int order)
        where TFilter : class, ICommandFilter
    {
        var descriptors = FilterDescriptors.GetOrAdd(typeof(TTarget), static _ => []);
        lock (descriptors)
        {
            descriptors.Add(new FilterDescriptor(typeof(TFilter), order));
        }
    }

    [RequiresUnreferencedCode("Reflection fallback is used when Source Generator is not applied. Use Source Generator to avoid this.")]
    internal static IReadOnlyList<FilterDescriptor> GetFilterDescriptors(Type type) =>
        FilterDescriptors.GetOrAdd(type, BuildFilterDescriptors);

    [RequiresUnreferencedCode("Reflection fallback is used when Source Generator is not applied. Use Source Generator to avoid this.")]
    private static List<FilterDescriptor> BuildFilterDescriptors(Type type)
    {
        var descriptors = new List<FilterDescriptor>();
        foreach (var attribute in type.GetCustomAttributes(true))
        {
            var attributeType = attribute.GetType();
            if (attributeType.IsGenericType &&
                (attributeType.GetGenericTypeDefinition() == typeof(FilterAttribute<>)) &&
                (attribute is IFilterAttribute filterAttribute))
            {
                descriptors.Add(new FilterDescriptor(filterAttribute.GetFilterType(), filterAttribute.GetOrder()));
            }
        }

        return descriptors;
    }

    //--------------------------------------------------------------------------------
    // Action builder
    //--------------------------------------------------------------------------------

    private static readonly ConcurrentDictionary<Type, Action<CommandActionBuilderContext>> ActionBuilders = new();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void AddActionBuilder<TCommand>(Action<CommandActionBuilderContext> builder)
    {
        var commandType = typeof(TCommand);
        ActionBuilders[commandType] = builder;
    }

    [RequiresUnreferencedCode("Reflection fallback is used when Source Generator is not applied. Use Source Generator to avoid this.")]
    [RequiresDynamicCode("Reflection fallback uses MakeGenericType/MakeGenericMethod. Use Source Generator to avoid this.")]
    internal static Action<CommandActionBuilderContext> ResolveActionBuilder(Type type)
    {
        if (ActionBuilders.TryGetValue(type, out var builder))
        {
            return builder;
        }

        // Reflection fallback
        return CreateReflectionBasedDelegate(type);
    }

    [RequiresUnreferencedCode("Reflection fallback uses GetCustomAttribute, GetProperties, Activator.CreateInstance, etc. Use Source Generator to avoid this.")]
    [RequiresDynamicCode("Reflection fallback uses MakeGenericType/MakeGenericMethod. Use Source Generator to avoid this.")]
    private static Action<CommandActionBuilderContext> CreateReflectionBasedDelegate(Type type)
    {
        return context =>
        {
            // Resolve the generic ParseResult.GetValue<T>(Option<T>) definition once per command type.
            var getValueDefinition = ResolveParseResultGetValueMethod();

            var propertyArguments = new List<(PropertyInfo Property, Option Option, MethodInfo GetValue)>();

            // Add option
            foreach (var (property, attribute) in EnumerableTargetProperties(type))
            {
                // Create option
                var optionType = typeof(Option<>).MakeGenericType(property.PropertyType);
                var option = (Option)Activator.CreateInstance(optionType, attribute.GetName(), attribute.GetAliases())!;

                // Set description
                var description = attribute.GetDescription();
                if (description is not null)
                {
                    var descriptionProperty = optionType.GetProperty(nameof(Argument.Description));
                    descriptionProperty?.SetValue(option, description);
                }

                option.Required = attribute.GetRequired();

                // Set default value factory
                var (hasValue, value) = GetDefaultValue(property, attribute);
                if (hasValue)
                {
                    SetDefaultValueFactory(option, property.PropertyType, value);
                }

                // Set completion sources
                var completions = attribute.GetCompletions();
                if (completions.Length > 0)
                {
                    SetCompletionSources(option, completions);
                }

                // Add to context
                context.AddOption(option);

                // Pre-build the closed generic getter so it is not resolved on every execution.
                var getValue = getValueDefinition.MakeGenericMethod(property.PropertyType);
                propertyArguments.Add((property, option, getValue));
            }

            // Build operation
            context.Operation = (command, parseResult, commandContext) =>
            {
                // Set property values
                foreach (var (property, option, getValue) in propertyArguments)
                {
                    var value = getValue.Invoke(parseResult, [option]);
                    property.SetValue(command, value);
                }

                // Execute command
                return command.ExecuteAsync(commandContext);
            };
        };
    }

    [RequiresUnreferencedCode("Uses GetProperties with reflection.")]
    private static IEnumerable<(PropertyInfo Property, IOptionAttribute Attribute)> EnumerableTargetProperties(Type type)
    {
        var propertiesWithMetadata = new List<(PropertyInfo Property, IOptionAttribute Attribute, int TypeLevel, int Order, int PropertyIndex)>();

        var currentType = type;
        var currentLevel = 0;
        while ((currentType is not null) && (currentType != typeof(object)))
        {
            var properties = currentType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];

                if ((property.GetIndexParameters().Length > 0) || (property.SetMethod is not { IsPublic: true }))
                {
                    continue;
                }

                if (property.GetCustomAttribute<BaseOptionAttribute>() is IOptionAttribute attribute)
                {
                    propertiesWithMetadata.Add((property, attribute, currentLevel, attribute.GetOrder(), i));
                }
            }

            currentType = currentType.BaseType;
            currentLevel--;
        }

        propertiesWithMetadata.Sort(static (x, y) =>
        {
            var orderComparison = x.Order.CompareTo(y.Order);
            if (orderComparison != 0)
            {
                return orderComparison;
            }

            var levelComparison = x.TypeLevel.CompareTo(y.TypeLevel);
            if (levelComparison != 0)
            {
                return levelComparison;
            }

            return x.PropertyIndex.CompareTo(y.PropertyIndex);
        });

        return propertiesWithMetadata.Select(static x => (x.Property, x.Attribute));
    }

    [RequiresUnreferencedCode("Uses Activator.CreateInstance for value type default values.")]
    private static (bool HasValue, object? Value) GetDefaultValue(PropertyInfo property, IOptionAttribute attribute)
    {
        var defaultValue = attribute.GetDefaultValue();
        if (defaultValue is not null)
        {
            return (true, defaultValue);
        }

        if (!attribute.GetRequired())
        {
            defaultValue = property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;
            return (true, defaultValue);
        }

        return (false, null);
    }

    [RequiresUnreferencedCode("Uses GetProperty and MakeGenericMethod with reflection.")]
    [RequiresDynamicCode("Uses MakeGenericMethod at runtime.")]
    private static void SetDefaultValueFactory(Option option, Type propertyType, object? value)
    {
        var defaultValueFactoryProperty = option.GetType().GetProperty(nameof(Option<>.DefaultValueFactory));
        if (defaultValueFactoryProperty is null)
        {
            return;
        }

        // Create default value factory delegate
        var factoryCreateMethod = typeof(CommandMetadataProvider)
            .GetMethod(nameof(CreateDefaultValueFactory), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(propertyType);
        var factoryDelegate = factoryCreateMethod.Invoke(null, [value]);

        defaultValueFactoryProperty.SetValue(option, factoryDelegate);
    }

    private static Func<ArgumentResult, T> CreateDefaultValueFactory<T>(object? value)
    {
        return _ => (T)value!;
    }

    [RequiresUnreferencedCode("Uses GetProperty/GetMethod with reflection.")]
    private static void SetCompletionSources(Option option, string[] completions)
    {
        if (completions.Length == 0)
        {
            return;
        }

        var completionSourcesProperty = option.GetType().GetProperty("CompletionSources", BindingFlags.Public | BindingFlags.Instance);
        var completionSources = completionSourcesProperty?.GetValue(option);
        if (completionSources is null)
        {
            return;
        }

        var addMethod = completionSources.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance, null, [typeof(string[])], null);
        addMethod?.Invoke(completionSources, [completions]);
    }

    [RequiresUnreferencedCode("Uses GetMethods with reflection.")]
    private static MethodInfo ResolveParseResultGetValueMethod() =>
        typeof(ParseResult)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(static x => x is { Name: nameof(ParseResult.GetValue), IsGenericMethodDefinition: true } &&
                              (x.GetParameters().Length == 1) &&
                              x.GetParameters()[0].ParameterType.IsGenericType &&
                              (x.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Option<>)));
}
