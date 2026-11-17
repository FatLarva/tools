using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class FetchMatchEntitlementsStep : IProcessStep<FetchMatchEntitlementsStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            public MatchSession Session;
            public MatchSettings Settings;
        }

        public struct Output : IProcessOutput
        {
            [Data("entitlements")] public WorkProduct Entitlements;
        }

        public FetchMatchEntitlementsStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(FetchMatchEntitlementsStep), cancellationToken, _input.Session.Value, _input.Settings.Value);
            return StepResult<Output>.Success(new Output { Entitlements = product });
        }
    }
}
