using LanBot.Domain.Brackets;

namespace LanBot.Domain.Tests;

public class UnitTest1
{
    [Fact]
    public void Normalize_CollapsesWhitespace_Uppercases()
    {
        var normalized = NameNormalization.Normalize("  Mathias   Jensen  ");
        Assert.Equal("MATHIAS JENSEN", normalized);
    }

    [Fact]
    public void Seed_GroupsIntoMatchesOfPlayersPerMatch()
    {
        var entrants = Enumerable.Range(1, 10).ToArray();
        var matches = BracketGenerator.Seed(entrants, playersPerMatch: 4, randomize: false);

        Assert.Equal(3, matches.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, matches[0]);
        Assert.Equal(new[] { 5, 6, 7, 8 }, matches[1]);
        Assert.Equal(new[] { 9, 10 }, matches[2]);
    }

    [Fact]
    public void Seed_WithSeed_IsDeterministic()
    {
        var entrants = Enumerable.Range(1, 20).ToArray();

        var a = BracketGenerator.Seed(entrants, playersPerMatch: 4, randomize: true, seed: 123);
        var b = BracketGenerator.Seed(entrants, playersPerMatch: 4, randomize: true, seed: 123);

        Assert.Equal(a.SelectMany(x => x), b.SelectMany(x => x));
    }
}
