using System.Threading;
using System.Threading.Tasks;

namespace Tools.Samples.StructuredProcess
{
    public sealed class PlayerProfile
    {
        public string DisplayName { get; }

        public PlayerProfile(string displayName)
        {
            DisplayName = displayName;
        }
    }

    public readonly struct ProfilePair
    {
        public PlayerProfile Player { get; }
        public PlayerProfile Opponent { get; }

        public ProfilePair(PlayerProfile player, PlayerProfile opponent)
        {
            Player = player;
            Opponent = opponent;
        }
    }

    public interface IProfileSource
    {
        ValueTask<PlayerProfile> LoadAsync(string accountId, CancellationToken cancellationToken);
    }

    public interface IProfileView
    {
        void Show(ProfilePair profiles);
    }

    public interface IProfileAnalytics
    {
        ValueTask ReportAsync(CancellationToken cancellationToken);
    }
}
