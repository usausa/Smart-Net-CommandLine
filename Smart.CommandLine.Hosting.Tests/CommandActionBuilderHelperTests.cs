// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable UnusedMember.Local
namespace Smart.CommandLine.Hosting;

using System.CommandLine;

public sealed class CommandMetadataProviderTests
{
    private sealed class SimpleCommand : ICommandHandler
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
    private sealed class CommandWithRequired : ICommandHandler
    {
        [Option("--required", Required = true)]
        public string Required { get; set; } = default!;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
#pragma warning restore CA1812

    private sealed class CommandWithDefaultValue : ICommandHandler
    {
        [Option<int>("--count", DefaultValue = 10)]
        public int Count { get; set; }

        [Option<string>("--name", DefaultValue = "default")]
        public string Name { get; set; } = default!;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class CommandWithDescription : ICommandHandler
    {
        [Option("--verbose", Description = "Enable verbose output")]
        public bool Verbose { get; set; }

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class CommandWithAliases : ICommandHandler
    {
        [Option("--name", "-n", "--full-name")]
        public string Name { get; set; } = default!;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

#pragma warning disable CA1812
    private sealed class CommandWithMultipleOptions : ICommandHandler
    {
        [Option("--name")]
        public string Name { get; set; } = default!;

        [Option("--age")]
        public int Age { get; set; }

        [Option("--active")]
        public bool Active { get; set; }

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class CommandWithNullableType : ICommandHandler
    {
        [Option("--value")]
        public int? Value { get; set; }

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class CommandWithCompletions : ICommandHandler
    {
        [Option("--format", "-f", Completions = ["json", "xml", "yaml"])]
        public string Format { get; set; } = default!;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class CommandWithGenericCompletions : ICommandHandler
    {
        [Option<string>("--level", "-l", Completions = ["debug", "info", "warning", "error"])]
        public string Level { get; set; } = default!;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }

    private sealed class CommandWithIntCompletions : ICommandHandler
    {
        [Option<int>("--port", "-p", Completions = [80, 443, 8080, 8443])]
        public int Port { get; set; }

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
#pragma warning restore CA1812

    private sealed class CommandWithoutOptions : ICommandHandler
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
    public void ResolveActionBuilder_WithSimpleCommand_CreatesValidDelegate()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(SimpleCommand), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(SimpleCommand));
        actionBuilder(context);

        // Assert
        Assert.NotNull(context.Operation);
        Assert.Equal(2, command.Options.Count);
        Assert.Contains(command.Options, x => x.Name == "--name");
        Assert.Contains(command.Options, x => x.Name == "--value");
    }

    [Fact]
    public void ResolveActionBuilder_AddsOptionsWithCorrectNames()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(SimpleCommand), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(SimpleCommand));
        actionBuilder(context);

        // Assert
        var nameOption = command.Options.FirstOrDefault(o => o.Name == "--name");
        var valueOption = command.Options.FirstOrDefault(o => o.Name == "--value");
        Assert.NotNull(nameOption);
        Assert.NotNull(valueOption);
    }

    [Fact]
    public void ResolveActionBuilder_WithDescription_SetsDescriptionProperty()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithDescription), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithDescription));
        actionBuilder(context);

        // Assert
        var verboseOption = command.Options.FirstOrDefault(o => o.Name == "--verbose");
        Assert.NotNull(verboseOption);
        Assert.Equal("Enable verbose output", verboseOption.Description);
    }

