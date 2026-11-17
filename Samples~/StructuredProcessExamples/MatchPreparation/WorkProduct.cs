namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public sealed class WorkProduct
    {
        public string Origin { get; }
        public int Fingerprint { get; }

        public WorkProduct(string origin, int fingerprint)
        {
            Origin = origin;
            Fingerprint = fingerprint;
        }
    }

    public readonly struct MatchSettings
    {
        public WorkProduct Value { get; }

        public MatchSettings(WorkProduct value)
        {
            Value = value;
        }
    }

    public readonly struct MatchSession
    {
        public WorkProduct Value { get; }

        public MatchSession(WorkProduct value)
        {
            Value = value;
        }
    }

    public readonly struct MatchReady
    {
        public WorkProduct Value { get; }

        public MatchReady(WorkProduct value)
        {
            Value = value;
        }
    }
}
