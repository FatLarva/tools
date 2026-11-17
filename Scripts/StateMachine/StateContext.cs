using System.Threading;

namespace StateMachine
{
    /// <summary>Belongs to one entry. Capture this value in asynchronous operations.</summary>
    public readonly struct StateContext<TId, TOutput>
        where TId : struct
        where TOutput : IStateOutput
    {
        private readonly IStateTransitionSink<TId> _transitions;

        public CancellationToken Cancellation { get; }

        internal StateContext(CancellationToken cancellation, IStateTransitionSink<TId> transitions)
        {
            Cancellation = cancellation;
            _transitions = transitions;
        }

        /// <summary>Requests the next state. Each entry makes at most one request.</summary>
        public void RequestTransition(TId destination, TOutput output)
        {
            _transitions.RequestTransition(destination, output);
        }
    }
}
