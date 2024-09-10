using StateMachine.Tools.StateMachine;

namespace StateMachine
{
	public readonly struct StateTransferInfo
	{
		public readonly int FromState;
		public readonly int ToState;
		public readonly IStatePayload Payload;

		public StateTransferInfo(int fromState, int toState, IStatePayload payload)
		{
			FromState = fromState;
			ToState = toState;
			Payload = payload;
		}
	}
}