    [Fact]
    public void ResolveActionBuilder_WithAliases_AddsAllAliases()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithAliases), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithAliases));
        actionBuilder(context);

        // Assert
        var nameOption = command.Options.FirstOrDefault(o => o.Name == "--name");
        Assert.NotNull(nameOption);
        Assert.Contains(nameOption.Aliases, x => x == "-n");
        Assert.Contains(nameOption.Aliases, x => x == "--full-name");
    }

    [Fact]
    public void ResolveActionBuilder_WithDefaultValue_SetsDefaultValueFactory()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithDefaultValue), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithDefaultValue));
        actionBuilder(context);

        // Assert
        Assert.Equal(2, command.Options.Count);
        Assert.Contains(command.Options, x => x.Name == "--count");
        Assert.Contains(command.Options, x => x.Name == "--name");
    }

    [Fact]
    public void ResolveActionBuilder_WithMultipleOptions_AddsAllOptions()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithMultipleOptions), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithMultipleOptions));
        actionBuilder(context);

        // Assert
        Assert.Equal(3, command.Options.Count);
        Assert.Contains(command.Options, x => x.Name == "--name");
        Assert.Contains(command.Options, x => x.Name == "--age");
        Assert.Contains(command.Options, x => x.Name == "--active");
    }

    [Fact]
    public void ResolveActionBuilder_WithNullableType_AddsOption()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithNullableType), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithNullableType));
        actionBuilder(context);

        // Assert
        Assert.Single(command.Options);
        Assert.Contains(command.Options, x => x.Name == "--value");
    }

    [Fact]
    public void ResolveActionBuilder_WithoutOptions_DoesNotAddOptions()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithoutOptions), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithoutOptions));
        actionBuilder(context);

        // Assert
        Assert.Empty(command.Options);
        Assert.NotNull(context.Operation);
    }

    [Fact]
    public async Task ResolveActionBuilder_Operation_ExecutesCommand()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithoutOptions), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithoutOptions));
        actionBuilder(context);

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
    public async Task ResolveActionBuilder_Operation_SetsPropertyValues()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(SimpleCommand), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(SimpleCommand));
        actionBuilder(context);

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
    public async Task ResolveActionBuilder_Operation_WithDefaultValues_UsesDefaults()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithDefaultValue), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithDefaultValue));
        actionBuilder(context);

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
    public async Task ResolveActionBuilder_Operation_WithPartialValues_UsesProvidedAndDefaults()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithDefaultValue), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithDefaultValue));
        actionBuilder(context);

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
    public async Task ResolveActionBuilder_Operation_WithAliases_ParsesCorrectly()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithAliases), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithAliases));
        actionBuilder(context);

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
    public async Task ResolveActionBuilder_Operation_WithBooleanOption_ParsesCorrectly()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithDescription), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithDescription));
        actionBuilder(context);

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
    public void ResolveActionBuilder_WithNonICommandType_CreatesDelegate()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(NonICommandType), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(NonICommandType));
        actionBuilder(context);

        // Assert
        Assert.NotNull(context.Operation);
        Assert.Single(command.Options);
    }

    [Fact]
    public void ResolveActionBuilder_WithRequiredOption_AddsOption()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithRequired), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithRequired));
        actionBuilder(context);

        // Assert
        Assert.Single(command.Options);
        Assert.Contains(command.Options, x => x.Name == "--required");
    }

    [Fact]
    public void ResolveActionBuilder_SetsOperationInContext()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(SimpleCommand), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(SimpleCommand));
        actionBuilder(context);

        // Assert
        Assert.NotNull(context.Operation);
    }

    [Fact]
    public async Task ResolveActionBuilder_Operation_WithMultipleOptions_SetsAllProperties()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithMultipleOptions), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithMultipleOptions));
        actionBuilder(context);

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
    public async Task ResolveActionBuilder_Operation_WithNullableType_SetsValue()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithNullableType), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithNullableType));
        actionBuilder(context);

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
        Assert.Equal(100, commandInstance.Value);
    }

    [Fact]
    public async Task ResolveActionBuilder_Operation_WithNullableType_WithoutValue_SetsNull()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithNullableType), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithNullableType));
        actionBuilder(context);

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

    [Fact]
    public void ResolveActionBuilder_WithCompletions_AddsOption()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithCompletions), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithCompletions));
        actionBuilder(context);

        // Assert
        Assert.Single(command.Options);
        var formatOption = command.Options.FirstOrDefault(o => o.Name == "--format");
        Assert.NotNull(formatOption);
        Assert.Contains(formatOption.Aliases, x => x == "-f");
    }

    [Fact]
    public async Task ResolveActionBuilder_Operation_WithCompletions_AcceptsAnyValue()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithCompletions), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithCompletions));
        actionBuilder(context);

        var commandInstance = new CommandWithCompletions();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test --format json");
        var commandContext = new CommandContext(typeof(CommandWithCompletions), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert
        var formatOption = (Option<string>)command.Options.First(o => o.Name == "--format");
        Assert.Equal("json", parseResult.GetValue(formatOption));
        Assert.Equal("json", commandInstance.Format);
    }

    [Fact]
    public void ResolveActionBuilder_WithGenericCompletions_AddsOption()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithGenericCompletions), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithGenericCompletions));
        actionBuilder(context);

        // Assert
        Assert.Single(command.Options);
        var levelOption = command.Options.FirstOrDefault(o => o.Name == "--level");
        Assert.NotNull(levelOption);
        Assert.Contains(levelOption.Aliases, x => x == "-l");
    }

    [Fact]
    public async Task ResolveActionBuilder_Operation_WithGenericCompletions_SetsValue()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithGenericCompletions), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithGenericCompletions));
        actionBuilder(context);

        var commandInstance = new CommandWithGenericCompletions();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test --level warning");
        var commandContext = new CommandContext(typeof(CommandWithGenericCompletions), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert
        var levelOption = (Option<string>)command.Options.First(o => o.Name == "--level");
        Assert.Equal("warning", parseResult.GetValue(levelOption));
        Assert.Equal("warning", commandInstance.Level);
    }

    [Fact]
    public void ResolveActionBuilder_WithIntCompletions_AddsOption()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithIntCompletions), command, serviceProvider);

        // Act
        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithIntCompletions));
        actionBuilder(context);

        // Assert
        Assert.Single(command.Options);
        var portOption = command.Options.FirstOrDefault(o => o.Name == "--port");
        Assert.NotNull(portOption);
        Assert.Contains(portOption.Aliases, x => x == "-p");
    }

    [Fact]
    public async Task ResolveActionBuilder_Operation_WithIntCompletions_SetsValue()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithIntCompletions), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithIntCompletions));
        actionBuilder(context);

        var commandInstance = new CommandWithIntCompletions();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test --port 8080");
        var commandContext = new CommandContext(typeof(CommandWithIntCompletions), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert
        var portOption = (Option<int>)command.Options.First(o => o.Name == "--port");
        Assert.Equal(8080, parseResult.GetValue(portOption));
        Assert.Equal(8080, commandInstance.Port);
    }

    [Fact]
    public async Task ResolveActionBuilder_Operation_WithCompletions_AcceptsValueNotInList()
    {
        // Arrange
        var serviceProvider = new TestServiceProvider();
        var command = new Command("test");
        var context = new CommandActionBuilderContext(typeof(CommandWithCompletions), command, serviceProvider);

        var actionBuilder = CommandMetadataProvider.ResolveActionBuilder(typeof(CommandWithCompletions));
        actionBuilder(context);

        var commandInstance = new CommandWithCompletions();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        var parseResult = rootCommand.Parse("test --format csv");
        var commandContext = new CommandContext(typeof(CommandWithCompletions), commandInstance, CancellationToken.None);

        // Act
        await context.Operation!(commandInstance, parseResult, commandContext);

        // Assert - Completions provide suggestions but don't validate
        var formatOption = (Option<string>)command.Options.First(o => o.Name == "--format");
        Assert.Equal("csv", parseResult.GetValue(formatOption));
        Assert.Equal("csv", commandInstance.Format);
    }
}
