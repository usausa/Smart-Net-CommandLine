// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable UnusedMember.Local
namespace Smart.CommandLine.Hosting;

using System.CommandLine;

public sealed class CommandActionBuilderHelperTests
{
    private sealed class SimpleCommand : ICommand
    {
        [Option("--name")]
        public string Name { get; set; } = default!;

        [Option("--value")]
        public int Value { get; set; }

        public bool Executed { get; private set; }

        public ValueTask ExecuteAsync(CommandContext context)
        {
            Executed = true;
            return ValueTask.CompletedTask;
        }
    }

#pragma warning disable CA1812
    private sealed class CommandWithRequired : ICommand
    {
        [Option("--required", IsRequired = true)]
        public string Required { get; set; } = default!;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
#pragma warning restore CA1812

    private sealed class CommandWithDefaultValue : ICommand
    {
        [Option<int>("--count", DefaultValue = 10)]
        public int Count { get; set; }

        [Option<string>("--name", DefaultValue = "default")]
        public string Name { get; set; } = default!;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class CommandWithDescription : ICommand
    {
        [Option("--verbose", Description = "Enable verbose output")]
        public bool Verbose { get; set; }

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class CommandWithAliases : ICommand
    {
        [Option("--name", "-n", "--full-name")]
        public string Name { get; set; } = default!;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

#pragma warning disable CA1812
    private sealed class CommandWithMultipleOptions : ICommand
    {
        [Option("--name")]
        public string Name { get; set; } = default!;

        [Option("--age")]
        public int Age { get; set; }

        [Option("--active")]
        public bool Active { get; set; }

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class CommandWithNullableType : ICommand
    {
        [Option("--value")]
        public int? Value { get; set; }

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
#pragma warning restore CA1812

    private sealed class CommandWithoutOptions : ICommand
    {
        public bool Executed { get; private set; }

        public ValueTask ExecuteAsync(CommandContext context)
        {
            Executed = true;
            return ValueTask.CompletedTask;
        }
    }

#pragma warning disable CA1812
    private sealed class NonICommandType
    {
        [Option("--name")]
        public string Name { get; set; } = default!;
    }
#pragma warning restore CA1812

    //--------------------------------------------------------------------------------
    // Test
    //--------------------------------------------------------------------------------

    [Fact]
    public void CreateReflectionBasedDelegate_WithSimpleCommand_CreatesValidDelegate()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(SimpleCommand), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(SimpleCommand));
        builderDelegate(context);

        // Assert
        Assert.NotNull(context.Operation);
        Assert.Equal(2, command.Options.Count);
        Assert.Contains(command.Options, x => x.Name == "--name");
        Assert.Contains(command.Options, x => x.Name == "--value");
    }

    [Fact]
    public void CreateReflectionBasedDelegate_AddsOptionsWithCorrectNames()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(SimpleCommand), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(SimpleCommand));
        builderDelegate(context);

        // Assert
        var nameOption = command.Options.FirstOrDefault(o => o.Name == "--name");
        var valueOption = command.Options.FirstOrDefault(o => o.Name == "--value");
        Assert.NotNull(nameOption);
        Assert.NotNull(valueOption);
    }

