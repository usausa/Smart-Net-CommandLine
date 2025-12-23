namespace Smart.CommandLine.Hosting;

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;

internal static class CommandActionBuilderHelper
{
    public static Action<CommandActionBuilderContext> CreateReflectionBasedDelegate(Type type)
    {
        return context =>
        {
            var propertyArguments = new List<(PropertyInfo, Option)>();

            // Add option
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Get option attribute
                if (property.GetCustomAttribute<BaseOptionAttribute>() is not IOptionAttribute attribute)
                {
                    continue;
                }

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

                // Set default value factory
                var defaultValue = GetDefaultValue(property, attribute);
                if (defaultValue.HasValue)
                {
                    SetDefaultValueFactory(option, property.PropertyType, defaultValue.Value);
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

    private static (bool HasValue, object? Value) GetDefaultValue(PropertyInfo property, IOptionAttribute attribute)
    {
        var defaultValue = attribute.GetDefaultValue();
        if (defaultValue is not null)
        {
            return (true, defaultValue);
        }

        if (!attribute.GetIsRequired())
        {
            defaultValue = property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;
            return (true, defaultValue);
        }

        return (false, null);
    }

    private static void SetDefaultValueFactory(Option option, Type propertyType, object? value)
    {
        var defaultValueFactoryProperty = option.GetType().GetProperty("DefaultValueFactory");
        if (defaultValueFactoryProperty is null)
        {
            return;
        }

        // Create default value factory delegate
        var method = typeof(CommandActionBuilderHelper)
            .GetMethod(nameof(CreateDefaultValueFactory), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(propertyType);
        var factoryDelegate = method.Invoke(null, [value]);

        defaultValueFactoryProperty.SetValue(option, factoryDelegate);
    }

    private static Func<ArgumentResult, T> CreateDefaultValueFactory<T>(object? value)
    {
        return _ => (T)value!;
    }

    private static void SetOptionValue(ICommand command, ParseResult parseResult, PropertyInfo property, Option option)
    {
        var getValueMethod = typeof(ParseResult).GetMethod(nameof(ParseResult.GetValue), [option.GetType()]);
        if (getValueMethod is null)
        {
            return;
        }

        var value = getValueMethod.Invoke(parseResult, [option]);
        property.SetValue(command, value);
    }
}
