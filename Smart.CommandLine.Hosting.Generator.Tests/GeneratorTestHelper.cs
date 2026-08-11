namespace Smart.CommandLine.Hosting.Generator.Tests;

using System.Collections.Generic;

using Microsoft.CodeAnalysis;

using Smart.CommandLine.Hosting;

using SourceGenerateHelper.Testing;

internal static class GeneratorTestHelper
{
    private const string InitializerHintName = "CommandInitializer.g.cs";

    private static GeneratorTestRunner CreateRunner(IReadOnlyDictionary<string, string>? globalOptions)
    {
        var runner = GeneratorTestRunner
            .For<CommandGenerator>()
            .WithReference(typeof(CommandAttribute).Assembly)
            .WithOutputKind(OutputKind.ConsoleApplication);

        if (globalOptions is not null)
        {
            foreach (var option in globalOptions)
            {
                runner.WithGlobalOption(option.Key, option.Value);
            }
        }

        return runner;
    }

    public static GeneratorResult RunGenerator(
        string source,
        IReadOnlyDictionary<string, string>? globalOptions = null)
    {
        var result = CreateRunner(globalOptions).Run(source);

        return new GeneratorResult
        {
            GeneratedSource = result.FindGeneratedSource(InitializerHintName),
            GeneratorDiagnostics = result.GeneratorDiagnostics,
            CompilationErrors = result.CompilationErrors
        };
    }

    internal sealed class GeneratorResult
    {
        public string? GeneratedSource { get; init; }

        public IReadOnlyList<Diagnostic> GeneratorDiagnostics { get; init; } = [];

        public IReadOnlyList<Diagnostic> CompilationErrors { get; init; } = [];
    }
}
