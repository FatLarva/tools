using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Tools.StructuredProcess.Generator
{
    internal static class Names
    {
        internal const string Runtime = "Tools.StructuredProcess.Declarative.";

        internal static string Full(ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
    }

    internal sealed class ProcessModel
    {
        internal INamedTypeSymbol Type = null!;
        internal Location Location = null!;
        internal readonly List<StepModel> Steps = new List<StepModel>();
        internal readonly List<MemberModel> Outputs = new List<MemberModel>();
        internal readonly List<ExternalInput> Inputs = new List<ExternalInput>();
        internal readonly List<Binding> OutputBindings = new List<Binding>();
        internal readonly List<StepModel> Order = new List<StepModel>();
    }

    internal sealed class StepModel
    {
        internal INamedTypeSymbol Type = null!;
        internal INamedTypeSymbol InputType = null!;
        internal INamedTypeSymbol OutputType = null!;
        internal Location Location = null!;
        internal string? Id;
        internal bool Optional;
        internal bool AnyThread;
        internal int Index;
        internal string Key => Id == null ? "type:" + Names.Full(Type) : "id:" + Id;
        internal readonly List<StepReference> After = new List<StepReference>();
        internal readonly List<MemberModel> Inputs = new List<MemberModel>();
        internal readonly List<MemberModel> Outputs = new List<MemberModel>();
        internal readonly List<Binding> Bindings = new List<Binding>();
        internal readonly HashSet<StepModel> Dependencies = new HashSet<StepModel>();
    }

    internal sealed class StepReference
    {
        internal string? Id;
        internal INamedTypeSymbol? Type;
        internal Location Location = null!;
    }

    internal sealed class MemberModel
    {
        internal string Name = "";
        internal string? Label;
        internal ITypeSymbol Type = null!;
        internal Location Location = null!;
    }

    internal sealed class ExternalInput
    {
        internal string Name = "";
        internal string? Label;
        internal ITypeSymbol Type = null!;
    }

    internal sealed class Binding
    {
        internal MemberModel Target = null!;
        internal StepModel? Producer;
        internal MemberModel? Output;
        internal ExternalInput? External;
    }

    internal sealed class DeclarationError : Exception
    {
        internal Diagnostic Diagnostic { get; }

        internal DeclarationError(string id, Location location, string message) : base(message)
        {
            var descriptor = new DiagnosticDescriptor(id, "Invalid process declaration", "{0}",
                "StructuredProcess", DiagnosticSeverity.Error, true);
            Diagnostic = Diagnostic.Create(descriptor, location, message);
        }
    }
}
