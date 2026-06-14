namespace Smart.CommandLine.Hosting.Generator.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

internal sealed class GeneratorResult
{
    public string? GeneratedSource { get; init; }

    public IReadOnlyList<Diagnostic> GeneratorDiagnostics { get; init; } = [];

    public IReadOnlyList<Diagnostic> CompilationErrors { get; init; } = [];
}

internal static class GeneratorTestHelper
{
    private static readonly Assembly SmartCommandLineAssembly = typeof(CommandAttribute).Assembly;

    private static readonly Assembly SystemCommandLineAssembly = typeof(System.CommandLine.RootCommand).Assembly;

    private static readonly Lazy<bool> EnsureDeps = new(() =>
    {
        var dir = Path.GetDirectoryName(typeof(GeneratorTestHelper).Assembly.Location)!;
        var helper = Path.Combine(dir, "SourceGenerateHelper.dll");
        if (File.Exists(helper))
        {
            Assembly.LoadFrom(helper);
        }
        return true;
    });

    public static GeneratorResult RunGenerator(
        string source,
        IReadOnlyDictionary<string, string>? globalOptions = null)
    {
        _ = EnsureDeps.Value;
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(SmartCommandLineAssembly.Location),
            MetadataReference.CreateFromFile(SystemCommandLineAssembly.Location)
        }.Concat(GetRuntimeReferences());

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        var generator = new CommandGenerator();

        GeneratorDriver driver;
        if (globalOptions is not null)
        {
            var optionsProvider = new DictionaryAnalyzerConfigOptionsProvider(globalOptions);
            driver = CSharpGeneratorDriver.Create(
                generators: [generator.AsSourceGenerator()],
                parseOptions: (CSharpParseOptions)syntaxTree.Options,
                optionsProvider: optionsProvider);
        }
        else
        {
            driver = CSharpGeneratorDriver.Create(
                generators: [generator.AsSourceGenerator()],
                parseOptions: (CSharpParseOptions)syntaxTree.Options);
        }

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        var driverResult = driver.GetRunResult();

        var generatedSource = driverResult.Results
            .SelectMany(static r => r.GeneratedSources)
            .Where(static s => s.HintName == "CommandInitializer.g.cs")
            .Select(static s => s.SourceText.ToString())
            .FirstOrDefault();

        var allGeneratorDiagnostics = driverResult.Results
            .SelectMany(static r => r.Diagnostics)
            .Concat(generatorDiagnostics)
            .ToList();

        var compilationErrors = outputCompilation.GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        return new GeneratorResult
        {
            GeneratedSource = generatedSource,
            GeneratorDiagnostics = allGeneratorDiagnostics,
            CompilationErrors = compilationErrors
        };
    }

    private static IEnumerable<MetadataReference> GetRuntimeReferences()
    {
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string trustedAssemblies)
        {
            yield break;
        }

        foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
        {
            if (!String.IsNullOrEmpty(path))
            {
                yield return MetadataReference.CreateFromFile(path);
            }
        }
    }

    // AnalyzerConfigOptionsProvider backed by a plain dictionary
    private sealed class DictionaryAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly DictionaryAnalyzerConfigOptions globalOptions;

        private static readonly DictionaryAnalyzerConfigOptions Empty = new([]);

        public DictionaryAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> options)
        {
            globalOptions = new DictionaryAnalyzerConfigOptions(
                options.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase));
        }

        public override AnalyzerConfigOptions GlobalOptions => globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
    }

    private sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> options;

        public DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string> options)
        {
            this.options = options;
        }

        public override bool TryGetValue(string key, out string value)
        {
            if (options.TryGetValue(key, out var v))
            {
                value = v;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
