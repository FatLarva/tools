using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tools.StructuredProcess.Generator
{
    internal sealed class DeclarationParser
    {
        private readonly Compilation _compilation;
        private readonly CancellationToken _cancellation;

        internal DeclarationParser(Compilation compilation, CancellationToken cancellation)
        {
            _compilation = compilation;
            _cancellation = cancellation;
        }

        internal ProcessModel Parse(INamedTypeSymbol type, ClassDeclarationSyntax declaration)
        {
            var model = new ProcessModel { Type = type, Location = declaration.Identifier.GetLocation() };
            Require(declaration.Modifiers.Any(SyntaxKind.PartialKeyword) && type.ContainingType == null
                && type.TypeParameters.Length == 0 && !type.IsAbstract, "SPG001", model.Location,
                "A process must be a top-level, non-generic, non-abstract partial class.");
            Require(type.GetTypeMembers("Input").Length == 0, "SPG001", model.Location,
                "Process.Input is generated from unresolved step inputs; do not declare it manually.");

            var output = Contract(type, "Output", "IProcessOutput", model.Location);
            model.Outputs.AddRange(Members(output, true));

            var field = type.GetMembers("Steps").OfType<IFieldSymbol>().FirstOrDefault();
            Require(field != null && field.IsStatic && field.Type is IArrayTypeSymbol array
                && array.ElementType.ToDisplayString() == Names.Runtime + "StepDescription",
                "SPG001", model.Location, "Declare a static StepDescription[] Steps field with an initializer.");
            var variable = field!.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(_cancellation)
                as VariableDeclaratorSyntax;
            var expression = variable?.Initializer?.Value;
            var initializer = ArrayInitializer(expression);
            Require(initializer != null, "SPG001", variable?.GetLocation() ?? model.Location,
                "Declare Steps directly in an array initializer. Calls, builders and runtime values are not supported.");

            foreach (var entry in initializer!.Expressions)
            {
                _cancellation.ThrowIfCancellationRequested();
                model.Steps.Add(ParseStep(entry, type));
            }

            return model;
        }

        private StepModel ParseStep(ExpressionSyntax expression, INamedTypeSymbol process)
        {
            var semantic = _compilation.GetSemanticModel(expression.SyntaxTree);
            Require(semantic.GetTypeInfo(expression, _cancellation).Type?.ToDisplayString()
                == Names.Runtime + "StepDescription", "SPG001", expression.GetLocation(),
                "Each entry must create a StepDescription directly.");

            ArgumentListSyntax? arguments = null;
            InitializerExpressionSyntax? initializer = null;
            if (expression is ObjectCreationExpressionSyntax creation)
            {
                arguments = creation.ArgumentList;
                initializer = creation.Initializer;
            }
            else if (expression is ImplicitObjectCreationExpressionSyntax implicitCreation)
            {
                arguments = implicitCreation.ArgumentList;
                initializer = implicitCreation.Initializer;
            }

            Require(arguments != null && arguments.Arguments.Count == 1, "SPG001", expression.GetLocation(),
                "Use new StepDescription(typeof(YourStep)) with an optional object initializer.");
            var type = ReadType(arguments!.Arguments[0].Expression);
            var step = new StepModel { Type = type, Location = expression.GetLocation() };
            Require(type.TypeKind == TypeKind.Class && !type.IsAbstract && !type.IsUnboundGenericType
                && _compilation.IsSymbolAccessibleWithin(type, process), "SPG002", step.Location,
                "A step must be an accessible, concrete class.");
            step.InputType = Contract(type, "Input", "IProcessInput", step.Location);
            step.OutputType = Contract(type, "Output", "IProcessOutput", step.Location);
            Require(type.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 1
                && constructor.Parameters[0].RefKind == RefKind.None
                && Same(constructor.Parameters[0].Type, step.InputType)
                && _compilation.IsSymbolAccessibleWithin(constructor, process)), "SPG002", step.Location,
                "A step needs an accessible constructor accepting its Input struct by value.");

            var contract = _compilation.GetTypeByMetadataName(Names.Runtime + "IProcessStep`1");
            Require(contract != null && type.AllInterfaces.Any(item => Same(item.OriginalDefinition, contract)
                && Same(item.TypeArguments[0], step.OutputType)), "SPG002", step.Location,
                "A step must implement IProcessStep<Output>.");
            Require(type.GetMembers("ExecuteAsync").OfType<IMethodSymbol>().Any(method => !method.IsStatic
                && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1
                && method.Parameters[0].Type.ToDisplayString() == "System.Threading.CancellationToken"
                && method.ReturnType is INamedTypeSymbol task
                && task.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>"
                && task.TypeArguments[0] is INamedTypeSymbol result
                && result.OriginalDefinition.ToDisplayString() == Names.Runtime + "StepResult<TOutput>"
                && Same(result.TypeArguments[0], step.OutputType)), "SPG002", step.Location,
                "Expose public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken).");

            step.Inputs.AddRange(Members(step.InputType, true));
            step.Outputs.AddRange(Members(step.OutputType, false));
            if (initializer == null)
            {
                return step;
            }

            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in initializer.Expressions)
            {
                Require(item is AssignmentExpressionSyntax assignment
                    && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    && assignment.Left is IdentifierNameSyntax, "SPG001", item.GetLocation(),
                    "Use property assignments in a StepDescription initializer.");
                var property = (AssignmentExpressionSyntax)item;
                string name = ((IdentifierNameSyntax)property.Left).Identifier.ValueText;
                Require(properties.Add(name), "SPG001", item.GetLocation(), "Duplicate setting: " + name);
                switch (name)
                {
                    case "Id":
                        step.Id = ReadString(property.Right);
                        break;
                    case "Optional":
                        var optional = Constant(property.Right);
                        Require(optional is bool, "SPG001", item.GetLocation(), "Optional must be a constant bool.");
                        step.Optional = (bool)optional!;
                        break;
                    case "Thread":
                        var thread = Constant(property.Right);
                        Require(thread is int value && (value == 0 || value == 1), "SPG001", item.GetLocation(),
                            "Thread must be StepThread.Main or StepThread.Any.");
                        step.AnyThread = (int)thread! == 1;
                        break;
                    case "After":
                        foreach (var dependency in Collection(property.Right).Expressions)
                        {
                            var reference = new StepReference { Location = dependency.GetLocation() };
                            if (dependency is TypeOfExpressionSyntax)
                            {
                                reference.Type = ReadType(dependency);
                            }
                            else
                            {
                                reference.Id = ReadString(dependency);
                            }

                            step.After.Add(reference);
                        }

                        break;
                    case "Inputs":
                        ApplyAliases(step.Inputs, property.Right);
                        break;
                    case "Outputs":
                        ApplyAliases(step.Outputs, property.Right);
                        break;
                    default:
                        throw new DeclarationError("SPG001", item.GetLocation(), "Unsupported setting: " + name);
                }
            }

            return step;
        }

        private INamedTypeSymbol Contract(INamedTypeSymbol owner, string name, string marker, Location location)
        {
            var type = owner.GetTypeMembers(name).FirstOrDefault();
            Require(type != null && type.TypeKind == TypeKind.Struct && type.TypeParameters.Length == 0
                && type.DeclaredAccessibility == Accessibility.Public
                && type.AllInterfaces.Any(item => item.ToDisplayString() == Names.Runtime + marker),
                "SPG002", location, owner.Name + " needs a public nested " + name + " struct implementing " + marker + ".");
            return type!;
        }

        private IEnumerable<MemberModel> Members(INamedTypeSymbol type, bool writable)
        {
            foreach (var symbol in type.GetMembers())
            {
                _cancellation.ThrowIfCancellationRequested();
                if (symbol.IsStatic || symbol.IsImplicitlyDeclared || symbol.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                ITypeSymbol memberType;
                if (symbol is IFieldSymbol field)
                {
                    Require(!writable || !field.IsReadOnly, "SPG002", symbol.Locations.First(),
                        "Input and process Output fields must be writable: " + symbol.Name);
                    memberType = field.Type;
                }
                else if (symbol is IPropertySymbol property && !property.IsIndexer)
                {
                    var accessor = writable ? property.SetMethod : property.GetMethod;
                    Require(accessor?.DeclaredAccessibility == Accessibility.Public, "SPG002", symbol.Locations.First(),
                        "The data property needs a public " + (writable ? "setter: " : "getter: ") + symbol.Name);
                    memberType = property.Type;
                }
                else
                {
                    continue;
                }

                string? label = null;
                var attribute = symbol.GetAttributes().FirstOrDefault(item =>
                    item.AttributeClass?.ToDisplayString() == Names.Runtime + "DataAttribute");
                if (attribute != null)
                {
                    label = attribute.ConstructorArguments.FirstOrDefault().Value as string;
                    Require(!string.IsNullOrWhiteSpace(label), "SPG009", symbol.Locations.First(),
                        "A Data ID must be a nonempty constant string.");
                }

                yield return new MemberModel
                {
                    Name = symbol.Name, Label = label, Type = memberType, Location = symbol.Locations.First()
                };
            }
        }

        private void ApplyAliases(List<MemberModel> members, ExpressionSyntax expression)
        {
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in Collection(expression).Expressions)
            {
                Require(item is AssignmentExpressionSyntax assignment
                    && assignment.Left is ImplicitElementAccessSyntax access && access.ArgumentList.Arguments.Count == 1,
                    "SPG009", item.GetLocation(), "Declare aliases as [\"local-id\"] = \"process-id\".");
                var alias = (AssignmentExpressionSyntax)item;
                string source = ReadString(((ImplicitElementAccessSyntax)alias.Left).ArgumentList.Arguments[0].Expression);
                Require(!aliases.ContainsKey(source), "SPG009", item.GetLocation(), "Duplicate alias: " + source);
                Require(members.Any(member => (member.Label ?? member.Name) == source), "SPG009", item.GetLocation(),
                    "No data member has the local ID '" + source + "'.");
                aliases.Add(source, ReadString(alias.Right));
            }

            foreach (var member in members)
            {
                if (aliases.TryGetValue(member.Label ?? member.Name, out string label))
                {
                    member.Label = label;
                }
            }
        }

        private INamedTypeSymbol ReadType(ExpressionSyntax expression)
        {
            Require(expression is TypeOfExpressionSyntax, "SPG001", expression.GetLocation(), "Use typeof(YourStep).");
            var type = _compilation.GetSemanticModel(expression.SyntaxTree)
                .GetTypeInfo(((TypeOfExpressionSyntax)expression).Type, _cancellation).Type as INamedTypeSymbol;
            Require(type != null && type.TypeKind != TypeKind.Error, "SPG002", expression.GetLocation(),
                "The step type could not be resolved.");
            return type!;
        }

        private string ReadString(ExpressionSyntax expression)
        {
            var value = Constant(expression) as string;
            Require(!string.IsNullOrWhiteSpace(value), "SPG001", expression.GetLocation(),
                "Use a nonempty constant string, const field or nameof expression.");
            return value!;
        }

        private object? Constant(ExpressionSyntax expression)
        {
            var constant = _compilation.GetSemanticModel(expression.SyntaxTree).GetConstantValue(expression, _cancellation);
            return constant.HasValue ? constant.Value : null;
        }

        private static InitializerExpressionSyntax Collection(ExpressionSyntax expression)
        {
            Require(expression is InitializerExpressionSyntax, "SPG001", expression.GetLocation(),
                "Use a nested collection initializer: { ... }.");
            return (InitializerExpressionSyntax)expression;
        }

        private static InitializerExpressionSyntax? ArrayInitializer(ExpressionSyntax? expression)
        {
            if (expression is ArrayCreationExpressionSyntax array)
            {
                return array.Initializer;
            }

            if (expression is ImplicitArrayCreationExpressionSyntax implicitArray)
            {
                return implicitArray.Initializer;
            }

            return expression as InitializerExpressionSyntax;
        }

        internal static bool Same(ITypeSymbol left, ITypeSymbol right)
        {
            return SymbolEqualityComparer.Default.Equals(left, right);
        }

        internal static void Require(bool condition, string id, Location location, string message)
        {
            if (!condition)
            {
                throw new DeclarationError(id, location, message);
            }
        }
    }
}
