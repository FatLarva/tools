using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class LoadMatchAudioStep : IProcessStep<LoadMatchAudioStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            public MatchSettings Settings;
            [Data("arena-selection")] public WorkProduct Selection;
        }

        public struct Output : IProcessOutput
        {
            [Data("audio-bank")] public WorkProduct Audio;
        }

        public LoadMatchAudioStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(LoadMatchAudioStep), cancellationToken, _input.Settings.Value, _input.Selection);
            return StepResult<Output>.Success(new Output { Audio = product });
        }
    }
}
