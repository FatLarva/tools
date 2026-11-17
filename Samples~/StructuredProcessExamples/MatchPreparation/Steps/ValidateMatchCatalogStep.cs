using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class ValidateMatchCatalogStep : IProcessStep<ValidateMatchCatalogStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("catalog")] public WorkProduct Catalog;
            public MatchSettings Settings;
        }

        public struct Output : IProcessOutput
        {
            [Data("validated-catalog")] public WorkProduct Catalog;
        }

        public ValidateMatchCatalogStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = _input.Work.RunCpu(nameof(ValidateMatchCatalogStep), cancellationToken, _input.Catalog, _input.Settings.Value);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Catalog = product }));
        }
    }
}
