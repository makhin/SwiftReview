using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.Helpers;

namespace ORP.Api.Infrastructure;

internal static class AdminUserGridLoadOptions
{
    public static DataSourceLoadOptionsBase Parse(int skip, int take, string? search, string? sort)
    {
        if (skip < 0 || take is < 1 or > 100 || search?.Length > 100)
            throw new FormatException("Invalid user paging or search options.");

        var options = new DataSourceLoadOptionsBase
        {
            Skip = skip, Take = take, RequireTotalCount = true,
            PrimaryKey = ["Id"], SortByPrimaryKey = true
        };
        try
        {
            // This endpoint exposes sorting and paging, not the full grid load-options contract.
            DataSourceLoadOptionsParser.Parse(options, key => key == "sort" ? sort ?? string.Empty : string.Empty);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or System.Text.Json.JsonException)
        {
            throw new FormatException("Invalid user sort options.", ex);
        }

        if (options.Sort is { Length: > 3 })
            throw new FormatException("At most three user sort fields are allowed.");
        foreach (var clause in options.Sort ?? [])
        {
            if (clause is null) throw new FormatException("Invalid user sort options.");
            clause.Selector = clause.Selector switch
            {
                "id" => "Id", "userName" => "UserName", "displayName" => "DisplayName",
                _ => throw new FormatException("Unknown user sort field.")
            };
        }
        if (options.Sort is not { Length: > 0 })
            options.Sort = [new SortingInfo { Selector = "DisplayName" }];
        return options;
    }
}
