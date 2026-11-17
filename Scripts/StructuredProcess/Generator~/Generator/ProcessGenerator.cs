using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Tools.StructuredProcess.Generator
{
    [Generator]
    public sealed class ProcessGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var processes = context.SyntaxProvider.CreateSyntaxProvider(
                (node, _) => node is ClassDeclarationSyntax declaration && declaration.BaseList != null,
                (syntax, cancellation) => Generate(syntax, cancellation));

            context.RegisterSourceOutput(processes, (output, result) =>
            {
                if (result == null)
                {
                    return;
                }

                if (result.Diagnostic != null)
                {
                    output.ReportDiagnostic(result.Diagnostic);
                    return;
                }

                output.AddSource(result.Name, SourceText.From(result.Source, Encoding.UTF8));
            });
        }

        private static GenerationResult? Generate(GeneratorSyntaxContext context,
            System.Threading.CancellationToken cancellationToken)
        {
            var declaration = (ClassDeclarationSyntax)context.Node;
            var type = context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) as INamedTypeSymbol;
            string? baseType = type?.BaseType?.ToDisplayString();
            if (type == null || (baseType != Names.Runtime + "LinearProcess"
                && baseType != Names.Runtime + "ParallelProcess"))
            {
                return null;
            }

            try
            {
                var parser = new DeclarationParser(context.SemanticModel.Compilation, cancellationToken);
                var model = parser.Parse(type, declaration);
                model.Parallel = baseType == Names.Runtime + "ParallelProcess";
                ProcessGraph.Bind(model, cancellationToken);
                return new GenerationResult
                {
                    Name = type.ToDisplayString().Replace("@", "") + ".Process.g.cs",
                    Source = ProcessEmitter.Emit(model, context.SemanticModel.Compilation, cancellationToken)
                };
            }
            catch (DeclarationError error)
            {
                return new GenerationResult { Diagnostic = error.Diagnostic };
            }
        }

        private sealed class GenerationResult
        {
            internal string Name = "";
            internal string Source = "";
            internal Diagnostic? Diagnostic;
        }
    }
}
