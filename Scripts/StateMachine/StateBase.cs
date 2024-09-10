using StateMachine.Tools.StateMachine;

namespace StateMachine
{
	public abstract class StateBase
	{
		public abstract int StateId { get; }

		public abstract event StateChangeRequest StateChangeRequested;

		public abstract void Enter(IStatePayload payload);

		public abstract void Exit();
	}
}
