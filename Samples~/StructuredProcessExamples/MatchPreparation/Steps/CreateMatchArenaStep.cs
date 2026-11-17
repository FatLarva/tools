using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class CreateMatchArenaStep : IProcessStep<CreateMatchArenaStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("arena-data")] public WorkProduct Arena;
            [Data("presentation")] public WorkProduct Presentation;
        }

        public struct Output : IProcessOutput
        {
            [Data("arena-handle")] public WorkProduct Arena;
        }

        public CreateMatchArenaStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(CreateMatchArenaStep), cancellationToken, _input.Arena, _input.Presentation);
            return StepResult<Output>.Success(new Output { Arena = product });
        }
    }
}
