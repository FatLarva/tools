using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess
{
    public sealed class ReportProfilesStep : IProcessStep<ReportProfilesStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public IProfileAnalytics Analytics;
        }

        public struct Output : IProcessOutput
        {
        }

        public ReportProfilesStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            await _input.Analytics.ReportAsync(cancellationToken);
            return StepResult<Output>.Success(default);
        }
    }
}
