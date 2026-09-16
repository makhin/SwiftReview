using ORP.Application.Abstractions;
using ORP.Application.Reviews;

namespace ORP.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/messages/{id:long}/reviews").WithTags("Review");
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
        group.MapPost("/undo", UndoReview).RequireAuthorization("GlobalAdministrator")
            .WithName(nameof(UndoReview)).WithSummary("Undo a review confirmation as a global administrator.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
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
