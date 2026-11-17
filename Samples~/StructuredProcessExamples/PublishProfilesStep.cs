using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess
{
    public sealed class PublishProfilesStep : IProcessStep<PublishProfilesStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public IProfileView View;

            [Data("player-profile")]
            public PlayerProfile Player;

            [Data("opponent-profile")]
            public PlayerProfile Opponent;
        }

        public struct Output : IProcessOutput
        {
            public ProfilePair Profiles;
        }

        public PublishProfilesStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var profiles = new ProfilePair(_input.Player, _input.Opponent);
            _input.View.Show(profiles);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Profiles = profiles }));
        }
    }
}
