using System.Collections.Concurrent;

namespace LanBot.Discord;

public sealed class MatchReportUiState
{
    private readonly ConcurrentDictionary<ulong, Draft> _drafts = new();

    public Draft Create(ulong messageId, Guid matchId, IReadOnlyList<ulong> playerIds)
    {
        var draft = new Draft(matchId, playerIds.ToList());
        _drafts[messageId] = draft;
        return draft;
    }

    public bool TryGet(ulong messageId, out Draft draft) => _drafts.TryGetValue(messageId, out draft!);

    public void Remove(ulong messageId) => _drafts.TryRemove(messageId, out _);

    public sealed record Draft(Guid MatchId, List<ulong> PlayerIds)
    {
        public ulong? First { get; set; }
        public ulong? Second { get; set; }
        public ulong? Third { get; set; }
        public ulong? Fourth { get; set; }
    }
}

