// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CP.IO.Ports.SourceGeneration;
using CP.IO.Ports.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Core;

namespace CP.IO.Ports.Tests;

/// <summary>
/// Tests for the serial reactive stream source generator.
/// </summary>
public class SerialPortReactiveStreamGeneratorTests
{
    /// <summary>
    /// Verifies generated serial stream properties and observables compile.
    /// </summary>
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

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var errors = outputCompilation.GetDiagnostics()
            .Concat(diagnostics)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        var generatedTree = outputCompilation.SyntaxTrees.Single(tree =>
            tree.FilePath.EndsWith("GeneratedTests_DeviceState.SerialPortReactiveStreams.g.cs", StringComparison.Ordinal));
        var generatedSource = generatedTree.ToString();

        await Assert.That(errors).IsEmpty();
        await Assert.That(generatedSource).Contains("public double Temperature");
        await Assert.That(generatedSource).Contains("TemperatureObservable");
        await Assert.That(generatedSource).Contains("TemperatureObservableAsync");
        await Assert.That(generatedSource).Contains("public bool IsConnected");
    }

    private static CSharpCompilation CreateCompilation(string source, CSharpParseOptions parseOptions)
    {
        var references = new List<MetadataReference>();
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator) ?? [];

        references.AddRange(trustedPlatformAssemblies.Select(path => MetadataReference.CreateFromFile(path)));
        references.Add(MetadataReference.CreateFromFile(typeof(SerialPortRx).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(SerialPortReactiveValueConverter).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Reactive.Linq.Observable).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(ReactiveUI.Extensions.Async.IObservableAsync<>).Assembly.Location));

        return CSharpCompilation.Create(
            "SerialPortRx.GeneratedTests",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references.DistinctBy(reference => reference.Display),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }
}
