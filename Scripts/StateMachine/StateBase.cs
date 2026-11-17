using System;
using System.Threading;

namespace StateMachine
{
    public abstract class StateBase<TId> where TId : struct
    {
        public abstract TId StateId { get; }

        internal abstract void ValidatePayload(IStatePayload payload);
        internal abstract void Enter(IStatePayload payload, CancellationToken cancellation,
            IStateTransitionSink<TId> transitions);
        internal abstract void Exit();
    }

    public abstract class StateBase<TId, TPayload, TOutput> : StateBase<TId>
        where TId : struct
        where TPayload : IStatePayload
        where TOutput : IStateOutput
    {
        protected abstract void OnEnter(TPayload payload, StateContext<TId, TOutput> context);
        protected abstract void OnExit();

        internal override void ValidatePayload(IStatePayload payload)
        {
            if (payload is not TPayload)
            {
                throw new ArgumentException($"State {StateId} expects {typeof(TPayload).FullName}.", nameof(payload));
            }
        }

        internal override void Enter(IStatePayload payload, CancellationToken cancellation,
            IStateTransitionSink<TId> transitions)
        {
            OnEnter((TPayload)payload, new StateContext<TId, TOutput>(cancellation, transitions));
        }

        internal override void Exit()
        {
            OnExit();
        }
    }
}
