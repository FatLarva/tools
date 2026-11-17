using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Tools.StructuredProcess.Declarative;

namespace Tools.StructuredProcess.Generator.Tests
{
    public sealed class GeneratorTests
    {
        private const string Header = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using Tools.StructuredProcess.Declarative;
namespace Fixture
{
    public sealed class Trace
    {
        public readonly List<string> Entries = new List<string>();
        public bool Throw;
        public bool Cleaned;
    }

    public sealed class SourceStep : IProcessStep<SourceStep.Output>
    {
        private readonly Input _input;
        public struct Input : IProcessInput
        {
            [Data(""seed"")] public string Seed;
            public Trace Trace;
        }
        public struct Output : IProcessOutput
        {
            [Data(""value"")] public string Value;
        }
        public SourceStep(Input input)
        {
            _input = input;
        }
        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Trace.Entries.Add(""source:"" + _input.Seed);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Value = _input.Seed + ""!"" }));
        }
    }

    public sealed class ConsumeStep : IProcessStep<ConsumeStep.Output>
    {
        private readonly Input _input;
        public struct Input : IProcessInput
        {
            [Data(""value"")] public string Value;
            public Trace Trace;
        }
        public struct Output : IProcessOutput
        {
            [Data(""text"")] public string Text;
        }
        public ConsumeStep(Input input)
        {
            _input = input;
        }
        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            _input.Trace.Entries.Add(""consume"");
            return StepResult<Output>.Success(new Output { Text = _input.Value + ""?"" });
        }
    }

    public sealed class FailStep : IProcessStep<FailStep.Output>
    {
        private readonly Input _input;
        public struct Input : IProcessInput
        {
            public Trace Trace;
        }
        public struct Output : IProcessOutput
        {
        }
        public FailStep(Input input)
        {
            _input = input;
        }
        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Trace.Entries.Add(""fail"");
            if (_input.Trace.Throw)
            {
                throw new InvalidOperationException(""broken"");
            }
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Failure(""expected""));
        }
    }

    public sealed class FinishStep : IProcessStep<FinishStep.Output>
    {
        private readonly Input _input;
        public struct Input : IProcessInput
        {
            public Trace Trace;
        }
        public struct Output : IProcessOutput
        {
        }
        public FinishStep(Input input)
        {
            _input = input;
        }
        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Trace.Entries.Add(""finish"");
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(default));
        }
    }
