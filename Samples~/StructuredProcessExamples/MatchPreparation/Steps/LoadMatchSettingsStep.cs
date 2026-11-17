using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class LoadMatchSettingsStep : IProcessStep<LoadMatchSettingsStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
        }

        public struct Output : IProcessOutput
        {
            public MatchSettings Settings;
        }

        public LoadMatchSettingsStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(LoadMatchSettingsStep), cancellationToken);
            return StepResult<Output>.Success(new Output { Settings = new MatchSettings(product) });
        }
    }
}
