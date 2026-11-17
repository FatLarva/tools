using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class FetchMatchProfileStep : IProcessStep<FetchMatchProfileStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            public MatchSession Session;
            [Data("account-id")] public string AccountId;
        }

        public struct Output : IProcessOutput
        {
            [Data("profile")] public WorkProduct Profile;
        }

        public FetchMatchProfileStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(_input.AccountId, cancellationToken, _input.Session.Value);
            return StepResult<Output>.Success(new Output { Profile = product });
        }
    }
}
