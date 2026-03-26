using LanBot.Domain.Entities;
using System.Collections.Concurrent;

namespace LanBot.Discord;

public sealed class TournamentCreateFlowState
{
    private readonly ConcurrentDictionary<ulong, Draft> _drafts = new();

    public Draft Create(ulong messageId, string name)
    {
        var draft = new Draft
        {
            Name = name.Trim(),
            Progression = TournamentProgression.Bracket,
            IsTeamBased = false,
            RandomizeSeeds = true,
            PlayersPerMatch = 4,
            AdvanceCount = 2,
        };
        _drafts[messageId] = draft;
        return draft;
    }

    public bool TryGet(ulong messageId, out Draft draft) => _drafts.TryGetValue(messageId, out draft!);
    public void Remove(ulong messageId) => _drafts.TryRemove(messageId, out _);

    public sealed class Draft
    {
        public string Name { get; set; } = "";
        public TournamentProgression Progression { get; set; }
        public bool IsTeamBased { get; set; }
        public bool RandomizeSeeds { get; set; }
        public int PlayersPerMatch { get; set; }
        public int AdvanceCount { get; set; }
    }
}

