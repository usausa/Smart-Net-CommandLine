namespace Smart.CommandLine.Hosting.Generator.Tests;

public class CommandGeneratorTests
{
    // Minimal source template for a simple command without handler
    private const string MinimalNoHandlerSource =
        """
        using System.Threading.Tasks;
        using Smart.CommandLine.Hosting;

        namespace TestApp;

        [Command("foo")]
        public sealed class FooCommand
        {
        }

        public static class Program
        {
            public static void Main(string[] args)
            {
                var builder = CommandHost.CreateBuilder(args);
                builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                _ = builder.Build();
            }
        }
        """;

    //--------------------------------------------------------------------------------
    // Scaffolding
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateScaffoldingContainsInternalStaticClass()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("internal static class CommandInitializer", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }

    //--------------------------------------------------------------------------------
    // Command metadata
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateCommandWithNameOnlyEmitsMetadataWithoutDescription()
    {
        // Act
        var result = GeneratorTestHelper.RunGenerator(MinimalNoHandlerSource);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("""AddCommandMetadata<global::TestApp.FooCommand>("foo");""", result.GeneratedSource, StringComparison.Ordinal);
        // No description argument
        Assert.DoesNotContain("""AddCommandMetadata<global::TestApp.FooCommand>("foo",""", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateCommandWithNameAndDescriptionEmitsBothArgs()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("""AddCommandMetadata<global::TestApp.FooCommand>("foo", "Foo command");""", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }

    //--------------------------------------------------------------------------------
    // Handler and ActionBuilder
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateHandlerTypeEmitsAddActionBuilder()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("AddActionBuilder<global::TestApp.FooCommand>(static context =>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("return command.ExecuteAsync(commandContext);", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateNonHandlerTypeNoAddActionBuilder()
    {
        // Act
        var result = GeneratorTestHelper.RunGenerator(MinimalNoHandlerSource);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain("AddActionBuilder<global::TestApp.FooCommand>", result.GeneratedSource, StringComparison.Ordinal);
    }

    //--------------------------------------------------------------------------------
    // Option emission
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateOptionWithNameAliasDescriptionRequiredEmitsAllProperties()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                [Option<string>("--text", "-t", Description = "Text", Required = true)]
                public string Text { get; set; } = default!;

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        // Name + alias constructor args (property type renders as the C# keyword)
        Assert.Contains("""new global::System.CommandLine.Option<string>("--text", "-t");""", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains(""".Description = "Text";""", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains(".Required = true;", result.GeneratedSource, StringComparison.Ordinal);
        // Target assignment
        Assert.Contains("var target = (global::TestApp.FooCommand)commandContext.Command;", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("target.Text = result.GetValue(option0)!;", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("context.AddOption(option0);", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }

    [Fact]
    public void GenerateOptionStringDefaultValueEmitsDefaultValueFactory()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                [Option<string>("--text", DefaultValue = "Hello")]
                public string Text { get; set; } = default!;

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        // The actual rendering — dump via probe test first, assert the real string
        Assert.Contains("DefaultValueFactory = static _ => ", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("\"Hello\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.CompilationErrors);
    }

    [Fact]
    public void GenerateOptionIntDefaultValueEmitsDefaultValueFactory()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                [Option<int>("--count", DefaultValue = 1)]
                public int Count { get; set; }

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("DefaultValueFactory = static _ => ", result.GeneratedSource, StringComparison.Ordinal);
        // Int type argument rendering
        Assert.Contains("Option<int>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.CompilationErrors);
    }

    [Fact]
    public void GenerateOptionCompletionsEmitsCompletionSources()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                [Option("--format", "-f", Completions = ["json", "xml"])]
                public string Format { get; set; } = default!;

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("CompletionSourceExtensions.Add(option0.CompletionSources", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("\"json\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("\"xml\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.CompilationErrors);
    }

    //--------------------------------------------------------------------------------
    // Option ordering
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateOptionsWithExplicitOrderSortsCorrectly()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                [Option<string>(1, "--b")]
                public string B { get; set; } = default!;

                [Option<string>(0, "--a")]
                public string A { get; set; } = default!;

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        // "--a" must come before "--b" (option0 = --a, option1 = --b)
        var idxA = result.GeneratedSource.IndexOf("\"--a\"", StringComparison.Ordinal);
        var idxB = result.GeneratedSource.IndexOf("\"--b\"", StringComparison.Ordinal);
        Assert.True(idxA < idxB, "option --a (order=0) should appear before --b (order=1)");
        Assert.Empty(result.CompilationErrors);
    }

    [Fact]
    public void GenerateInheritedOptionsBasePropertyComesFirst()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            public abstract class BaseCommand : ICommandHandler
            {
                [Option<string>("--base-opt")]
                public string BaseOpt { get; set; } = default!;

                public abstract ValueTask ExecuteAsync(CommandContext context);
            }

            [Command("foo", "Foo command")]
            public sealed class FooCommand : BaseCommand
            {
                [Option<string>("--derived-opt")]
                public string DerivedOpt { get; set; } = default!;

                public override ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        // Base property (--base-opt) sorts earlier (lower HierarchyLevel) than derived (--derived-opt)
        var idxBase = result.GeneratedSource.IndexOf("\"--base-opt\"", StringComparison.Ordinal);
        var idxDerived = result.GeneratedSource.IndexOf("\"--derived-opt\"", StringComparison.Ordinal);
        Assert.True(idxBase < idxDerived, "base class option should appear before derived class option");
        Assert.Empty(result.CompilationErrors);
    }

    //--------------------------------------------------------------------------------
    // Filter
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateFilterAttributeEmitsAddFilterDescriptor()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            public sealed class LoggingFilter : ICommandFilter
            {
                public int Order => 0;

                public ValueTask ExecuteAsync(CommandContext context, CommandDelegate next) => next(context);
            }

            [Command("foo", "Foo command")]
            [Filter<LoggingFilter>]
            public sealed class FooCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("AddFilterDescriptor<global::TestApp.FooCommand, global::TestApp.LoggingFilter>(0);", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }

    [Fact]
    public void GenerateFilterAttributeWithOrderEmitsCorrectOrder()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            public sealed class LoggingFilter : ICommandFilter
            {
                public int Order => 0;

                public ValueTask ExecuteAsync(CommandContext context, CommandDelegate next) => next(context);
            }

            [Command("foo", "Foo command")]
            [Filter<LoggingFilter>(Order = 10)]
            public sealed class FooCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("AddFilterDescriptor<global::TestApp.FooCommand, global::TestApp.LoggingFilter>(10);", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.CompilationErrors);
    }

    //--------------------------------------------------------------------------------
    // SubCommand
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateSubCommandEmitsRegistrationsForBothTypes()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("parent", "Parent command")]
            public sealed class ParentCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            [Command("child", "Child command")]
            public sealed class ChildCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands =>
                        commands.AddCommand<ParentCommand>(static c => c.AddSubCommand<ChildCommand>()));
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("global::TestApp.ParentCommand", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("global::TestApp.ChildCommand", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }

    //--------------------------------------------------------------------------------
    // Root handler tests
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateRootHandlerEmitsActionBuilderWithoutCommandMetadata()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            public sealed class RootHandler : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands =>
                        commands.ConfigureRootCommand(static root => root.UseHandler<RootHandler>()));
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("AddActionBuilder<global::TestApp.RootHandler>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AddCommandMetadata<global::TestApp.RootHandler>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }

    //--------------------------------------------------------------------------------
    // Negative
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateCommandWithoutCommandAttributeReturnsNull()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            public sealed class NotACommand
            {
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<NotACommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.Null(result.GeneratedSource);
    }

    [Fact]
    public void GenerateNoBuilderCallsReturnsNull()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.Null(result.GeneratedSource);
    }

    //--------------------------------------------------------------------------------
    // Setting
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateSettingDisabledReturnsNull()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        var options = new Dictionary<string, string>
        {
            ["build_property.EnableSmartCommandLineHostingGenerator"] = "false"
        };

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, options);

        // Assert
        Assert.Null(result.GeneratedSource);
    }

    [Fact]
    public void GenerateSettingEnabledReturnsSource()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        var options = new Dictionary<string, string>
        {
            ["build_property.EnableSmartCommandLineHostingGenerator"] = "true"
        };

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, options);

        // Assert
        Assert.NotNull(result.GeneratedSource);
    }

    [Fact]
    public void GenerateSettingInvalidValueReturnsNull()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        var options = new Dictionary<string, string>
        {
            ["build_property.EnableSmartCommandLineHostingGenerator"] = "abc"
        };

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, options);