";

        private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator).Append(typeof(LinearProcess).Assembly.Location).Distinct()
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();

        [Test]
        public async Task ResolvesDataAndGeneratesInputRegardlessOfDeclarationOrder()
        {
            string source = @"
public partial class Process : LinearProcess
{
    public static readonly StepDescription[] Steps =
    {
        new StepDescription(typeof(ConsumeStep)),
        new StepDescription(typeof(SourceStep))
    };
    public struct Output : IProcessOutput
    {
        [Data(""text"")] public string Text;
    }
}
public static class Entry
{
    public static async Task<string> Run()
    {
        var trace = new Trace();
        var process = new Process(new Process.Input { Seed = ""a"", Trace = trace });
        var result = await process.ExecuteAsync();
        return result.Output.Text + ""|"" + string.Join("","", trace.Entries);
    }
} ";
            Assert.That(await Run(source), Is.EqualTo("a!?|source:a,consume"));
        }

        [Test]
        public async Task AliasesAllowRepeatedStepsAndSharedInputs()
        {
            string source = @"
public partial class Process : LinearProcess
{
    private const string Player = ""player"";
    public static readonly StepDescription[] Steps =
    {
        new StepDescription(typeof(SourceStep))
        {
            Id = Player,
            Inputs = { [""seed""] = ""player-seed"" },
            Outputs = { [""value""] = ""player-value"" }
        },
        new StepDescription(typeof(SourceStep))
        {
            Id = nameof(SourceStep),
            Inputs = { [""seed""] = ""opponent-seed"" },
            Outputs = { [""value""] = ""opponent-value"" },
            After = { Player },
            Thread = StepThread.Any
        }
    };
    public struct Output : IProcessOutput
    {
        [Data(""player-value"")] public string Player;
        [Data(""opponent-value"")] public string Opponent;
    }
}
public static class Entry
{
    public static async Task<string> Run()
    {
        var trace = new Trace();
        var process = new Process(new Process.Input { PlayerSeed = ""p"", OpponentSeed = ""o"", Trace = trace });
        var result = await process.ExecuteAsync();
        return result.Output.Player + result.Output.Opponent + ""|"" + string.Join("","", trace.Entries);
    }
}";
            Assert.That(await Run(source), Is.EqualTo("p!o!|source:p,source:o"));
        }

        [TestCase(false, false, "False|fail|1|expected")]
        [TestCase(true, false, "True|fail,finish|1|")]
        [TestCase(false, true, "False|fail|1|broken")]
        [TestCase(true, true, "True|fail,finish|1|")]
        public async Task RequiredFailureStopsAndOptionalFailureLogsAndContinues(bool optional, bool throws, string expected)
        {
            string source = @"
public partial class Process : LinearProcess
{
    public static readonly StepDescription[] Steps =
    {
        new StepDescription(typeof(FinishStep)) { After = { typeof(FailStep) } },
        new StepDescription(typeof(FailStep)) { Optional = OPTIONAL }
    };
    public struct Output : IProcessOutput
    {
    }
}
public static class Entry
{
    public static async Task<string> Run()
    {
        var trace = new Trace { Throw = THROWS };
        var log = new TestLog();
        var result = await new Process(new Process.Input { Trace = trace }, log).ExecuteAsync();
        return result.IsSuccess + ""|"" + string.Join("","", trace.Entries) + ""|"" + log.Messages.Count + ""|"" + result.Error;
    }
}";
            Assert.That(await Run(source.Replace("OPTIONAL", optional ? "true" : "false")
                .Replace("THROWS", throws ? "true" : "false")), Is.EqualTo(expected));
        }

        [Test]
        public async Task CancellationWaitsForStepCleanupAndDoesNotStartSuccessor()
        {
            string source = @"
public sealed class WaitStep : IProcessStep<WaitStep.Output>
{
    private readonly Input _input;
    public struct Input : IProcessInput
    {
        public Trace Trace;
    }
    public struct Output : IProcessOutput
    {
    }
    public WaitStep(Input input)
    {
        _input = input;
    }
    public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return StepResult<Output>.Success(default);
        }
        finally
        {
            await Task.Yield();
            _input.Trace.Cleaned = true;
        }
    }
}
public partial class Process : LinearProcess
{
    public static readonly StepDescription[] Steps =
    {
        new StepDescription(typeof(FinishStep)) { After = { typeof(WaitStep) } },
        new StepDescription(typeof(WaitStep)) { Optional = true }
    };
    public struct Output : IProcessOutput
    {
    }
}
public static class Entry
{
    public static async Task<string> Run()
    {
        var trace = new Trace();
        using var cancellation = new CancellationTokenSource();
        var running = new Process(new Process.Input { Trace = trace }).ExecuteAsync(cancellation.Token).AsTask();
        cancellation.Cancel();
        try
        {
            await running;
            return ""not cancelled"";
        }
        catch (OperationCanceledException)
        {
            return trace.Cleaned + ""|"" + trace.Entries.Count;
        }
    }
}";
            Assert.That(await Run(source), Is.EqualTo("True|0"));
        }

        [TestCase("new StepDescription(typeof(SourceStep)), new StepDescription(typeof(SourceStep))", "SPG003")]
        [TestCase("new StepDescription(typeof(SourceStep)) { Id = \"a\" }, new StepDescription(typeof(SourceStep))", "SPG003")]
        [TestCase("new StepDescription(typeof(FailStep)) { Id = \"same\" }, new StepDescription(typeof(FinishStep)) { Id = \"same\" }", "SPG003")]
        [TestCase("new StepDescription(typeof(FinishStep)) { After = { \"missing\" } }", "SPG004")]
        [TestCase("new StepDescription(typeof(FinishStep)) { After = { typeof(FinishStep) } }", "SPG008")]
        [TestCase("new StepDescription(typeof(FinishStep)) { After = { typeof(FailStep) } }, new StepDescription(typeof(FailStep)) { After = { typeof(FinishStep) } }", "SPG008")]
        [TestCase("new StepDescription(typeof(SourceStep)) { Optional = true }, new StepDescription(typeof(ConsumeStep))", "SPG007")]
        [TestCase("new StepDescription(typeof(SourceStep)) { Inputs = { [\"typo\"] = \"seed\" } }", "SPG009")]
        [TestCase("new StepDescription(typeof(SourceStep)) { Id = \"a\" }, new StepDescription(typeof(SourceStep)) { Id = \"b\" }", "SPG005")]
        [TestCase("new StepDescription(typeof(SourceStep)) { Thread = (StepThread)7 }", "SPG001")]
        public void ReportsDeclarationErrors(string steps, string diagnostic)
        {
            var generated = Generate("public partial class Process : LinearProcess\n{\npublic static readonly StepDescription[] Steps = { "
                + steps + " };\npublic struct Output : IProcessOutput\n{\n}\n}");
            Assert.That(generated.Diagnostics.Select(item => item.Id), Does.Contain(diagnostic));
        }

        [Test]
        public void RejectsRequiredProcessOutputFromOptionalStep()
        {
            var generated = Generate(@"
public partial class Process : LinearProcess
{
    public static readonly StepDescription[] Steps = { new StepDescription(typeof(SourceStep)) { Optional = true } };
    public struct Output : IProcessOutput
    {
        [Data(""value"")] public string Value;
    }
}");
            Assert.That(generated.Diagnostics.Select(item => item.Id), Does.Contain("SPG007"));
        }

        [Test]
        public void RejectsComputedStepList()
        {
            var generated = Generate(@"
public partial class Process : LinearProcess
{
    public static readonly StepDescription[] Steps = BuildSteps();
    public struct Output : IProcessOutput
    {
    }
    private static StepDescription[] BuildSteps()
    {
        return Array.Empty<StepDescription>();
    }
}");
            Assert.That(generated.Diagnostics.Select(item => item.Id), Does.Contain("SPG001"));
        }

        [Test]
        public void EmptyProcessCompilesWithoutAsyncWarnings()
        {
            var generated = Generate(@"
public partial class Process : LinearProcess
{
    public static readonly StepDescription[] Steps = { };
    public struct Output : IProcessOutput
    {
    }
}");
            Assert.That(generated.Diagnostics, Is.Empty);
            Assert.That(generated.Compilation.GetDiagnostics().Where(item => item.Severity >= DiagnosticSeverity.Warning), Is.Empty);
        }

        [Test]
        public void DoesNotRewriteDirectiveTextInsideDataIds()
        {
            var generated = Generate(@"
public partial class Process : LinearProcess
{
    public static readonly StepDescription[] Steps =
    {
        new StepDescription(typeof(SourceStep))
        {
            Inputs = { [""seed""] = ""#nullable disable"" }
        }
    };
    public struct Output : IProcessOutput
    {
    }
}");
            Assert.That(generated.Diagnostics, Is.Empty);
            Assert.That(generated.Compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error), Is.Empty);
            Assert.That(generated.Compilation.SyntaxTrees.Last().ToString(), Does.Contain("Data(\"#nullable disable\")"));
        }

        [Test]
        public void UsesReadableNamesWhenTheyAreUnambiguous()
        {
            var generated = Generate(@"
public partial class Process : ParallelProcess
{
    public static readonly StepDescription[] Steps =
    {
        new StepDescription(typeof(SourceStep)) { Thread = StepThread.Any },
        new StepDescription(typeof(ConsumeStep))
    };
    public struct Output : IProcessOutput
    {
        [Data(""text"")] public string Text;
    }
}");
            Assert.That(generated.Diagnostics, Is.Empty);
            Assert.That(generated.Compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error), Is.Empty);
            string emitted = generated.Compilation.SyntaxTrees.Last().ToString();
            Assert.That(emitted, Does.Contain("new SourceStep.Input"));
            Assert.That(emitted, Does.Contain("Task<StepResult<SourceStep.Output>>"));
            Assert.That(emitted, Does.Contain("Task.WhenAny(running)"));
            Assert.That(emitted, Does.Contain("__processInput.Seed"));
            Assert.That(emitted, Does.Not.Contain("@Seed"));
            Assert.That(emitted, Does.Not.Contain("global::Fixture.SourceStep"));
        }

        [Test]
        public void KeepsQualificationWhenUserMembersShadowFrameworkNames()
        {
            var generated = Generate(@"
public partial class Process : ParallelProcess
{
    public static object Task;
    public class CancellationToken
    {
    }
    public class StepResult<T>
    {
    }
    public static readonly StepDescription[] Steps =
    {
        new StepDescription(typeof(SourceStep)) { Thread = StepThread.Any }
    };
    public struct Output : IProcessOutput
    {
        [Data(""value"")] public string Value;
    }
}");
            Assert.That(generated.Diagnostics, Is.Empty);
            Assert.That(generated.Compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error), Is.Empty);
            string emitted = generated.Compilation.SyntaxTrees.Last().ToString();
            Assert.That(emitted, Does.Not.Contain("await Task.WhenAny"));
        }

        [Test]
        public async Task PreservesEscapingForKeywordIdentifiers()
        {
            string source = @"
public sealed class KeywordStep : IProcessStep<KeywordStep.Output>
{
    private readonly Input _input;
    public struct Input : IProcessInput
    {
        public string @class;
    }
    public struct Output : IProcessOutput
    {
        public string @namespace;
    }
    public KeywordStep(Input input)
    {
        _input = input;
    }
    public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
    {
        return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { @namespace = _input.@class }));
    }
}
public partial class @event : LinearProcess
{
    public static readonly StepDescription[] Steps = { new StepDescription(typeof(KeywordStep)) };
    public struct Output : IProcessOutput
    {
        public string @namespace;
    }
}
public static class Entry
{
    public static async Task<string> Run()
    {
        var result = await new @event(new @event.Input { Class = ""ok"" }).ExecuteAsync();
        return result.Output.@namespace;
    }
}";
            Assert.That(await Run(source), Is.EqualTo("ok"));
        }

        [Test]
        public void ReadsStepContractsFromReferencedAssembly()
        {
            var options = new CSharpParseOptions(LanguageVersion.CSharp9);
            var dependency = CSharpCompilation.Create("SharedSteps_" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(Header + "\n}", options) }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var dependencyResult = dependency.Emit(stream);
            Assert.That(dependencyResult.Success, Is.True, string.Join("\n", dependencyResult.Diagnostics));
            string source = @"
using Fixture;
using Tools.StructuredProcess.Declarative;
public partial class Process : LinearProcess
{
    public static readonly StepDescription[] Steps =
    {
        new(typeof(ConsumeStep)),
        new(typeof(SourceStep))
    };
    public struct Output : IProcessOutput
    {
        [Data(""text"")] public string Text;
    }
}";
            var compilation = CSharpCompilation.Create("Consumer", new[] { CSharpSyntaxTree.ParseText(source, options) },
                References.Append(MetadataReference.CreateFromImage(stream.ToArray())),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new ProcessGenerator().AsSourceGenerator() }, parseOptions: options);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var diagnostics);
            Assert.That(diagnostics, Is.Empty);
            Assert.That(generated.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error), Is.Empty);
        }

        [Test]
        public void DefaultResultIsFailureAndCannotExposeOutput()
        {
            var result = default(StepResult<EmptyOutput>);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("uninitialized"));
            Assert.Throws<InvalidOperationException>(() =>
            {
                var output = result.Output;
            });
        }

        private static async Task<string> Run(string source)
        {
            var generated = Generate(source);
            Assert.That(generated.Diagnostics, Is.Empty);
            using var stream = new MemoryStream();
            var emitted = generated.Compilation.Emit(stream);
            Assert.That(emitted.Success, Is.True, string.Join("\n", emitted.Diagnostics)
                + "\n" + string.Join("\n", generated.Compilation.SyntaxTrees.Skip(1)));
            var assembly = Assembly.Load(stream.ToArray());
            var method = assembly.GetType("Fixture.Entry").GetMethod("Run");
            return await (Task<string>)method.Invoke(null, null);
        }

        private static Generated Generate(string source)
        {
            var options = new CSharpParseOptions(LanguageVersion.CSharp9);
            var syntax = CSharpSyntaxTree.ParseText(Header + source + "\n}", options);
            var compilation = CSharpCompilation.Create("Fixture_" + Guid.NewGuid().ToString("N"),
                new[] { syntax }, References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new ProcessGenerator().AsSourceGenerator() }, parseOptions: options);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            return new Generated { Compilation = output, Diagnostics = diagnostics.ToArray() };
        }

        public struct EmptyOutput : IProcessOutput
        {
        }

        private sealed class Generated
        {
            internal Compilation Compilation;
            internal Diagnostic[] Diagnostics;
        }
    }
}
