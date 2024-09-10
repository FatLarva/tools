using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Logging;
using StateMachine.Tools.StateMachine;

namespace StateMachine
{
	public class StateMachineAsync : IAsyncDisposable
	{
		private readonly Dictionary<int, StateBaseAsync> _statesById;

		private StateBaseAsync _currentState;

		public StateBaseAsync CurrentState => _currentState;

		public StateMachineAsync(StateBaseAsync[] states)
		{
			_statesById = new Dictionary<int, StateBaseAsync>();
			foreach (var state in states)
			{
				_statesById.Add(state.StateId, state);
				state.StateChangeRequestedAsync += OnStateChangeRequestedAsync;
			}
		}
		
		public async ValueTask DisposeAsync()
		{
			foreach (var state in _statesById.Values)
			{
				state.StateChangeRequestedAsync -= OnStateChangeRequestedAsync;
			}
			
			if (_currentState != null)
			{
				await _currentState.ExitAsync();
				_currentState = null;
			}
		}

		public async ValueTask InitAsync(int initialStateId, IStatePayload initialStatePayload = null)
		{
			await SelectStateAsync(initialStateId, initialStatePayload);
		}

		public async ValueTask SelectStateAsync(int stateId, IStatePayload payload = null)
		{
			if (!_statesById.TryGetValue(stateId, out StateBaseAsync chosenState))
			{
				throw new ArgumentException($"Invalid state id: {stateId}");
			}

			if (_currentState != null)
			{
				await _currentState.ExitAsync();
				_currentState = null;
			}

			_currentState = chosenState;
			await chosenState.EnterAsync(payload);
		}

		protected virtual async ValueTask OnStateChangeRequestedAsync(StateTransferInfo info)
		{
			Log.Default.Info($"GameState change from {info.FromState} to {info.ToState} requested.");

			if (_currentState != null && _currentState.StateId == info.ToState)
			{
				Log.Default.Error($"GameState trying to change to itself stateId: {info.ToState}.");

				return;
			}

			await SelectStateAsync(info.ToState, info.Payload);
		}
	}
}
