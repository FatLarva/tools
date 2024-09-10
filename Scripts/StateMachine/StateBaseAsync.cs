using System.Threading.Tasks;
using StateMachine.Tools.StateMachine;

namespace StateMachine
{
	public abstract class StateBaseAsync
	{
		public abstract int StateId { get; }

		public abstract event StateChangeRequestAsync StateChangeRequestedAsync;

		public abstract ValueTask EnterAsync(IStatePayload payload);

		public abstract ValueTask ExitAsync();
	}
}
