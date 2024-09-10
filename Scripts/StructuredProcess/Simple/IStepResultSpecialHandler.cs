namespace Tools.StructuredProcess.Simple
{
    public interface IStepResultSpecialHandler
    {
        bool CanHandle(IProcessStep step);
        void HandleStepResult(IProcessStep step, IStepResult stepResult);
    }
}