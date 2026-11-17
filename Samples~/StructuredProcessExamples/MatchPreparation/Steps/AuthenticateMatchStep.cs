using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class AuthenticateMatchStep : IProcessStep<AuthenticateMatchStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
        }

        public struct Output : IProcessOutput
        {
            public MatchSession Session;
        }

        public AuthenticateMatchStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(AuthenticateMatchStep), cancellationToken);
            return StepResult<Output>.Success(new Output { Session = new MatchSession(product) });
        }
    }
}
