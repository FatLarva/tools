using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class LoadSavedLoadoutStep : IProcessStep<LoadSavedLoadoutStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("player-profile")] public WorkProduct Player;
            [Data("catalog")] public WorkProduct Catalog;
        }

        public struct Output : IProcessOutput
        {
            [Data("saved-loadout")] public WorkProduct Saved;
        }

        public LoadSavedLoadoutStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(LoadSavedLoadoutStep), cancellationToken, _input.Player, _input.Catalog);
            return StepResult<Output>.Success(new Output { Saved = product });
        }
    }
}
