using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class SpawnMatchParticipantsStep : IProcessStep<SpawnMatchParticipantsStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("arena-handle")] public WorkProduct Arena;
            [Data("loadout")] public WorkProduct Loadout;
            [Data("opponent-build")] public WorkProduct Opponent;
        }

        public struct Output : IProcessOutput
        {
            [Data("participants")] public WorkProduct Participants;
        }

        public SpawnMatchParticipantsStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(SpawnMatchParticipantsStep), cancellationToken, _input.Arena, _input.Loadout, _input.Opponent);
            return StepResult<Output>.Success(new Output { Participants = product });
        }
    }
}
