using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using ORP.Api.Infrastructure;
using Xunit;

namespace ORP.IntegrationTests;

public sealed class DevExtremeLoadOptionsTests
{
    [Fact]
    public void Parse_NormalizesAndBoundsLoadOptions()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["skip"] = "0",
            ["take"] = "25",
            ["sort"] = "[{\"selector\":\"receivedAt\",\"desc\":true}]",
            ["filter"] = "[[\"currency\",\"=\",\"EUR\"],\"and\",[\"externalId\",\"contains\",\"IT\"]]"
        });

        var options = DevExtremeLoadOptions.Parse(query);

        Assert.Equal("ReceivedAt", Assert.Single(options.Sort!).Selector);
        Assert.Equal(25, options.Take);
        Assert.True(options.PaginateViaPrimaryKey);
        Assert.Equal(["Id"], options.PrimaryKey);
    }

    [Theory]
    [InlineData("take", "501")]
    [InlineData("sort", "[{\"selector\":\"rawContent\"}]")]
    [InlineData("filter", "[\"amount\",\"contains\",\"1\"]")]
    public void Parse_RejectsUnsafeLoadOptions(string key, string value)
    {
        var values = new Dictionary<string, StringValues> { ["skip"] = "0", ["take"] = "25", [key] = value };
        Assert.Throws<FormatException>(() => DevExtremeLoadOptions.Parse(new QueryCollection(values)));
    }
}
