using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class FetchMatchCatalogStep : IProcessStep<FetchMatchCatalogStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            public MatchSettings Settings;
            public MatchSession Session;
        }

        public struct Output : IProcessOutput
        {
            [Data("catalog")] public WorkProduct Catalog;
        }

        public FetchMatchCatalogStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = await _input.Work.RunIoAsync(nameof(FetchMatchCatalogStep), cancellationToken, _input.Settings.Value, _input.Session.Value);
            return StepResult<Output>.Success(new Output { Catalog = product });
        }
    }
}
