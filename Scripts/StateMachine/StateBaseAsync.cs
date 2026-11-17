using System;
using System.Threading;
using System.Threading.Tasks;

namespace StateMachine
{
    public abstract class StateBaseAsync<TId> where TId : struct
    {
        public abstract TId StateId { get; }

        internal abstract void ValidatePayload(IStatePayload payload);
        internal abstract ValueTask EnterAsync(IStatePayload payload, CancellationToken cancellation,
            IStateTransitionSink<TId> transitions);
        internal abstract ValueTask ExitAsync();
    }

    public abstract class StateBaseAsync<TId, TPayload, TOutput> : StateBaseAsync<TId>
        where TId : struct
        where TPayload : IStatePayload
        where TOutput : IStateOutput
    {
        protected abstract ValueTask OnEnterAsync(TPayload payload, StateContext<TId, TOutput> context);

        /// <summary>Await completion of all operations owned by this entry before returning.</summary>
        protected abstract ValueTask OnExitAsync();

        internal override void ValidatePayload(IStatePayload payload)
        {
            if (payload is not TPayload)
            {
                throw new ArgumentException($"State {StateId} expects {typeof(TPayload).FullName}.", nameof(payload));
            }
        }

        internal override ValueTask EnterAsync(IStatePayload payload, CancellationToken cancellation,
            IStateTransitionSink<TId> transitions)
        {
            var context = new StateContext<TId, TOutput>(cancellation, transitions);
            return OnEnterAsync((TPayload)payload, context);
        }

        internal override ValueTask ExitAsync()
        {
            return OnExitAsync();
        }
    }
}
