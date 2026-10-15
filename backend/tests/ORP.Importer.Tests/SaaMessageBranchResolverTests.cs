using Xunit;

namespace ORP.Importer.Tests;

public sealed class SaaMessageBranchResolverTests
{
    [Theory]
    [InlineData("Paris")]
    [InlineData("PARIS")]
    [InlineData(" paris ")]
    public void ResolvesNamesIgnoringCaseAndSurroundingWhitespace(string branch)
    {
        var resolver = new SaaMessageBranchResolver(new[]
        {
            new KeyValuePair<string, int>(" Paris ", 7),
            new KeyValuePair<string, int>("London", 12)
        });
        Assert.Equal(7, resolver.ResolveBranchId(branch));
        Assert.Equal(12, resolver.ResolveBranchId("LONDON"));
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Par")]
    [InlineData("París")]
    [InlineData("")]
    [InlineData(" ")]
    public void DoesNotGuessUnmatchedNames(string branch)
    {
        var resolver = new SaaMessageBranchResolver(new[] { new KeyValuePair<string, int>("Paris", 7) });
        Assert.Null(resolver.ResolveBranchId(branch));
    }

    [Fact]
    public void RejectsAmbiguousNamesEvenAfterAnAdditionalDuplicate()
    {
        var resolver = new SaaMessageBranchResolver(new[]
        {
            new KeyValuePair<string, int>("Paris", 7),
            new KeyValuePair<string, int>(" PARIS ", 8),
            new KeyValuePair<string, int>("Paris", 7),
            new KeyValuePair<string, int>("London", 12)
        });
        Assert.Contains("multiple", Assert.Throws<FormatException>(() => resolver.ResolveBranchId("Paris")).Message);
        Assert.Equal(12, resolver.ResolveBranchId("London"));
    }

    [Fact]
    public void AllowsDuplicateMappingsToTheSameId()
    {
        var resolver = new SaaMessageBranchResolver(new[]
        {
            new KeyValuePair<string, int>("Paris", 7),
            new KeyValuePair<string, int>(" PARIS ", 7)
        });
        Assert.Equal(7, resolver.ResolveBranchId("Paris"));
    }

    [Theory]
    [InlineData("", 7)]
    [InlineData(" ", 7)]
    [InlineData("Paris", 0)]
    [InlineData("Paris", -1)]
    public void RejectsInvalidDirectoryEntries(string branch, int id) =>
        Assert.Throws<ArgumentException>(() => new SaaMessageBranchResolver(new[] { new KeyValuePair<string, int>(branch, id) }));

    [Fact]
    public void UsesAnIndependentDirectorySnapshot()
    {
        var entries = new Dictionary<string, int> { ["Paris"] = 7 };
        var resolver = new SaaMessageBranchResolver(entries);
        entries["Paris"] = 99;
        Assert.Equal(7, resolver.ResolveBranchId("Paris"));
    }
}