    [Fact]
    public void CreateReflectionBasedDelegate_WithDescription_SetsDescriptionProperty()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithDescription), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithDescription));
        builderDelegate(context);

        // Assert
        var verboseOption = command.Options.FirstOrDefault(o => o.Name == "--verbose");
        Assert.NotNull(verboseOption);
        Assert.Equal("Enable verbose output", verboseOption.Description);
    }

    [Fact]
    public void CreateReflectionBasedDelegate_WithAliases_AddsAllAliases()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithAliases), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithAliases));
        builderDelegate(context);

        // Assert
        var nameOption = command.Options.FirstOrDefault(o => o.Name == "--name");
        Assert.NotNull(nameOption);
        Assert.Contains(nameOption.Aliases, x => x == "-n");
        Assert.Contains(nameOption.Aliases, x => x == "--full-name");
    }

    [Fact]
    public void CreateReflectionBasedDelegate_WithDefaultValue_SetsDefaultValueFactory()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithDefaultValue), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithDefaultValue));
        builderDelegate(context);

        // Assert
        Assert.Equal(2, command.Options.Count);
        Assert.Contains(command.Options, x => x.Name == "--count");
        Assert.Contains(command.Options, x => x.Name == "--name");
    }

    [Fact]
    public void CreateReflectionBasedDelegate_WithMultipleOptions_AddsAllOptions()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithMultipleOptions), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithMultipleOptions));
        builderDelegate(context);

        // Assert
        Assert.Equal(3, command.Options.Count);
        Assert.Contains(command.Options, x => x.Name == "--name");
        Assert.Contains(command.Options, x => x.Name == "--age");
        Assert.Contains(command.Options, x => x.Name == "--active");
    }

    [Fact]
    public void CreateReflectionBasedDelegate_WithNullableType_AddsOption()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithNullableType), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithNullableType));
        builderDelegate(context);

        // Assert
        Assert.Single(command.Options);
        Assert.Contains(command.Options, x => x.Name == "--value");
    }

    [Fact]
    public void CreateReflectionBasedDelegate_WithoutOptions_DoesNotAddOptions()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithoutOptions), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithoutOptions));
        builderDelegate(context);

        // Assert
        Assert.Empty(command.Options);
        Assert.NotNull(context.Operation);
    }

    [Fact]
    public async Task CreateReflectionBasedDelegate_Operation_ExecutesCommand()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithoutOptions), command, serviceProvider);

        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithoutOptions));
        builderDelegate(context);

        var commandInstance = new CommandWithoutOptions();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse(string.Empty);
        var commandContext = new CommandContext(typeof(CommandWithoutOptions), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert
        Assert.True(commandInstance.Executed);
    }

    [Fact]
    public async Task CreateReflectionBasedDelegate_Operation_SetsPropertyValues()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(SimpleCommand), command, serviceProvider);

        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(SimpleCommand));
        builderDelegate(context);

        var commandInstance = new SimpleCommand();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test --name TestName --value 42");
        var commandContext = new CommandContext(typeof(SimpleCommand), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert - Verify ParseResult contains the values
        var nameOption = (Option<string>)command.Options.First(o => o.Name == "--name");
        var valueOption = (Option<int>)command.Options.First(o => o.Name == "--value");
        Assert.Equal("TestName", parseResult.GetValue(nameOption));
        Assert.Equal(42, parseResult.GetValue(valueOption));

        // Assert - Verify properties are set correctly
        Assert.Equal("TestName", commandInstance.Name);
        Assert.Equal(42, commandInstance.Value);
        Assert.True(commandInstance.Executed);
    }

    [Fact]
    public async Task CreateReflectionBasedDelegate_Operation_WithDefaultValues_UsesDefaults()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithDefaultValue), command, serviceProvider);

        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithDefaultValue));
        builderDelegate(context);

        var commandInstance = new CommandWithDefaultValue();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test");
        var commandContext = new CommandContext(typeof(CommandWithDefaultValue), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert - Verify ParseResult has default values
        var countOption = (Option<int>)command.Options.First(o => o.Name == "--count");
        var nameOption = (Option<string>)command.Options.First(o => o.Name == "--name");
        Assert.Equal(10, parseResult.GetValue(countOption));
        Assert.Equal("default", parseResult.GetValue(nameOption));

        // Assert - Verify properties are set with defaults
        Assert.Equal(10, commandInstance.Count);
        Assert.Equal("default", commandInstance.Name);
    }

    [Fact]
    public async Task CreateReflectionBasedDelegate_Operation_WithPartialValues_UsesProvidedAndDefaults()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithDefaultValue), command, serviceProvider);

        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithDefaultValue));
        builderDelegate(context);

        var commandInstance = new CommandWithDefaultValue();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test --count 20");
        var commandContext = new CommandContext(typeof(CommandWithDefaultValue), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert - Verify ParseResult has provided and default values
        var countOption = (Option<int>)command.Options.First(o => o.Name == "--count");
        var nameOption = (Option<string>)command.Options.First(o => o.Name == "--name");
        Assert.Equal(20, parseResult.GetValue(countOption));
        Assert.Equal("default", parseResult.GetValue(nameOption));

        // Assert - Verify properties are set correctly
        Assert.Equal(20, commandInstance.Count);
        Assert.Equal("default", commandInstance.Name);
    }

    [Fact]
    public async Task CreateReflectionBasedDelegate_Operation_WithAliases_ParsesCorrectly()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithAliases), command, serviceProvider);

        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithAliases));
        builderDelegate(context);

        var commandInstance = new CommandWithAliases();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test -n ShortName");
        var commandContext = new CommandContext(typeof(CommandWithAliases), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert - Verify ParseResult has the value
        var nameOption = (Option<string>)command.Options.First(o => o.Name == "--name");
        Assert.Equal("ShortName", parseResult.GetValue(nameOption));

        // Assert - Verify property is set via alias
        Assert.Equal("ShortName", commandInstance.Name);
    }

    [Fact]
    public async Task CreateReflectionBasedDelegate_Operation_WithBooleanOption_ParsesCorrectly()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithDescription), command, serviceProvider);

        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithDescription));
        builderDelegate(context);

        var commandInstance = new CommandWithDescription();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test --verbose");
        var commandContext = new CommandContext(typeof(CommandWithDescription), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert - Verify ParseResult has the boolean value
        var verboseOption = (Option<bool>)command.Options.First(o => o.Name == "--verbose");
        Assert.True(parseResult.GetValue(verboseOption));

        // Assert - Verify boolean property is set
        Assert.True(commandInstance.Verbose);
    }

    [Fact]
    public void CreateReflectionBasedDelegate_WithNonICommandType_CreatesDelegate()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(NonICommandType), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(NonICommandType));
        builderDelegate(context);

        // Assert
        Assert.NotNull(context.Operation);
        Assert.Single(command.Options);
    }

    [Fact]
    public void CreateReflectionBasedDelegate_WithRequiredOption_AddsOption()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithRequired), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithRequired));
        builderDelegate(context);

        // Assert
        Assert.Single(command.Options);
        Assert.Contains(command.Options, x => x.Name == "--required");
    }

    [Fact]
    public void CreateReflectionBasedDelegate_SetsOperationInContext()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(SimpleCommand), command, serviceProvider);

        // Act
        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(SimpleCommand));
        builderDelegate(context);

        // Assert
        Assert.NotNull(context.Operation);
    }

    [Fact]
    public async Task CreateReflectionBasedDelegate_Operation_WithMultipleOptions_SetsAllProperties()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithMultipleOptions), command, serviceProvider);

        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithMultipleOptions));
        builderDelegate(context);

        var commandInstance = new CommandWithMultipleOptions();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test --name John --age 30 --active");
        var commandContext = new CommandContext(typeof(CommandWithMultipleOptions), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert - Verify ParseResult has all values
        var nameOption = (Option<string>)command.Options.First(o => o.Name == "--name");
        var ageOption = (Option<int>)command.Options.First(o => o.Name == "--age");
        var activeOption = (Option<bool>)command.Options.First(o => o.Name == "--active");
        Assert.Equal("John", parseResult.GetValue(nameOption));
        Assert.Equal(30, parseResult.GetValue(ageOption));
        Assert.True(parseResult.GetValue(activeOption));

        // Assert - Verify all properties are set
        Assert.Equal("John", commandInstance.Name);
        Assert.Equal(30, commandInstance.Age);
        Assert.True(commandInstance.Active);
    }

    [Fact]
    public async Task CreateReflectionBasedDelegate_Operation_WithNullableType_SetsValue()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithNullableType), command, serviceProvider);

        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithNullableType));
        builderDelegate(context);

        var commandInstance = new CommandWithNullableType();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test --value 100");
        var commandContext = new CommandContext(typeof(CommandWithNullableType), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert - Verify ParseResult has the nullable value
        var valueOption = (Option<int?>)command.Options.First(o => o.Name == "--value");
        Assert.Equal(100, parseResult.GetValue(valueOption));

        // Assert - Verify nullable property is set
        Assert.Equal(100, commandInstance.Value);
    }

    [Fact]
    public async Task CreateReflectionBasedDelegate_Operation_WithNullableType_WithoutValue_SetsNull()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithNullableType), command, serviceProvider);

        var builderDelegate = CommandActionBuilderHelper.CreateReflectionBasedDelegate(typeof(CommandWithNullableType));
        builderDelegate(context);

        var commandInstance = new CommandWithNullableType();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test");
        var commandContext = new CommandContext(typeof(CommandWithNullableType), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert - Verify ParseResult has null for nullable value
        var valueOption = (Option<int?>)command.Options.First(o => o.Name == "--value");
        Assert.Null(parseResult.GetValue(valueOption));

        // Assert - Verify nullable property remains null
        Assert.Null(commandInstance.Value);
    }
}
