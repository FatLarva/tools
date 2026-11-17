using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class ResolveMatchLoadoutStep : IProcessStep<ResolveMatchLoadoutStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public MatchPreparationWork Work;
            [Data("saved-loadout")] public WorkProduct Saved;
            [Data("inventory")] public WorkProduct Inventory;
            [Data("entitlements")] public WorkProduct Entitlements;
            [Data("validated-catalog")] public WorkProduct Catalog;
        }

        public struct Output : IProcessOutput
        {
            [Data("loadout")] public WorkProduct Loadout;
        }

        public ResolveMatchLoadoutStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var product = _input.Work.RunCpu(nameof(ResolveMatchLoadoutStep), cancellationToken, _input.Saved, _input.Inventory, _input.Entitlements, _input.Catalog);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Loadout = product }));
        }
    }
}
