using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Tools;

namespace StateMachine
{
    /// <summary>Prepares a transition synchronously and supplies it to one awaiting runner.</summary>
    internal sealed class TransitionGatekeeper<TId> : IStateTransitionSink<TId>, IValueTaskSource<TransitionGatekeeper<TId>.Transition>
        where TId : struct
    {
        private readonly IReadOnlyDictionary<TId, StateBaseAsync<TId>> _states;
        private readonly IStateTransitionResolver<TId> _resolver;
        private ManualResetValueTaskSourceCore<Transition> _completion;
        private StateBaseAsync<TId> _source;
        private CancellationToken _cancellation;
        private readonly AtomicBool _completed = new(true);

        public TransitionGatekeeper(
            IReadOnlyDictionary<TId, StateBaseAsync<TId>> states,
            IStateTransitionResolver<TId> resolver)
        {
            _states = states;
            _resolver = resolver;
            _completion.RunContinuationsAsynchronously = true;
        }

        public void Begin(StateBaseAsync<TId> source, CancellationToken cancellation)
        {
            _completion.Reset();
            _source = source;
            _cancellation = cancellation;
            // Publish the active wait only after Reset and its entry data are ready.
            _completed.Set(false);

            // Cancellation may have happened between entries, when no wait was active.
            if (cancellation.IsCancellationRequested)
            {
                Cancel();
            }
        }

        public void End()
        {
            _completed.Set(true);
            _source = null;
        }

        public void RequestTransition(TId destination, IStateOutput output)
        {
            if (_source == null)
            {
                throw new InvalidOperationException("There is no active state.");
            }

            _cancellation.ThrowIfCancellationRequested();
            if (_completed)
            {
                // Cancellation can claim completion after the preceding token check.
                _cancellation.ThrowIfCancellationRequested();
                throw new InvalidOperationException("This entry has already requested its next transition.");
            }

            var payload = _resolver.Resolve(_source.StateId, destination, output);
            var state = GetState(destination, payload);

            _cancellation.ThrowIfCancellationRequested();
            if (!_completed.TryFlip(true))
            {
                // A lost claim may mean cancellation, not a duplicate request.
                _cancellation.ThrowIfCancellationRequested();
                throw new InvalidOperationException("This entry has already requested its next transition.");
            }

            _completion.SetResult(new Transition(state, payload));
        }

        public ValueTask<Transition> WaitAsync()
        {
            return new ValueTask<Transition>(this, _completion.Version);
        }

        public void Cancel()
        {
            // Only owner cancellation can publish from another thread; it also stops the run.
            if (!_completed.TryFlip(true))
            {
                // The runner still observes the canceled owner token after a successful result.
                return;
            }

            _completion.SetException(new OperationCanceledException(_cancellation));
        }

        public StateBaseAsync<TId> GetState(TId id, IStatePayload payload)
        {
            if (!_states.TryGetValue(id, out var state))
            {
                throw new ArgumentException($"Unknown state {id}.", nameof(id));
            }

            state.ValidatePayload(payload);
            return state;
        }

        Transition IValueTaskSource<Transition>.GetResult(short token)
        {
            var transition = _completion.GetResult(token);
            _cancellation.ThrowIfCancellationRequested();
            return transition;
        }

        ValueTaskSourceStatus IValueTaskSource<Transition>.GetStatus(short token)
        {
            return _completion.GetStatus(token);
        }

        void IValueTaskSource<Transition>.OnCompleted(
            Action<object> continuation,
            object state,
            short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
            _completion.OnCompleted(continuation, state, token, flags);
        }

        internal readonly struct Transition
        {
            public StateBaseAsync<TId> State { get; }
            public IStatePayload Payload { get; }

            public Transition(StateBaseAsync<TId> state, IStatePayload payload)
            {
                State = state;
                Payload = payload;
            }
        }
    }
}
