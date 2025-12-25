namespace Smart.CommandLine.Hosting;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;

internal static class CommandActionBuilderHelper
{
    private static readonly MethodInfo GetValueMethod = typeof(ParseResult)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .First(x => x is { Name: nameof(ParseResult.GetValue), IsGenericMethodDefinition: true } &&
                    (x.GetParameters().Length == 1) &&
                    x.GetParameters()[0].ParameterType.IsGenericType &&
                    x.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Option<>));

    public static Action<CommandActionBuilderContext> CreateReflectionBasedDelegate(Type type)
    {
        return context =>
        {
            var propertyArguments = new List<(PropertyInfo, Option)>();

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
                var defaultValue = GetDefaultValue(property, attribute);
                if (defaultValue.HasValue)
                {
                    SetDefaultValueFactory(option, property.PropertyType, defaultValue.Value);
                }

                // Set completion sources
                var completions = attribute.GetCompletions();
                if (completions.Length > 0)
                {
                    SetCompletionSources(option, completions);
                }

                // Add to context
                context.AddOption(option);

                propertyArguments.Add((property, option));
            }

            // Build operation
            context.Operation = (command, parseResult, commandContext) =>
            {
                // Set property values
                foreach (var (property, option) in propertyArguments)
                {
                    SetOptionValue(command, parseResult, property, option);
                }

                // Execute command
                return command.ExecuteAsync(commandContext);
            };
        };
    }

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

    private static void SetDefaultValueFactory(Option option, Type propertyType, object? value)
    {
        var defaultValueFactoryProperty = option.GetType().GetProperty(nameof(Option<>.DefaultValueFactory));
        if (defaultValueFactoryProperty is null)
        {
            return;
        }

        // Create default value factory delegate
        var factoryCreateMethod = typeof(CommandActionBuilderHelper)
            .GetMethod(nameof(CreateDefaultValueFactory), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(propertyType);
        var factoryDelegate = factoryCreateMethod.Invoke(null, [value]);

        defaultValueFactoryProperty.SetValue(option, factoryDelegate);
    }

    private static Func<ArgumentResult, T> CreateDefaultValueFactory<T>(object? value)
    {
        return _ => (T)value!;
    }

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

    private static void SetOptionValue(ICommandHandler handler, ParseResult parseResult, PropertyInfo property, Option option)
    {
        var genericMethod = GetValueMethod.MakeGenericMethod(property.PropertyType);

        // Invoke and set value
        var value = genericMethod.Invoke(parseResult, [option]);
        property.SetValue(handler, value);
    }
}
