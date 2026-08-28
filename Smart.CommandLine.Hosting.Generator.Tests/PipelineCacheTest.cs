namespace Smart.CommandLine.Hosting.Generator.Tests;

using SourceGenerateHelper.Testing;

public sealed class PipelineCacheTest
{
    private const string Source =
        """
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

    private const string UnrelatedSource =
        """
        namespace Other;

        internal sealed class Unrelated;
        """;

    private const string AddedTargetSource =
        """
        using Smart.CommandLine.Hosting;

        namespace TestApp;

        [Command("bar")]
        public sealed class BarCommand
        {
        }

        public static class Runner
        {
            public static void Run(string[] args)
            {
                var builder = CommandHost.CreateBuilder(args);
                builder.ConfigureCommands(static commands => commands.AddCommand<BarCommand>());
                _ = builder.Build();
            }
        }
        """;

    // ------------------------------------------------------------
    // Cache
    // ------------------------------------------------------------

    [Fact]
    public void UnrelatedEditKeepsModelCached()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental(Source, UnrelatedSource);

        // Assert
        Assert.Equal(result.FirstGeneratedText, result.SecondGeneratedText);
        Assert.NotEmpty(result.OutputReasons);
        Assert.DoesNotContain(result.OutputReasons, static x => x.IsChanged());
    }

    [Fact]
    public void TargetEditRebuildsModel()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunIncremental(Source, AddedTargetSource);

        // Assert
        Assert.Contains(result.OutputReasons, static x => x.IsChanged());
    }
}
