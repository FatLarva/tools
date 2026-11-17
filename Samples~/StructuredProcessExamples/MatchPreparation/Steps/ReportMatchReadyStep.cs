using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class ReportMatchReadyStep : IProcessStep<ReportMatchReadyStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            public MatchSession Session;
            [Data("participants")] public WorkProduct Participants;
        }

        public struct Output : IProcessOutput
        {
        }

        public ReportMatchReadyStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Work.RunCpu(nameof(ReportMatchReadyStep), cancellationToken, _input.Session.Value, _input.Participants);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(default));
        }
    }
}
