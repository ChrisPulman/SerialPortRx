// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace CP.IO.Ports.Tests;

/// <summary>Tests for the serial reactive stream source generator.</summary>
public class SerialPortReactiveStreamGeneratorTests
{
    /// <summary>Verifies generated serial stream properties and observables compile.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Generator_CreatesPropertyAndObservableStreams()
    {
        const string source = """
using CP.IO.Ports.SourceGeneration;

namespace GeneratedTests;

[SerialPortReactiveStream("Temperature", typeof(double), @"^TEMP:(?<value>-?\d+(\.\d+)?)$")]
[SerialPortReactiveStream("IsConnected", typeof(bool), Source = SerialPortReactiveSource.IsOpen)]
public partial class DeviceState
{
}
""";

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var compilation = CreateCompilation(source, parseOptions);
        var generator = new SerialPortReactiveStreamGenerator();
        var driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parseOptions);

        _ = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var errors = new List<Diagnostic>();
        foreach (var diagnostic in outputCompilation.GetDiagnostics())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic);
            }
        }

        SyntaxTree? generatedTree = null;
        foreach (var tree in outputCompilation.SyntaxTrees)
        {
            if (tree.FilePath.EndsWith("GeneratedTests_DeviceState.SerialPortReactiveStreams.g.cs", StringComparison.Ordinal))
            {
                generatedTree = tree;
                break;
            }
        }

        if (generatedTree is null)
        {
            throw new InvalidOperationException("The expected generated syntax tree was not produced.");
        }

        var generatedSource = generatedTree.ToString();

        await Assert.That(errors).IsEmpty();
        await Assert.That(generatedSource).Contains("public double Temperature");
        await Assert.That(generatedSource).Contains("TemperatureObservable");
        await Assert.That(generatedSource).Contains("TemperatureObservableAsync");
        await Assert.That(generatedSource).Contains("public bool IsConnected");
    }

    /// <summary>Creates a C# compilation for source generator tests.</summary>
    /// <param name="source">The source text to compile.</param>
    /// <param name="parseOptions">The parse options to use.</param>
    /// <returns>The created compilation.</returns>
    private static CSharpCompilation CreateCompilation(string source, CSharpParseOptions parseOptions)
    {
        var references = new List<MetadataReference>();
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator) ?? [];

        foreach (var path in trustedPlatformAssemblies)
        {
            AddReferenceIfMissing(references, path);
        }

        AddReferenceIfMissing(references, typeof(SerialPortRx).Assembly.Location);
        AddReferenceIfMissing(references, typeof(SerialPortReactiveValueConverter).Assembly.Location);
        AddReferenceIfMissing(references, typeof(Signal).Assembly.Location);
        AddReferenceIfMissing(references, typeof(IObservableAsync<>).Assembly.Location);
        AddReferenceIfMissing(references, typeof(ObservableAsyncBridgeExtensions).Assembly.Location);

        return CSharpCompilation.Create(
            "SerialPortRx.GeneratedTests",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>Adds a metadata reference if it has not already been added.</summary>
    /// <param name="references">The reference collection.</param>
    /// <param name="path">The assembly path.</param>
    private static void AddReferenceIfMissing(List<MetadataReference> references, string path)
    {
        foreach (var reference in references)
        {
            if (string.Equals(reference.Display, path, StringComparison.Ordinal))
            {
                return;
            }
        }

        references.Add(MetadataReference.CreateFromFile(path));
    }
}
