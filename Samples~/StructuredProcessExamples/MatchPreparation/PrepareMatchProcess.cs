using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    public partial class PrepareMatchProcess : ParallelProcess
    {
        public const string Player = "Player";
        public const string Opponent = "Opponent";

        public static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(CreateMatchArenaStep))
            {
                // Navigation must be registered before creating the arena; no navigation value is consumed here.
                After = { typeof(BuildMatchNavigationStep) }
            },
            new StepDescription(typeof(LoadMatchSettingsStep)),
            new StepDescription(typeof(AuthenticateMatchStep)),
            new StepDescription(typeof(FetchMatchCatalogStep)),
            new StepDescription(typeof(FetchMatchProfileStep))
            {
                Id = Player,
                Inputs = { ["account-id"] = "player-id" },
                Outputs = { ["profile"] = "player-profile" },
                After = { typeof(FetchMatchCatalogStep) }
            },
            new StepDescription(typeof(FetchMatchProfileStep))
            {
                Id = Opponent,
                Inputs = { ["account-id"] = "opponent-id" },
                Outputs = { ["profile"] = "opponent-profile" },
                // The simulated profile backend allows one request at a time.
                After = { Player }
            },
            new StepDescription(typeof(ValidateMatchCatalogStep)) { Thread = StepThread.Any },
            new StepDescription(typeof(FetchMatchInventoryStep))
            {
                After = { typeof(FetchMatchEntitlementsStep) }
            },
            new StepDescription(typeof(LoadSavedLoadoutStep))
            {
                // This local cache is populated by the inventory request.
                After = { typeof(FetchMatchInventoryStep) }
            },
            new StepDescription(typeof(FetchMatchEntitlementsStep)) { After = { Player } },
            new StepDescription(typeof(ResolveMatchLoadoutStep)) { Thread = StepThread.Any },
            new StepDescription(typeof(ResolveOpponentBuildStep)) { Thread = StepThread.Any },
            new StepDescription(typeof(SelectMatchArenaStep)) { Thread = StepThread.Any },
            new StepDescription(typeof(DownloadMatchArenaStep)),
            new StepDescription(typeof(LoadMatchAudioStep)),
            new StepDescription(typeof(BuildMatchNavigationStep))
            {
                Thread = StepThread.Any,
                // Finish audio streaming before using its shared temporary storage for navigation.
                After = { typeof(LoadMatchAudioStep) }
            },
            new StepDescription(typeof(BakeMatchPresentationStep)) { Thread = StepThread.Any },
            new StepDescription(typeof(SpawnMatchParticipantsStep)),
            new StepDescription(typeof(ActivateMatchStep)),
            new StepDescription(typeof(ReportMatchReadyStep))
            {
                Optional = true,
                Thread = StepThread.Any,
                After = { typeof(ActivateMatchStep) }
            }
        };

        public struct Output : IProcessOutput
        {
            public MatchReady Ready;
            [Data("navigation")] public WorkProduct Navigation;
            [Data("loadout")] public WorkProduct Loadout;
            [Data("player-profile")] public WorkProduct PlayerProfile;
            [Data("opponent-profile")] public WorkProduct OpponentProfile;
        }
    }
}
