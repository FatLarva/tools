namespace StateMachine
{
    public interface IStateTransitionSink<TId> where TId : struct
    {
        void RequestTransition(TId destination, IStateOutput output);
    }
}
