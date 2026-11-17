using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess
{
    public partial class PrepareProfilesProcess : LinearProcess
    {
        private const string Player = "Player";
        private const string Opponent = "Opponent";

        public static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(PublishProfilesStep)),
            new StepDescription(typeof(LoadProfileStep))
            {
                Id = Player,
                Inputs =
                {
                    ["account-id"] = "player-id"
                },
                Outputs =
                {
                    ["profile"] = "player-profile"
                }
            },
            new StepDescription(typeof(LoadProfileStep))
            {
                Id = Opponent,
                Inputs =
                {
                    ["account-id"] = "opponent-id"
                },
                Outputs =
                {
                    ["profile"] = "opponent-profile"
                }
            },
            new StepDescription(typeof(ReportProfilesStep))
            {
                Optional = true,
                Thread = StepThread.Any,
                After =
                {
                    typeof(PublishProfilesStep)
                }
            }
        };

        public struct Output : IProcessOutput
        {
            public ProfilePair Profiles;
        }
    }
}
