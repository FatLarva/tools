using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class SelectMatchArenaStep : IProcessStep<SelectMatchArenaStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            public MatchSettings Settings;
            [Data("loadout")] public WorkProduct Loadout;
            [Data("opponent-build")] public WorkProduct Opponent;
        }

        public struct Output : IProcessOutput
        {
            [Data("arena-selection")] public WorkProduct Selection;
        }

        public SelectMatchArenaStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = _input.Work.RunCpu(nameof(SelectMatchArenaStep), cancellationToken, _input.Settings.Value, _input.Loadout, _input.Opponent);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Selection = product }));
        }
    }
}
