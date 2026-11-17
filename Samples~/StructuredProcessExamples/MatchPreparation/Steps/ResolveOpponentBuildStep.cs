using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class ResolveOpponentBuildStep : IProcessStep<ResolveOpponentBuildStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("opponent-profile")] public WorkProduct Opponent;
            [Data("validated-catalog")] public WorkProduct Catalog;
        }

        public struct Output : IProcessOutput
        {
            [Data("opponent-build")] public WorkProduct Opponent;
        }

        public ResolveOpponentBuildStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = _input.Work.RunCpu(nameof(ResolveOpponentBuildStep), cancellationToken, _input.Opponent, _input.Catalog);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Opponent = product }));
        }
    }
}
