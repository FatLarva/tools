using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class ActivateMatchStep : IProcessStep<ActivateMatchStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("arena-handle")] public WorkProduct Arena;
            [Data("participants")] public WorkProduct Participants;
            [Data("audio-bank")] public WorkProduct Audio;
        }

        public struct Output : IProcessOutput
        {
            public MatchReady Ready;
        }

        public ActivateMatchStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(ActivateMatchStep), cancellationToken, _input.Arena, _input.Participants, _input.Audio);
            return StepResult<Output>.Success(new Output { Ready = new MatchReady(product) });
        }
    }
}
