using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class BuildMatchNavigationStep : IProcessStep<BuildMatchNavigationStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("arena-data")] public WorkProduct Arena;
            [Data("loadout")] public WorkProduct Loadout;
        }

        public struct Output : IProcessOutput
        {
            [Data("navigation")] public WorkProduct Navigation;
        }

        public BuildMatchNavigationStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = _input.Work.RunCpu(nameof(BuildMatchNavigationStep), cancellationToken, _input.Arena, _input.Loadout);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Navigation = product }));
        }
    }
}
