using ORP.Api.Authorization;
using ORP.Application.Abstractions;

namespace ORP.Api.Endpoints;

internal static class MessageEndpointAuthorization
{
    internal static async Task<MessageAuthorizationResource> AuthorizationResource(long id, IORPStore store, CancellationToken ct)
    {
        var message = await store.FindMessageAsync(id, ct) ?? throw new ResourceNotFoundException("Message not found.");
        var source = await store.FindMessageSourceAsync(id, ct) ?? throw new ResourceNotFoundException("SWIFT message not found.");
        return new MessageAuthorizationResource(message, source.BranchId, source.DepartmentId, await store.GetReviewsAsync(id, ct));
    }

    internal static IResult Forbidden() => Results.Problem(statusCode: StatusCodes.Status403Forbidden,
        title: "Forbidden", detail: "The current user is not allowed to perform this action.");
}
