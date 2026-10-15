using Xunit;

namespace ORP.Importer.Tests;

public sealed class SaaMessageTypeResolverTests
{
    [Theory]
    [InlineData("MT199", "MT199")]
    [InlineData(" mt199 ", "MT199")]
    [InlineData("mxpain.001.001.09", "MXpain.001.001.09")]
    public void ReturnsTheFullCanonicalAllowedType(string input, string expected)
    {
        var resolver = new SaaMessageTypeResolver(new[] { " MT199 ", "MXpain.001.001.09" });
        Assert.Equal(expected, resolver.ResolveMessageType(input));
    }

    [Theory]
    [InlineData("199")]
    [InlineData("MX199")]
    [InlineData("MT199x")]
    [InlineData("MXpain.001.001.08")]
    [InlineData("pain.001.001.09")]
    [InlineData("MT999")]
    [InlineData("")]
    public void DoesNotMatchOtherPrefixesVersionsOrPartialTypes(string input)
    {
        var resolver = new SaaMessageTypeResolver(new[] { "MT199", "MXpain.001.001.09" });
        Assert.Null(resolver.ResolveMessageType(input));
    }

    [Fact]
    public void AnEmptyAllowlistDoesNotAllowAnyType()
    {
        Assert.Null(new SaaMessageTypeResolver(Array.Empty<string>()).ResolveMessageType("MT199"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void RejectsEmptyAllowedTypes(string? input) =>
        Assert.Throws<ArgumentException>(() => new SaaMessageTypeResolver(new[] { input! }));

    [Fact]
    public void DuplicateAllowedTypesUseTheFirstCanonicalSpelling()
    {
        var resolver = new SaaMessageTypeResolver(new[] { "MT199", "mt199" });
        Assert.Equal("MT199", resolver.ResolveMessageType("mt199"));
    }

    [Fact]
    public void TakesAnIndependentSnapshot()
    {
        var allowed = new List<string> { "MT199" };
        var resolver = new SaaMessageTypeResolver(allowed);
        allowed.Clear();
        allowed.Add("MT999");
        Assert.Equal("MT199", resolver.ResolveMessageType("MT199"));
        Assert.Null(resolver.ResolveMessageType("MT999"));
    }
}
