using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class BakeMatchPresentationStep : IProcessStep<BakeMatchPresentationStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("loadout")] public WorkProduct Loadout;
            [Data("opponent-build")] public WorkProduct Opponent;
            [Data("arena-data")] public WorkProduct Arena;
        }

        public struct Output : IProcessOutput
        {
            [Data("presentation")] public WorkProduct Presentation;
        }

        public BakeMatchPresentationStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = _input.Work.RunCpu(nameof(BakeMatchPresentationStep), cancellationToken, _input.Loadout, _input.Opponent, _input.Arena);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Presentation = product }));
        }
    }
}
