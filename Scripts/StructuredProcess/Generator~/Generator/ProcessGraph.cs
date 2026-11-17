using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Tools.StructuredProcess.Generator
{
    internal static class ProcessGraph
    {
        internal static void Bind(ProcessModel process, CancellationToken cancellation)
        {
            ValidateIds(process);
            var producers = new List<Binding>();
            foreach (var step in process.Steps)
            {
                cancellation.ThrowIfCancellationRequested();
                foreach (var output in step.Outputs)
                {
                    DeclarationParser.Require(!producers.Any(item => SameKey(item.Output!, output)),
                        "SPG005", step.Location, "Multiple outputs provide " + Describe(output)
                        + ". Give the outputs distinct Data IDs or occurrence aliases.");
                    producers.Add(new Binding { Producer = step, Output = output });
                }
            }

            foreach (var step in process.Steps.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                cancellation.ThrowIfCancellationRequested();
                foreach (var input in step.Inputs)
                {
                    var binding = Resolve(producers, input, step);
                    if (binding == null)
                    {
                        binding = new Binding { Target = input, External = External(process, input) };
                    }
                    else
                    {
                        step.Dependencies.Add(binding.Producer!);
                    }

                    step.Bindings.Add(binding);
                }

                foreach (var reference in step.After)
                {
                    var matches = process.Steps.Where(candidate => reference.Type != null
                        ? DeclarationParser.Same(candidate.Type, reference.Type)
                        : candidate.Id == reference.Id).ToList();
                    DeclarationParser.Require(matches.Count == 1, "SPG004", reference.Location,
                        matches.Count == 0 ? "The After dependency does not identify a declared step."
                        : "The After type occurs more than once. Refer to an explicit step ID.");
                    step.Dependencies.Add(matches[0]);
                }
            }

            foreach (var output in process.Outputs)
            {
                var binding = Resolve(producers, output, null);
                if (binding == null)
                {
                    var external = process.Inputs.Where(input => DeclarationParser.Same(input.Type, output.Type)
                        && (output.Label == null || input.Label == output.Label)).ToList();
                    DeclarationParser.Require(external.Count == 1, "SPG006", output.Location,
                        "Process output " + Describe(output) + " must have one producer or an existing process input.");
                    binding = new Binding { Target = output, External = external[0] };
                }

                process.OutputBindings.Add(binding);
            }

            var remaining = new HashSet<StepModel>(process.Steps);
            while (remaining.Count != 0)
            {
                cancellation.ThrowIfCancellationRequested();
                var next = remaining.Where(step => step.Dependencies.All(dependency => !remaining.Contains(dependency)))
                    .OrderBy(step => step.Key, StringComparer.Ordinal).FirstOrDefault();
                DeclarationParser.Require(next != null, "SPG008", process.Location,
                    "Dependencies contain a cycle involving: " + string.Join(", ",
                        remaining.OrderBy(step => step.Key, StringComparer.Ordinal).Select(step => step.Id ?? step.Type.Name)));
                next!.Index = process.Order.Count;
                process.Order.Add(next);
                remaining.Remove(next);
            }
        }

        private static void ValidateIds(ProcessModel process)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in process.Steps)
            {
                DeclarationParser.Require(keys.Add(step.Key), "SPG003", step.Location,
                    "Duplicate step ID: " + step.Key + ". Every occurrence of a repeated type needs a unique explicit Id.");
                DeclarationParser.Require(step.Id != null
                    || process.Steps.Count(candidate => DeclarationParser.Same(candidate.Type, step.Type)) == 1,
                    "SPG003", step.Location, "Every occurrence of the repeated type " + step.Type.Name + " needs an explicit Id.");
            }
        }

        private static Binding? Resolve(List<Binding> producers, MemberModel target, StepModel? consumer)
        {
            var matches = producers.Where(item => item.Producer != consumer
                && DeclarationParser.Same(item.Output!.Type, target.Type)
                && (target.Label == null || item.Output.Label == target.Label)).ToList();
            DeclarationParser.Require(matches.Count <= 1, "SPG005", target.Location,
                "Ambiguous data for " + Describe(target) + ". Use a Data ID or an occurrence alias.");
            if (matches.Count == 0)
            {
                return null;
            }

            var match = matches[0];
            DeclarationParser.Require(!match.Producer!.Optional, "SPG007", target.Location,
                "Data " + Describe(target) + " comes from optional step '"
                + (match.Producer.Id ?? match.Producer.Type.Name)
                + "'. Make its producer required and implement fallback inside that step.");
            return new Binding { Target = target, Producer = match.Producer, Output = match.Output };
        }

        private static ExternalInput External(ProcessModel process, MemberModel target)
        {
            var existing = process.Inputs.FirstOrDefault(input => DeclarationParser.Same(input.Type, target.Type)
                && input.Label == target.Label);
            if (existing != null)
            {
                return existing;
            }

            string name = Identifier(target.Label ?? target.Name);
            DeclarationParser.Require(process.Inputs.All(input => input.Name != name), "SPG006", target.Location,
                "Different external inputs map to the generated field '" + name + "'. Give them distinct Data IDs.");
            var external = new ExternalInput { Name = name, Label = target.Label, Type = target.Type };
            process.Inputs.Add(external);
            return external;
        }

        private static string Identifier(string label)
        {
            var result = new StringBuilder();
            bool upper = true;
            foreach (char character in label)
            {
                if (char.IsLetterOrDigit(character) || character == '_')
                {
                    if (result.Length == 0 && char.IsDigit(character))
                    {
                        result.Append('_');
                    }

                    result.Append(upper ? char.ToUpperInvariant(character) : character);
                    upper = false;
                }
                else
                {
                    upper = true;
                }
            }

            return result.Length == 0 ? "Value" : result.ToString();
        }

        private static bool SameKey(MemberModel left, MemberModel right)
        {
            return DeclarationParser.Same(left.Type, right.Type) && left.Label == right.Label;
        }

        private static string Describe(MemberModel member)
        {
            return "'" + (member.Label ?? member.Name) + "' (" + member.Type.ToDisplayString() + ")";
        }
    }
}
