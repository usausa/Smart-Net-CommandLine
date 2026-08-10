namespace Smart.CommandLine.Hosting.Generator.Tests;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using Smart.CommandLine.Hosting;

using SourceGenerateHelper.Testing;

internal static class GeneratorTestHelper
{
    private const string InitializerHintName = "CommandInitializer.g.cs";

    // The generator targets a program with an entry point, so the test sources are compiled
    // as a console application (the test sources declare Program.Main).
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
            // Null when the initializer was not generated at all.
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
