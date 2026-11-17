using System;
using System.Collections.Generic;
using System.Threading;
using Tools;

namespace StateMachine
{
    /// <summary>Runs synchronous states on the calling thread.</summary>
    public sealed class StateMachine<TId> : IDisposable, IStateTransitionSink<TId> where TId : struct
    {
        private readonly Dictionary<TId, StateBase<TId>> _states = new();
        private readonly IStateTransitionResolver<TId> _transitions;
        private StateBase<TId> _current;
        private CancellationTokenSource _entryCancellation;
        private Transition? _pending;
        private FlipBool _processing;
        private bool _started;
        private FlipBool _stopped;

        public StateBase<TId> CurrentState => _current;

        public event Action<TId> StateEntered;

        public StateMachine(IEnumerable<StateBase<TId>> states, IStateTransitionResolver<TId> transitions)
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));

            foreach (var state in states)
            {
                if (state == null || !_states.TryAdd(state.StateId, state))
                {
                    throw new ArgumentException("States must be non-null with unique IDs.", nameof(states));
                }
            }
        }

        public void Start(TId initialState, IStatePayload payload)
        {
            if (_started || _stopped)
            {
                throw new InvalidOperationException("The state machine can only start once.");
            }

            var destination = GetDestination(initialState, payload);
            _started = true;
            _pending = new Transition(destination, payload);
            ProcessTransitions();
        }

        public void Stop()
        {
            if (!_stopped.TrySet(true))
            {
                return;
            }

            _pending = null;

            if (_processing)
            {
                // Stop from Enter cancels its work without calling Exit inside Enter.
                _entryCancellation?.Cancel();
            }
            else
            {
                LeaveCurrent();
            }
        }

        public void Dispose()
        {
            Stop();
        }

        public void RequestTransition(TId destinationId, IStateOutput output)
        {
            if (_stopped)
            {
                throw new InvalidOperationException("The state machine has stopped.");
            }

            if (_current == null)
            {
                throw new InvalidOperationException("There is no active state.");
            }

            if (_pending.HasValue)
            {
                throw new InvalidOperationException("This entry has already requested its next transition.");
            }

            var payload = _transitions.Resolve(_current.StateId, destinationId, output);
            var destination = GetDestination(destinationId, payload);
            _pending = new Transition(destination, payload);
            ProcessTransitions();
        }

        private void ProcessTransitions()
        {
            if (!_processing.TrySet(true))
            {
                return;
            }

            try
            {
                while (!_stopped && _pending.HasValue)
                {
                    var next = _pending.Value;
                    _pending = null;
                    LeaveCurrent();

                    if (_stopped)
                    {
                        break;
                    }

                    _current = next.State;
                    _entryCancellation = new CancellationTokenSource();
                    var cancellation = _entryCancellation.Token;
                    var current = _current;
                    current.Enter(next.Payload, cancellation, this);

                    if (!_stopped)
                    {
                        StateEntered?.Invoke(current.StateId);
                    }
                }
            }
            catch
            {
                _stopped = true;
                _pending = null;
                throw;
            }
            finally
            {
                _processing = false;

                if (_stopped)
                {
                    LeaveCurrent();
                }
            }
        }

        private StateBase<TId> GetDestination(TId destinationId, IStatePayload payload)
        {
            if (!_states.TryGetValue(destinationId, out var destination))
            {
                throw new ArgumentException($"Unknown destination {destinationId}.", nameof(destinationId));
            }

            destination.ValidatePayload(payload);
            return destination;
        }

        private void LeaveCurrent()
        {
            if (_current == null)
            {
                return;
            }

            var previous = _current;
            var cancellation = _entryCancellation;
            _current = null;
            _entryCancellation = null;

            try
            {
                cancellation.Cancel();
            }
            finally
            {
                try
                {
                    previous.Exit();
                }
                finally
                {
                    cancellation.Dispose();
                }
            }
        }

        private readonly struct Transition
        {
            public StateBase<TId> State { get; }
            public IStatePayload Payload { get; }

            public Transition(StateBase<TId> state, IStatePayload payload)
            {
                State = state;
                Payload = payload;
            }
        }
    }
}
