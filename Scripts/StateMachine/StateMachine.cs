using System;
using System.Collections.Generic;
using Logging;
using StateMachine.Tools.StateMachine;

namespace StateMachine
{
	public class StateMachine : IDisposable
	{
		private readonly Dictionary<int, StateBase> _statesById;

		private StateBase _currentState;

		public StateBase CurrentState => _currentState;

		public StateMachine(StateBase[] states, int initialStateId, IStatePayload initialStatePayload = null)
		{
			_statesById = new Dictionary<int, StateBase>();
			foreach (var state in states)
			{
				_statesById.Add(state.StateId, state);
				state.StateChangeRequested += OnStateChangeRequested;
			}

			SelectState(initialStateId, initialStatePayload);
		}
		
		public void Dispose()
		{
			if (_currentState != null)
			{
				_currentState.Exit();
				_currentState = null;
			}
			
			foreach (var state in _statesById.Values)
			{
				state.StateChangeRequested -= OnStateChangeRequested;
			}
		}

		public void SelectState(int stateId, IStatePayload payload = null)
		{
			if (!_statesById.TryGetValue(stateId, out StateBase chosenState))
			{
				throw new ArgumentException($"Invalid state id: {stateId}");
			}

			if (_currentState != null)
			{
				_currentState.Exit();
				_currentState = null;
			}

			_currentState = chosenState;
			chosenState.Enter(payload);
		}

		protected virtual void OnStateChangeRequested(in StateTransferInfo info)
		{
			Log.Default.Info($"GameState change from {info.FromState} to {info.ToState} requested.");

			if (_currentState != null && _currentState.StateId == info.ToState)
			{
				Log.Default.Error($"GameState trying to change to itself stateId: {info.ToState}.");

				return;
			}

			SelectState(info.ToState, info.Payload);
		}
	}
}
