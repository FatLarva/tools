namespace Tools.StructuredProcess.Simple
{
    public interface IStepsLedger
    {
        void SetStepCompleted(IProcessStep processStep);
        bool IsStepCompleted(IProcessStep processStep);
    }
}