using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class DownloadMatchArenaStep : IProcessStep<DownloadMatchArenaStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("arena-selection")] public WorkProduct Selection;
            public MatchSession Session;
        }

        public struct Output : IProcessOutput
        {
            [Data("arena-data")] public WorkProduct Arena;
        }

        public DownloadMatchArenaStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(DownloadMatchArenaStep), cancellationToken, _input.Selection, _input.Session.Value);
            return StepResult<Output>.Success(new Output { Arena = product });
        }
    }
}
