using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StateMachine
{
    /// <summary>Runs states sequentially on the application's synchronization context.</summary>
    public sealed class StateMachineAsync<TId> : IStateTransitionSink<TId> where TId : struct
    {
        private readonly TransitionGatekeeper<TId> _transitionGatekeeper;
        private StateBaseAsync<TId> _currentState;

        public StateBaseAsync<TId> CurrentState => _currentState;

        public event Action<TId> StateEntered;

        public StateMachineAsync(IEnumerable<StateBaseAsync<TId>> states, IStateTransitionResolver<TId> transitions)
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            var stateLookup = new Dictionary<TId, StateBaseAsync<TId>>();
            foreach (var state in states)
            {
                if (state == null || !stateLookup.TryAdd(state.StateId, state))
                {
                    throw new ArgumentException("States must be non-null and have unique IDs.", nameof(states));
                }
            }

            _transitionGatekeeper = new TransitionGatekeeper<TId>(
                stateLookup,
                transitions ?? throw new ArgumentNullException(nameof(transitions)));
        }

        /// <summary>Completes after stopping and cleanup; lifecycle failures propagate to the caller.</summary>
        public async Task RunAsync(TId initialState, IStatePayload payload, CancellationToken cancellation)
        {
            var state = _transitionGatekeeper.GetState(initialState, payload);
            await using var registration = cancellation.Register(_transitionGatekeeper.Cancel);

            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                using var stateLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                _transitionGatekeeper.Begin(state, cancellation);
                _currentState = state;

                try
                {
                    await state.EnterAsync(payload, stateLifetime.Token, _transitionGatekeeper);
                    cancellation.ThrowIfCancellationRequested();
                    StateEntered?.Invoke(state.StateId);
                    var next = await _transitionGatekeeper.WaitAsync();
                    state = next.State;
                    payload = next.Payload;
                }
                finally
                {
                    var previous = _currentState;
                    _transitionGatekeeper.End();
                    _currentState = null;
                    try
                    {
                        stateLifetime.Cancel();
                    }
                    finally
                    {
                        await previous.ExitAsync();
                    }
                }
            }
        }

        /// <summary>Requests a transition from the active state, using that state's output contract.</summary>
        public void RequestTransition(TId destination, IStateOutput output)
        {
            _transitionGatekeeper.RequestTransition(destination, output);
        }
    }
}