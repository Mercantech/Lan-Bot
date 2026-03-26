namespace LanBot.Domain.Brackets;

public static class BracketGenerator
{
    public static IReadOnlyList<IReadOnlyList<T>> Seed<T>(
        IReadOnlyList<T> entrants,
        int playersPerMatch,
        bool randomize,
        int? seed = null)
    {
        if (playersPerMatch < 2)
            throw new ArgumentOutOfRangeException(nameof(playersPerMatch));

        if (entrants.Count == 0)
            return Array.Empty<IReadOnlyList<T>>();

        var list = entrants.ToList();
        if (randomize)
            Shuffle(list, seed);

        var matches = new List<IReadOnlyList<T>>();
        for (var i = 0; i < list.Count; i += playersPerMatch)
        {
            var chunk = list.Skip(i).Take(playersPerMatch).ToArray();
            matches.Add(chunk);
        }

        return matches;
    }

    private static void Shuffle<T>(IList<T> list, int? seed)
    {
        var rng = seed is null ? Random.Shared : new Random(seed.Value);
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