        // Assert
        Assert.Null(result.GeneratedSource);
    }

    //--------------------------------------------------------------------------------
    // String escaping
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateEscapesSpecialCharactersSoOutputCompiles()
    {
        // Arrange - command/option strings contain quotes, backslashes and a newline
        const string source =
            """"
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("say", "desc with \"quote\", backslash \\ and\nnewline")]
            public sealed class SayCommand : ICommandHandler
            {
                [Option<string>("--path", "-p", Description = "path \"a\\b\"", Completions = ["a\"b", "c\\d"])]
                public string Path { get; set; } = default!;

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<SayCommand>());
                    _ = builder.Build();
                }
            }
            """";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - the generated code must be valid (previously the raw values broke the literals)
        Assert.NotNull(result.GeneratedSource);
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }

    //--------------------------------------------------------------------------------
    // Property extraction condition
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateIgnoresNonBindablePropertiesWithOption()
    {
        // Arrange - static/private/get-only/init/indexer options must be ignored (like the runtime)
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                [Option<string>("--static-opt")]
                public static string StaticProp { get; set; } = default!;

                [Option<string>("--private-opt")]
                private string PrivateProp { get; set; } = default!;

                [Option<string>("--getonly-opt")]
                public string GetOnlyProp { get; } = default!;

                [Option<string>("--init-opt")]
                public string InitProp { get; init; } = default!;

                [Option<string>("--indexer-opt")]
                public string this[int index] { get => string.Empty; set { } }

                [Option<string>("--normal-opt")]
                public string NormalProp { get; set; } = default!;

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - only the normal property is bound and the output compiles
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("\"--normal-opt\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"--static-opt\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"--private-opt\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"--getonly-opt\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"--init-opt\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"--indexer-opt\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Empty(result.CompilationErrors);
    }

    //--------------------------------------------------------------------------------
    // Duplicate registration
    //--------------------------------------------------------------------------------

    [Fact]
    public void GenerateDuplicateRegistrationEmitsFilterDescriptorOnce()
    {
        // Arrange - the same command type is registered from two call sites
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            public sealed class LoggingFilter : ICommandFilter
            {
                public ValueTask ExecuteAsync(CommandContext context, CommandDelegate next) => next(context);
            }

            [Command("foo", "Foo command")]
            [Filter<LoggingFilter>]
            public sealed class FooCommand : ICommandHandler
            {
                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert - the filter descriptor is registered only once despite the duplicate call sites
        Assert.NotNull(result.GeneratedSource);
        var occurrences = CountOccurrences(
            result.GeneratedSource!,
            "AddFilterDescriptor<global::TestApp.FooCommand, global::TestApp.LoggingFilter>(0);");
        Assert.Equal(1, occurrences);
        Assert.Empty(result.CompilationErrors);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
