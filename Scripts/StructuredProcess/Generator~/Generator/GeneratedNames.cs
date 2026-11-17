using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tools.StructuredProcess.Generator
{
    /// <summary>Shortens generated names only when Roslyn resolves them to the same symbol.</summary>
    internal sealed class GeneratedNames : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semantic;
        private readonly CancellationToken _cancellation;

        private GeneratedNames(SemanticModel semantic, CancellationToken cancellation)
        {
            _semantic = semantic;
            _cancellation = cancellation;
        }

        internal static string Simplify(string source, ProcessModel process, Compilation compilation,
            CancellationToken cancellationToken)
        {
            var namespaces = new SortedSet<string>(StringComparer.Ordinal)
            {
                "System", "System.Collections.Generic", "System.Threading", "System.Threading.Tasks",
                "Logging", "Tools.StructuredProcess.Declarative"
            };
            foreach (var step in process.Steps)
            {
                CollectNamespaces(step.Type, namespaces);
            }

            foreach (var input in process.Inputs)
            {
                CollectNamespaces(input.Type, namespaces);
            }

            namespaces.Remove(process.Type.ContainingNamespace.ToDisplayString());
            string imports = string.Join(Environment.NewLine, namespaces.Select(item => "using global::" + item + ";"));
            int headerEnd = source.IndexOf("#nullable disable", StringComparison.Ordinal) + "#nullable disable".Length;
            source = source.Insert(headerEnd, Environment.NewLine + imports + Environment.NewLine);
            var options = (CSharpParseOptions)process.Location.SourceTree!.Options;
            var tree = CSharpSyntaxTree.ParseText(source, options, cancellationToken: cancellationToken);
            var semantic = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
            return new GeneratedNames(semantic, cancellationToken).Visit(tree.GetRoot(cancellationToken))!.ToFullString();
        }

        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
        {
            return ShortType(node) ?? base.VisitQualifiedName(node);
        }

        public override SyntaxNode? VisitAliasQualifiedName(AliasQualifiedNameSyntax node)
        {
            return ShortType(node) ?? base.VisitAliasQualifiedName(node);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (node.ToString().StartsWith("global::", StringComparison.Ordinal)
                && _semantic.GetSymbolInfo(node, _cancellation).Symbol is ITypeSymbol type)
            {
                string name = type.ToMinimalDisplayString(_semantic, node.SpanStart);
                var candidate = SyntaxFactory.ParseExpression(name);
                var symbol = _semantic.GetSpeculativeSymbolInfo(node.SpanStart, candidate,
                    SpeculativeBindingOption.BindAsExpression).Symbol;
                if (SymbolEqualityComparer.Default.Equals(type, symbol))
                {
                    return candidate.WithTriviaFrom(node);
                }
            }

            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Text.StartsWith("@", StringComparison.Ordinal)
                && SyntaxFacts.GetKeywordKind(token.ValueText) == SyntaxKind.None
                && SyntaxFacts.GetContextualKeywordKind(token.ValueText) == SyntaxKind.None)
            {
                return SyntaxFactory.Identifier(token.LeadingTrivia, token.ValueText, token.TrailingTrivia);
            }

            return base.VisitToken(token);
        }

        private TypeSyntax? ShortType(NameSyntax node)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (!node.ToString().StartsWith("global::", StringComparison.Ordinal)
                || !(_semantic.GetSymbolInfo(node, _cancellation).Symbol is ITypeSymbol type))
            {
                return null;
            }

            string name = type.ToMinimalDisplayString(_semantic, node.SpanStart);
            var candidate = SyntaxFactory.ParseTypeName(name);
            var resolved = _semantic.GetSpeculativeTypeInfo(node.SpanStart, candidate,
                SpeculativeBindingOption.BindAsTypeOrNamespace).Type;
            if (SymbolEqualityComparer.Default.Equals(type, resolved))
            {
                return candidate.WithTriviaFrom(node);
            }

            return null;
        }

        private static void CollectNamespaces(ITypeSymbol type, ISet<string> namespaces)
        {
            if (type is IArrayTypeSymbol array)
            {
                CollectNamespaces(array.ElementType, namespaces);
            }
            else if (type is INamedTypeSymbol named)
            {
                if (!named.ContainingNamespace.IsGlobalNamespace)
                {
                    namespaces.Add(named.ContainingNamespace.ToDisplayString());
                }

                foreach (var argument in named.TypeArguments)
                {
                    CollectNamespaces(argument, namespaces);
                }

                if (named.ContainingType != null)
                {
                    CollectNamespaces(named.ContainingType, namespaces);
                }
            }
        }
    }
}
