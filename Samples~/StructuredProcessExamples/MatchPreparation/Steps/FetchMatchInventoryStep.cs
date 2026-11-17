using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class FetchMatchInventoryStep : IProcessStep<FetchMatchInventoryStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("player-profile")] public WorkProduct Player;
            public MatchSession Session;
        }

        public struct Output : IProcessOutput
        {
            [Data("inventory")] public WorkProduct Inventory;
        }

        public FetchMatchInventoryStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(FetchMatchInventoryStep), cancellationToken, _input.Player, _input.Session.Value);
            return StepResult<Output>.Success(new Output { Inventory = product });
        }
    }
}
