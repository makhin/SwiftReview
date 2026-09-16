using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ORP.Infrastructure.Persistence;

namespace ORP.Api.Infrastructure;

// Describes full rows; DataSourceLoader still owns the runtime response and its dynamic modes.
public sealed class MessageGridLoadResultDto
{
    public required MessageGridRowDto[] Data { get; init; }
    public int TotalCount { get; init; }
    public int GroupCount { get; init; }
    public object[]? Summary { get; init; }
}

public static class MessageGridOpenApi
{
    public static async Task DescribeResponse(OpenApiOperation operation,
        OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        var dynamicResult = await context.GetOrCreateSchemaAsync(typeof(LoadResult), cancellationToken: ct);
        var content = operation.Responses!["200"].Content!["application/json"];
        content.Schema = new OpenApiSchema
        {
            Description = "Without group, select or isCountQuery, data contains complete MessageGridRowDto rows. " +
                "With those options, DevExtreme returns groups, selected fields or a count-only LoadResult.",
            AnyOf = [content.Schema!, dynamicResult]
        };
    }
}
