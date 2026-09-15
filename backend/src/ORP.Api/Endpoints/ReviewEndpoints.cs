using Microsoft.AspNetCore.Authorization;
using ORP.Api.Authorization;
using ORP.Application.Abstractions;
using ORP.Application.Reviews;
using ORP.Domain.Identity;
using static ORP.Api.Endpoints.MessageEndpointAuthorization;

namespace ORP.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/messages/{id:long}/reviews").WithTags("Review");
        group.MapPost("/start", StartReview).AddEndpointFilter<MessageMutationTransactionFilter>()
            .WithName(nameof(StartReview)).WithSummary("Start or resume a review.")
            .Produces<StartReviewResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/approve", ApproveReview).AddEndpointFilter<MessageMutationTransactionFilter>()
            .WithName(nameof(ApproveReview)).WithSummary("Approve a review attempt.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/reject", RejectReview).AddEndpointFilter<MessageMutationTransactionFilter>()
            .WithName(nameof(RejectReview)).WithSummary("Reject a review attempt.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/cancel", CancelReview).AddEndpointFilter<MessageMutationTransactionFilter>()
            .WithName(nameof(CancelReview)).WithSummary("Cancel an active review attempt.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/undo", UndoReview).RequireAuthorization("GlobalAdministrator")
            .WithName(nameof(UndoReview)).WithSummary("Undo a review confirmation as a global administrator.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
    }

    private static Task<IResult> StartReview(long id, StartReviewRequest request, StartReviewHandler handler, IORPStore store, IAuthorizationService auth, HttpContext context, CancellationToken ct) =>
        ReviewAction(id, request.Level, store, auth, context, ct, MessageActionOwnership.Assignee,
            async () => { var reviewId = await handler.HandleAsync(id, request, ct); return Results.Ok(new StartReviewResponse(reviewId)); });

    private static Task<IResult> ApproveReview(long id, ApproveReviewRequest request, ApproveReviewHandler handler, IORPStore store, IAuthorizationService auth, HttpContext context, CancellationToken ct) =>
        ReviewAction(id, request.Level, store, auth, context, ct, MessageActionOwnership.ActiveReviewer,
            async () => { await handler.HandleAsync(id, request, ct); return Results.NoContent(); });

    private static Task<IResult> RejectReview(long id, RejectReviewRequest request, RejectReviewHandler handler, IORPStore store, IAuthorizationService auth, HttpContext context, CancellationToken ct) =>
        ReviewAction(id, request.Level, store, auth, context, ct, MessageActionOwnership.ActiveReviewer,
            async () => { await handler.HandleAsync(id, request, ct); return Results.NoContent(); });

    private static Task<IResult> CancelReview(long id, CancelReviewRequest request, CancelReviewHandler handler, IORPStore store, IAuthorizationService auth, HttpContext context, CancellationToken ct) =>
        ReviewAction(id, request.Level, store, auth, context, ct, MessageActionOwnership.ActiveReviewer,
            async () => { await handler.HandleAsync(id, request, ct); return Results.NoContent(); });

    private static async Task<IResult> ReviewAction(long id, int level, IORPStore store,
        IAuthorizationService auth, HttpContext context, CancellationToken ct,
        MessageActionOwnership ownership, Func<Task<IResult>> action)
    {
        var resource = await AuthorizationResource(id, store, ct);
        var result = await auth.AuthorizeAsync(context.User, resource,
            new MessageActionRequirement(ReviewPermissions.ForLevel(level), level, ownership));
        return result.Succeeded ? await action() : Forbidden();
    }

    private static async Task<IResult> UndoReview(long id, UndoReviewRequest request, UndoReviewHandler handler, IORPStore store, IAuthorizationService auth, HttpContext context, CancellationToken ct)
    {
        var resource = await AuthorizationResource(id, store, ct);
        var result = await auth.AuthorizeAsync(context.User, resource, new MessageActionRequirement(Permissions.ReviewUndo));
        if (!result.Succeeded) return Forbidden();
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }
}
