using System.Globalization;
using System.Linq;

namespace Tools.StructuredProcess.Generator
{
    internal sealed partial class ProcessEmitter
    {
        private const string TaskType = "global::System.Threading.Tasks.Task";

        private void WriteParallelExecution(ProcessModel process)
        {
            string resultType = Runtime + "StepResult<Output>";
            Open("public async global::System.Threading.Tasks.ValueTask<" + resultType
                + "> ExecuteAsync(global::System.Threading.CancellationToken cancellationToken = default)");
            Line("cancellationToken.ThrowIfCancellationRequested();");
            Line("using var cancellation = global::System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);");
            Line("var token = cancellation.Token;");
            Line("var running = new global::System.Collections.Generic.List<" + TaskType + ">(" + process.Order.Count + ");");
            Line("var completed = new bool[" + process.Order.Count + "];");
            Line("int completedCount = 0;");
            Line(resultType + " failure = default;");
            Line("bool failed = false;");
            foreach (var step in process.Order)
            {
                string index = Index(step);
                Line(TaskType + "<" + Runtime + "StepResult<" + Names.Full(step.OutputType) + ">> task" + index + " = null;");
                if (HasConsumers(step, process))
                {
                    Line(Names.Full(step.OutputType) + " output" + index + " = default;");
                }
            }

            Open("try");
            Open("while (completedCount < " + process.Order.Count + ")");
            Line("token.ThrowIfCancellationRequested();");
            Line("// Observe all available completions before starting more work.");
            foreach (var step in process.Order)
            {
                WriteCompletion(step, process);
            }

            Line("token.ThrowIfCancellationRequested();");
            Open("if (failed)");
            Line("return failure;");
            Close();
            foreach (var step in process.Order)
            {
                WriteStart(step);
            }

            Open("if (running.Count != 0)");
            Line("await " + TaskType + ".WhenAny(running);");
            Close();
            Close();
            Line("token.ThrowIfCancellationRequested();");
            Line("return " + resultType + ".Success(new Output");
            Line("{");
            _indent++;
            foreach (var binding in process.OutputBindings)
            {
                Line("@" + binding.Target.Name + " = " + Value(binding) + ",");
            }

            _indent--;
            Line("});");
            Close();
            Open("finally");
            Open("if (running.Count != 0)");
            Open("try");
            Line("cancellation.Cancel();");
            Close();
            Open("finally");
            Open("try");
            Line("await " + TaskType + ".WhenAll(running);");
            Close();
            Open("catch (global::System.OperationCanceledException) when (token.IsCancellationRequested)");
            Line("// All tasks have settled; cancellation of remaining steps is expected.");
            Close();
            Close();
            foreach (var step in process.Order)
            {
                string index = Index(step);
                Open("if (task" + index + " != null && !completed[" + index + "] && task" + index
                    + ".Status == global::System.Threading.Tasks.TaskStatus.RanToCompletion)");
                Line("var result = await task" + index + ";");
                Open("if (!result.IsSuccess)");
                WriteFailureReport(step);
                Close();
                Close();
            }

            Close();
            Close();
            Close();

            foreach (var step in process.Order)
            {
                WriteStepRunner(step);
            }
        }

        private void WriteCompletion(StepModel step, ProcessModel process)
        {
            string index = Index(step);
            Open("if (task" + index + " != null && !completed[" + index + "] && task" + index + ".IsCompleted)");
            Line("var result = await task" + index + ";");
            Line("running.Remove(task" + index + ");");
            Line("completed[" + index + "] = true;");
            Line("completedCount++;");
            Open("if (!result.IsSuccess)");
            WriteFailureReport(step);
            if (!step.Optional)
            {
                Open("if (!failed)");
                Line("failure = " + Runtime + "StepResult<Output>.Failure(result.Error, result.Exception);");
                Line("failed = true;");
                Close();
            }

            Close();
            if (HasConsumers(step, process))
            {
                Open("else");
                Line("output" + index + " = result.Output;");
                Close();
            }

            Close();
        }

        private void WriteStart(StepModel step)
        {
            string index = Index(step);
            string dependencies = string.Concat(step.Dependencies.OrderBy(item => item.Index)
                .Select(item => " && completed[" + Index(item) + "]"));
            Open("if (task" + index + " == null" + dependencies + ")");
            Line("token.ThrowIfCancellationRequested();");
            Line("var input" + index + " = new " + Names.Full(step.InputType));
            Line("{");
            _indent++;
            foreach (var binding in step.Bindings)
            {
                Line("@" + binding.Target.Name + " = " + Value(binding) + ",");
            }

            _indent--;
            Line("};");
            string invocation = "__ExecuteStep" + index + "Async(input" + index + ", token)";
            Line("task" + index + " = " + (step.AnyThread ? TaskType + ".Run(() => " + invocation + ", token)" : invocation) + ";");
            Line("running.Add(task" + index + ");");
            Open("if (task" + index + ".IsCompleted)");
            Line("continue;");
            Close();
            Close();
        }

        private void WriteStepRunner(StepModel step)
        {
            Line();
            string resultType = Runtime + "StepResult<" + Names.Full(step.OutputType) + ">";
            Open("private static async " + TaskType + "<" + resultType + "> __ExecuteStep" + Index(step)
                + "Async(" + Names.Full(step.InputType) + " input, global::System.Threading.CancellationToken cancellationToken)");
            Open("try");
            Line("cancellationToken.ThrowIfCancellationRequested();");
            Line("var step = new " + Names.Full(step.Type) + "(input);");
            Line("return await step.ExecuteAsync(cancellationToken);");
            Close();
            Open("catch (global::System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)");
            Line("throw;");
            Close();
            Open("catch (global::System.Exception exception)");
            Line("return " + resultType + ".Failure(exception);");
            Close();
            Close();
        }

        private void WriteFailureReport(StepModel step)
        {
            string id = step.Id == null ? "typeof(" + Names.Full(step.Type) + ")" : Literal(step.Id);
            Line("ReportStepFailure(" + id + ", result.Error, result.Exception);");
        }

        private static bool HasConsumers(StepModel step, ProcessModel process)
        {
            return process.OutputBindings.Any(binding => binding.Producer == step)
                || process.Steps.Any(item => item.Bindings.Any(binding => binding.Producer == step));
        }

        private static string Index(StepModel step)
        {
            return step.Index.ToString(CultureInfo.InvariantCulture);
        }
    }
}
