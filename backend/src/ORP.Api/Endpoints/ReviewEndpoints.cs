using ORP.Application.Abstractions;
using ORP.Application.Reviews;

namespace ORP.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/messages/reviews/bulk-approve", BulkApproveReviews).WithTags("Review")
            .WithName(nameof(BulkApproveReviews)).WithSummary("Assign and approve all remaining levels for up to 100 messages.")
            .WithDescription("Immediate workflows only. Requires review.bulk-approve, message.assign and every remaining review.levelN permission in the processing scope. Allows reviewer reuse; never replaces another reviewer's assignment. Each message is atomic and has its own result, including errors, in input order. Completed messages return a conflict.")
            .Produces<BulkApproveReviewsResponse>().ProducesProblem(400).ProducesProblem(401);
        var group = api.MapGroup("/messages/{id:long}/reviews").WithTags("Review");
        group.MapPost("/confirm", ConfirmReview)
            .WithName(nameof(ConfirmReview)).WithSummary("Complete the requested confirmation atomically for the current assignee.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/assign-and-confirm", AssignAndConfirmReview)
            .WithName(nameof(AssignAndConfirmReview)).WithSummary("Assign an unassigned message to yourself and confirm atomically.")
            .WithDescription("Requires message.assign and the review-level permission. Confirms an existing own assignment without replacing it; returns 409 for another assignee. Immediate workflows only.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/start", StartReview)
            .WithName(nameof(StartReview)).WithSummary("Start or resume a review.")
            .Produces<StartReviewResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/approve", ApproveReview)
            .WithName(nameof(ApproveReview)).WithSummary("Approve a review attempt.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/reject", RejectReview)
            .WithName(nameof(RejectReview)).WithSummary("Reject a review attempt.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/cancel", CancelReview)
            .WithName(nameof(CancelReview)).WithSummary("Cancel an active review attempt.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/undo", UndoReview)
            .WithName(nameof(UndoReview)).WithSummary("Undo a review confirmation allowed by scoped permissions and workflow policy.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
    }

    private static async Task<IResult> BulkApproveReviews(BulkApproveReviewsRequest request,
        BulkApproveReviewsHandler handler, CancellationToken ct) => Results.Ok(await handler.HandleAsync(request, ct));

    private static async Task<IResult> ConfirmReview(long id, ConfirmReviewRequest request, ConfirmReviewHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AssignAndConfirmReview(long id, ConfirmReviewRequest request,
        AssignAndConfirmReviewHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> StartReview(long id, StartReviewRequest request, StartReviewHandler handler, CancellationToken ct) =>
        Results.Ok(new StartReviewResponse(await handler.HandleAsync(id, request, ct)));

    private static async Task<IResult> ApproveReview(long id, ApproveReviewRequest request, ApproveReviewHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RejectReview(long id, RejectReviewRequest request, RejectReviewHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CancelReview(long id, CancelReviewRequest request, CancelReviewHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UndoReview(long id, UndoReviewRequest request, UndoReviewHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }
}
