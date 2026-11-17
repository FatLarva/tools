namespace StateMachine
{
    public interface IStateTransitionResolver<TId> where TId : struct
    {
        IStatePayload Resolve(TId source, TId destination, IStateOutput output);
    }
}
