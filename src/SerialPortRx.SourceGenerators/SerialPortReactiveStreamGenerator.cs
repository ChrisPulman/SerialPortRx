// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CP.IO.Ports.SourceGenerators;

/// <summary>
/// Generates reactive serial-port properties and observable streams.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SerialPortReactiveStreamGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "CP.IO.Ports.SourceGeneration.SerialPortReactiveStreamAttribute";
    private static readonly DiagnosticDescriptor ClassMustBePartial = new(
        "SPRX001",
        "Reactive serial stream target must be partial",
        "Type '{0}' must be partial to receive generated serial stream members",
        "SerialPortRx.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor PropertyNameMustBeIdentifier = new(
        "SPRX002",
        "Reactive serial stream property name must be a valid identifier",
        "Property name '{0}' must be a valid C# identifier",
        "SerialPortRx.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
            postInitializationContext.AddSource("SerialPortReactiveStreamAttribute.g.cs", AttributeSource));

        var streamDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, cancellationToken) => GetStreamInfos(syntaxContext, cancellationToken));

        context.RegisterSourceOutput(
            streamDeclarations.Collect(),
            static (sourceProductionContext, streams) => Execute(sourceProductionContext, streams));
    }

    private static ImmutableArray<StreamInfo> GetStreamInfos(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol targetType)
        {
            return ImmutableArray<StreamInfo>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<StreamInfo>();
        foreach (var attribute in context.Attributes.Where(attributeData =>
                     attributeData.AttributeClass?.ToDisplayString() == AttributeMetadataName))
        {
            if (TryCreateStreamInfo(targetType, attribute, cancellationToken, out var streamInfo))
            {
                builder.Add(streamInfo);
            }
        }

        return builder.ToImmutable();
    }

    private static bool TryCreateStreamInfo(INamedTypeSymbol targetType, AttributeData attribute, CancellationToken cancellationToken, out StreamInfo streamInfo)
    {
        streamInfo = default!;
        if (attribute.ConstructorArguments.Length < 2)
        {
            return false;
        }

        var propertyName = attribute.ConstructorArguments[0].Value as string ?? string.Empty;
        var propertyType = attribute.ConstructorArguments[1].Value as ITypeSymbol;
        var pattern = attribute.ConstructorArguments.Length > 2
            ? attribute.ConstructorArguments[2].Value as string
            : null;

        var source = SerialPortReactiveSource.Lines;
        var groupName = "value";
        var groupNumber = 1;
        var ignoreCase = false;

        foreach (var argument in attribute.NamedArguments)
        {
            switch (argument.Key)
            {
                case "Source":
                    source = (SerialPortReactiveSource)(argument.Value.Value as int? ?? 0);
                    break;
                case "GroupName":
                    groupName = argument.Value.Value as string;
                    break;
                case "GroupNumber":
                    groupNumber = argument.Value.Value as int? ?? 1;
                    break;
                case "IgnoreCase":
                    ignoreCase = argument.Value.Value as bool? ?? false;
                    break;
            }
        }

        streamInfo = new StreamInfo(
            targetType,
            propertyName,
            propertyType,
            pattern,
            source,
            groupName,
            groupNumber,
            ignoreCase,
            attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation() ?? targetType.Locations.FirstOrDefault());

        return true;
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<ImmutableArray<StreamInfo>> streamInfoGroups)
    {
        var streamInfos = streamInfoGroups.SelectMany(static group => group);
        foreach (var typeGroup in streamInfos.GroupBy(static info => info.TargetType, SymbolEqualityComparer.Default))
        {
            var targetType = typeGroup.First().TargetType;
            if (!IsPartial(targetType))
            {
                context.ReportDiagnostic(Diagnostic.Create(ClassMustBePartial, targetType.Locations.FirstOrDefault(), targetType.Name));
                continue;
            }

            var streams = new List<StreamInfo>();
            foreach (var stream in typeGroup)
            {
                if (stream.PropertyType == null)
                {
                    continue;
                }

                if (!SyntaxFacts.IsValidIdentifier(stream.PropertyName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(PropertyNameMustBeIdentifier, stream.Location, stream.PropertyName));
                    continue;
                }

                streams.Add(stream);
            }

            if (streams.Count == 0)
            {
                continue;
            }

            var source = GenerateType(targetType, streams);
            context.AddSource($"{SanitizeHintName(targetType.ToDisplayString())}.SerialPortReactiveStreams.g.cs", source);
        }
    }

    private static bool IsPartial(INamedTypeSymbol typeSymbol) =>
        typeSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .Any(classDeclaration => classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword));

    private static string GenerateType(INamedTypeSymbol targetType, IReadOnlyList<StreamInfo> streams)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        var namespaceName = targetType.ContainingNamespace.IsGlobalNamespace ? null : targetType.ContainingNamespace.ToDisplayString();
        if (namespaceName != null)
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("partial class ").Append(targetType.Name).AppendLine();
        builder.AppendLine("{");

        foreach (var stream in streams)
        {
            AppendStreamMembers(builder, stream);
        }

        builder.AppendLine("    public global::System.IDisposable ConnectReactiveSerialPort(global::CP.IO.Ports.ISerialPortRx serialPort)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (serialPort == null)");
        builder.AppendLine("        {");
        builder.AppendLine("            throw new global::System.ArgumentNullException(nameof(serialPort));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        var disposables = new global::System.Reactive.Disposables.CompositeDisposable();");

        foreach (var stream in streams)
        {
            AppendSubscription(builder, stream);
        }

        builder.AppendLine("        return disposables;");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendStreamMembers(StringBuilder builder, StreamInfo stream)
    {
        var typeName = stream.PropertyType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var fieldName = GetSubjectFieldName(stream.PropertyName);

        builder.Append("    private readonly global::System.Reactive.Subjects.Subject<")
            .Append(typeName)
            .Append("> ")
            .Append(fieldName)
            .AppendLine(" = new();");
        builder.AppendLine();
        builder.Append("    public ").Append(typeName).Append(' ').Append(stream.PropertyName).AppendLine(" { get; private set; } = default!;");
        builder.AppendLine();
        builder.Append("    public global::System.IObservable<")
            .Append(typeName)
            .Append("> ")
            .Append(stream.PropertyName)
            .AppendLine("Observable => global::System.Reactive.Linq.Observable.AsObservable(" + fieldName + ");");
        builder.AppendLine();
        builder.Append("    public global::ReactiveUI.Extensions.Async.IObservableAsync<")
            .Append(typeName)
            .Append("> ")
            .Append(stream.PropertyName)
            .AppendLine("ObservableAsync => global::ReactiveUI.Extensions.Async.ObservableBridgeExtensions.ToObservableAsync(" + stream.PropertyName + "Observable);");
        builder.AppendLine();
    }

    private static void AppendSubscription(StringBuilder builder, StreamInfo stream)
    {
        var sourceExpression = stream.Source switch
        {
            SerialPortReactiveSource.DataReceived => "serialPort.DataReceived",
            SerialPortReactiveSource.DataReceivedBytes => "serialPort.DataReceivedBytes",
            SerialPortReactiveSource.BytesReceived => "serialPort.BytesReceived",
            SerialPortReactiveSource.IsOpen => "serialPort.IsOpenObservable",
            _ => "serialPort.Lines",
        };

        var typeName = stream.PropertyType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var fieldName = GetSubjectFieldName(stream.PropertyName);

        builder.Append("        disposables.Add(global::System.ObservableExtensions.Subscribe(")
            .Append(sourceExpression)
            .AppendLine(", __serialPortRxValue =>");
        builder.AppendLine("        {");
        builder.Append("            if (global::CP.IO.Ports.SourceGeneration.SerialPortReactiveValueConverter.TryConvertMatch<")
            .Append(typeName)
            .Append(">(__serialPortRxValue, ")
            .Append(ToLiteral(stream.Pattern))
            .Append(", ")
            .Append(ToLiteral(stream.GroupName))
            .Append(", ")
            .Append(stream.GroupNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(", ")
            .Append(stream.IgnoreCase ? "true" : "false")
            .AppendLine(", out var __serialPortRxConverted))");
        builder.AppendLine("            {");
        builder.Append("                ").Append(stream.PropertyName).AppendLine(" = __serialPortRxConverted;");
        builder.Append("                ").Append(fieldName).AppendLine(".OnNext(__serialPortRxConverted);");
        builder.AppendLine("            }");
        builder.AppendLine("        }));");
    }

    private static string GetSubjectFieldName(string propertyName) =>
        "__serialPortRx" + propertyName + "Subject";

    private static string ToLiteral(string? value) =>
        value == null ? "null" : SymbolDisplay.FormatLiteral(value, quote: true);

    private static string SanitizeHintName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    private enum SerialPortReactiveSource
    {
        Lines = 0,
        DataReceived = 1,
        DataReceivedBytes = 2,
        BytesReceived = 3,
        IsOpen = 4,
    }

    private sealed class StreamInfo
    {
        public StreamInfo(
            INamedTypeSymbol targetType,
            string propertyName,
            ITypeSymbol? propertyType,
            string? pattern,
            SerialPortReactiveSource source,
            string? groupName,
            int groupNumber,
            bool ignoreCase,
            Location? location)
        {
            TargetType = targetType;
            PropertyName = propertyName;
            PropertyType = propertyType;
            Pattern = pattern;
            Source = source;
            GroupName = groupName;
            GroupNumber = groupNumber;
            IgnoreCase = ignoreCase;
            Location = location;
        }

        public INamedTypeSymbol TargetType { get; }

        public string PropertyName { get; }

        public ITypeSymbol? PropertyType { get; }

        public string? Pattern { get; }

        public SerialPortReactiveSource Source { get; }

        public string? GroupName { get; }

        public int GroupNumber { get; }

        public bool IgnoreCase { get; }

        public Location? Location { get; }
    }

    private const string AttributeSource = """
// <auto-generated />
#nullable enable

namespace CP.IO.Ports.SourceGeneration;

/// <summary>
/// Selects the serial stream that drives a generated reactive property.
/// </summary>
public enum SerialPortReactiveSource
{
    /// <summary>
    /// The complete line stream from ISerialPortRx.Lines.
    /// </summary>
    Lines = 0,

    /// <summary>
    /// The character stream from ISerialPortRx.DataReceived.
    /// </summary>
    DataReceived = 1,

    /// <summary>
    /// The raw byte stream from ISerialPortRx.DataReceivedBytes.
    /// </summary>
    DataReceivedBytes = 2,

    /// <summary>
    /// The byte stream emitted by ReadAsync via IPortRx.BytesReceived.
    /// </summary>
    BytesReceived = 3,

    /// <summary>
    /// The open state stream from ISerialPortRx.IsOpenObservable.
    /// </summary>
    IsOpen = 4,
}

/// <summary>
/// Generates a property plus classic and async observable streams from serial data.
/// </summary>
[global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SerialPortReactiveStreamAttribute : global::System.Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortReactiveStreamAttribute"/> class.
    /// </summary>
    /// <param name="propertyName">The generated property name.</param>
    /// <param name="propertyType">The generated property type.</param>
    public SerialPortReactiveStreamAttribute(string propertyName, global::System.Type propertyType)
        : this(propertyName, propertyType, pattern: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerialPortReactiveStreamAttribute"/> class.
    /// </summary>
    /// <param name="propertyName">The generated property name.</param>
    /// <param name="propertyType">The generated property type.</param>
    /// <param name="pattern">The optional regular expression used to identify relevant data.</param>
    public SerialPortReactiveStreamAttribute(string propertyName, global::System.Type propertyType, string? pattern)
    {
        PropertyName = propertyName;
        PropertyType = propertyType;
        Pattern = pattern;
    }

    /// <summary>
    /// Gets the generated property name.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the generated property type.
    /// </summary>
    public global::System.Type PropertyType { get; }

    /// <summary>
    /// Gets the optional regular expression used to identify relevant data.
    /// </summary>
    public string? Pattern { get; }

    /// <summary>
    /// Gets or sets the serial stream source.
    /// </summary>
    public SerialPortReactiveSource Source { get; set; } = SerialPortReactiveSource.Lines;

    /// <summary>
    /// Gets or sets the named regular expression group converted into the property value.
    /// </summary>
    public string? GroupName { get; set; } = "value";

    /// <summary>
    /// Gets or sets the fallback regular expression group number converted into the property value.
    /// </summary>
    public int GroupNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether pattern matching ignores case.
    /// </summary>
    public bool IgnoreCase { get; set; }
}
""";
}
