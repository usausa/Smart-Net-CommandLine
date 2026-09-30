namespace Smart.CommandLine.Hosting.Generator.Tests;

public class OptionDeclarationTests
{
    private const string Program =
        """

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

    [Fact]
    public void OptionOfHiddenBasePropertyIsBoundThroughCast()
    {
        // Arrange
        const string source =
            """
            #nullable enable
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            public abstract class FooCommandBase
            {
                [Option<string>("--level")]
                public string Level { get; set; } = string.Empty;
            }

            [Command("foo", "Foo command")]
            public sealed class FooCommand : FooCommandBase, ICommandHandler
            {
                [Option<int>("--level-value")]
                public new int Level { get; set; }

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }
            """ + Program;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.Empty(GeneratorTestHelper.GetProblemIds(source));
        Assert.Contains("\"--level-value\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("\"--level\"", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("((global::TestApp.FooCommandBase)target).Level = ", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[Option<LogLevel>(\"--level\", DefaultValue = (LogLevel)99)]", "public LogLevel Level { get; set; }", "(global::TestApp.LogLevel)(99)")]
    [InlineData("[Option<int>(\"--size\", DefaultValue = 1)]", "public long Size { get; set; }", "static _ => 1;")]
    [InlineData("[Option<string>(\"--name\", DefaultValue = null)]", "public string? Name { get; set; }", "static _ => null;")]
    public void DefaultValueIsWrittenForPropertyType(string attribute, string property, string expected)
    {
        // Arrange
        var source = $$"""
            #nullable enable
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            public enum LogLevel
            {
                Info,
                Debug
            }

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                {{attribute}}
                {{property}}

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }
            """ + Program;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.Empty(GeneratorTestHelper.GetProblemIds(source));
        Assert.Contains(expected, result.GeneratedSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[Option(\"--format\", Completions = null)]")]
    [InlineData("[Option(\"--format\", null)]")]
    public void NullArraysDoNotStopGeneration(string attribute)
    {
        // Arrange
        var source = $$"""
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                {{attribute}}
                public string Format { get; set; } = default!;

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }
            """ + Program;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.NotNull(result.GeneratedSource);
        Assert.DoesNotContain("CS8785", GeneratorTestHelper.GetProblemIds(source));
    }

    [Fact]
    public void KeywordPropertyNameIsEscaped()
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
                [Option<string>("--event")]
                public string @event { get; set; } = default!;

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }
            """ + Program;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.Empty(GeneratorTestHelper.GetProblemIds(source));
        Assert.Contains("target.@event = ", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public string Name { get; init; } = string.Empty;")]
    [InlineData("public string Name { get; private set; } = string.Empty;")]
    [InlineData("public string Name => string.Empty;")]
    public void Scl0001OptionThatCannotBeWrittenEmitsDiagnostic(string property)
    {
        // Arrange
        var source = $$"""
            #nullable enable
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                [Option<string>("--name")]
                {{property}}

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }
            """ + Program;

        // Act
        var enabled = GeneratorTestHelper.RunGenerator(source);
        var disabled = GeneratorTestHelper.RunGenerator(source, new Dictionary<string, string> { ["build_property.EnableSmartCommandLineHostingGenerator"] = "false" });

        // Assert
        Assert.Contains(enabled.GeneratorDiagnostics, static x => x.Id == "SCL0001");
        Assert.Contains(disabled.GeneratorDiagnostics, static x => x.Id == "SCL0001");
        Assert.DoesNotContain("\"--name\"", enabled.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Scl0002CommandThatCannotBeReferredToEmitsDiagnostic()
    {
        // Arrange
        const string source =
            """
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            public static class Program
            {
                [Command("foo", "Foo command")]
                private sealed class FooCommand : ICommandHandler
                {
                    public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
                }

                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            }
            """;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);

        // Assert
        Assert.Equal(["SCL0002"], problems);
    }

    [Fact]
    public void Scl0003DefaultValueThatDoesNotConvertEmitsDiagnostic()
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
                [Option<string>("--size", DefaultValue = "abc")]
                public int Size { get; set; }

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }
            """ + Program;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);

        // Assert
        Assert.Equal(["SCL0003"], problems);
    }

    [Fact]
    public void OptionWithoutResultKeepsPropertyValue()
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
                [Option<string>("--name")]
                public string Name { get; set; } = "initial";

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }
            """ + Program;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.Contains("if (result.GetResult(option0) is not null)", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ObsoleteCommandCompilesWithoutWarning()
    {
        // Arrange
        const string source =
            """
            using System;
            using System.Threading.Tasks;
            using Smart.CommandLine.Hosting;

            namespace TestApp;

            [Obsolete]
            [Command("foo", "Foo command")]
            public sealed class FooCommand : ICommandHandler
            {
                [Obsolete]
                [Option<string>("--name")]
                public string Name { get; set; } = string.Empty;

                public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
            }

            public static class Program
            {
            #pragma warning disable CS0612
                public static void Main(string[] args)
                {
                    var builder = CommandHost.CreateBuilder(args);
                    builder.ConfigureCommands(static commands => commands.AddCommand<FooCommand>());
                    _ = builder.Build();
                }
            #pragma warning restore CS0612
            }
            """;

        // Act
        var problems = GeneratorTestHelper.GetProblemIds(source);

        // Assert
        Assert.Empty(problems);
    }
}
