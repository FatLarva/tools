using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess
{
    public sealed class LoadProfileStep : IProcessStep<LoadProfileStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public IProfileSource Source;
            public ILog Log;

            [Data("account-id")]
            public string AccountId;

            [Data("fallback")]
            public PlayerProfile Fallback;
        }

        public struct Output : IProcessOutput
        {
            [Data("profile")]
            public PlayerProfile Profile;
        }

        public LoadProfileStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            PlayerProfile profile;
            try
            {
                profile = await _input.Source.LoadAsync(_input.AccountId, cancellationToken);
            }
            catch (IOException exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _input.Log.Warning($"Profile '{_input.AccountId}' is unavailable: {exception.Message}. Using fallback.");
                profile = _input.Fallback;
            }

            return StepResult<Output>.Success(new Output { Profile = profile });
        }
    }
}
