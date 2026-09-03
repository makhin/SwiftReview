using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using DevExtreme.AspNet.Data.ResponseModel;
using SwiftReview.Api.Authorization;
using SwiftReview.Api.Hubs;
using SwiftReview.Api.Infrastructure;
using SwiftReview.Application.Abstractions;
using SwiftReview.Application.Assignments.Assign;
using SwiftReview.Application.Assignments.Reassign;
using SwiftReview.Application.Audit.GetAuditTrail;
using SwiftReview.Application.Dashboard.GetSummary;
using SwiftReview.Application.Messages.Get;
using SwiftReview.Application.Messages.Import;
using SwiftReview.Application.Messages.Search;
using SwiftReview.Application.Reviews;
using SwiftReview.Application.ReferenceData;
using SwiftReview.Domain.Identity;
using SwiftReview.Domain.Messages;
using SwiftReview.Infrastructure.Persistence;

namespace SwiftReview.Api.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").RequireAuthorization();
        var messages = api.MapGroup("/messages");

        messages.MapGet("/grid", Grid).Produces<LoadResult>().ProducesProblem(400).ProducesProblem(403);
        messages.MapGet("/{id:long}", GetMessage).Produces<MessageDetailsDto>().ProducesProblem(404).ProducesProblem(403);
        messages.MapPost("/search", Search).Produces<PagedResult<MessageListItemDto>>().ProducesProblem(400);
        messages.MapPost("/import", Import)
            .RequireAuthorization(policy => policy.RequireClaim("permission", Permissions.MessageImport))
            .Produces<ImportMessageResponse>(StatusCodes.Status201Created).Produces<ImportMessageResponse>()
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404);
        messages.MapPost("/{id:long}/assign", Assign).Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        messages.MapPost("/{id:long}/reassign", Reassign).Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        messages.MapPost("/{id:long}/reviews/start", StartReview).Produces<StartReviewResponse>(StatusCodes.Status201Created).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        messages.MapPost("/{id:long}/reviews/approve", Approve).Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        messages.MapPost("/{id:long}/reviews/reject", Reject).Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        messages.MapPost("/{id:long}/undo", Undo).Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        messages.MapGet("/{id:long}/audit", Audit).Produces<IReadOnlyList<AuditEventDto>>().ProducesProblem(403);
        api.MapGet("/dashboard/summary", Dashboard).Produces<DashboardSummaryDto>();
        api.MapGet("/me", Me).Produces<CurrentUserResponse>();
        api.MapGet("/workflows", Workflows).Produces<IReadOnlyList<WorkflowSummaryDto>>().ProducesProblem(403);
        api.MapGet("/users", Users).Produces<IReadOnlyList<UserSummaryDto>>().ProducesProblem(403);
        api.MapGet("/branches", Branches).Produces<IReadOnlyList<ReferenceItemDto>>().ProducesProblem(403);
        api.MapGet("/departments", Departments).Produces<IReadOnlyList<ReferenceItemDto>>().ProducesProblem(403);
        api.MapGet("/message-types", MessageTypes).Produces<IReadOnlyList<string>>().ProducesProblem(403);
        endpoints.MapPost("/internal/message-changed", Notify).AllowAnonymous();
        return endpoints;
    }

    private static async Task<MessageDetailsDto> GetMessage(long id, GetMessageHandler handler, CancellationToken ct) => await handler.HandleAsync(id, ct);
    private static async Task<LoadResult> Grid([AsParameters] DevExtremeGridRequest request, MessageGridQueries queries, ICurrentUser currentUser,
        IUserAccessService accessService, CancellationToken ct)
    {
        var access = await accessService.GetByIdAsync(currentUser.UserId, ct) ?? throw new UnauthorizedAccessException();
        return await queries.LoadAsync(DevExtremeLoadOptions.Parse(request), access, ct);
    }
    private static Task<PagedResult<MessageListItemDto>> Search(MessageSearchRequest request, SearchMessagesHandler handler, CancellationToken ct) => handler.HandleAsync(request, ct);
    private static async Task<Results<Created<ImportMessageResponse>, Ok<ImportMessageResponse>>> Import(
        ImportMessageRequest request, ImportMessageHandler handler, CancellationToken ct)
    {
        var (message, created) = await handler.HandleAsync(request, ct);
        var response = new ImportMessageResponse(message.Id, !created);
        return created ? TypedResults.Created($"/api/messages/{message.Id}", response) : TypedResults.Ok(response);
    }

    private static async Task<IResult> Assign(long id, AssignMessageRequest request, AssignMessageHandler handler, ISwiftReviewStore store, IAuthorizationService authorization, HttpContext context, CancellationToken ct)
    {
        var resource = new MessageAuthorizationResource(await store.FindMessageAsync(id, ct) ?? throw new ResourceNotFoundException("Message not found."), await store.GetReviewsAsync(id, ct));
        var result = await authorization.AuthorizeAsync(context.User, resource, new MessageActionRequirement(Permissions.MessageAssign));
        if (!result.Succeeded) return Forbidden(); await handler.HandleAsync(id, request, ct); return Results.NoContent();
    }
    private static async Task<IResult> Reassign(long id, AssignMessageRequest request, ReassignMessageHandler handler, ISwiftReviewStore store, IAuthorizationService authorization, HttpContext context, CancellationToken ct)
    {
        var resource = new MessageAuthorizationResource(await store.FindMessageAsync(id, ct) ?? throw new ResourceNotFoundException("Message not found."), await store.GetReviewsAsync(id, ct));
        var result = await authorization.AuthorizeAsync(context.User, resource, new MessageActionRequirement(Permissions.MessageAssign));
        if (!result.Succeeded) return Forbidden(); await handler.HandleAsync(id, request, ct); return Results.NoContent();
    }
    private static Task<IResult> StartReview(long id, StartReviewRequest request, StartReviewHandler handler, ISwiftReviewStore store, IAuthorizationService auth, HttpContext context, CancellationToken ct) =>
        ReviewAction(id, request.Level, store, auth, context, ct, async () => { var reviewId = await handler.HandleAsync(id, request, ct); return Results.Created($"/api/messages/{id}", new StartReviewResponse(reviewId)); });
    private static Task<IResult> Approve(long id, ApproveReviewRequest request, ApproveReviewHandler handler, ISwiftReviewStore store, IAuthorizationService auth, HttpContext context, CancellationToken ct) =>
        ReviewAction(id, request.Level, store, auth, context, ct, async () => { await handler.HandleAsync(id, request, ct); return Results.NoContent(); });
    private static Task<IResult> Reject(long id, RejectReviewRequest request, RejectReviewHandler handler, ISwiftReviewStore store, IAuthorizationService auth, HttpContext context, CancellationToken ct) =>
        ReviewAction(id, request.Level, store, auth, context, ct, async () => { await handler.HandleAsync(id, request, ct); return Results.NoContent(); }, Permissions.ReviewReject);
    private static async Task<IResult> ReviewAction(long id, int level, ISwiftReviewStore store, IAuthorizationService auth, HttpContext context, CancellationToken ct, Func<Task<IResult>> action, string? permission = null)
    { var resource = new MessageAuthorizationResource(await store.FindMessageAsync(id, ct) ?? throw new ResourceNotFoundException("Message not found."), await store.GetReviewsAsync(id, ct)); var ok = await auth.AuthorizeAsync(context.User, resource, new MessageActionRequirement(permission ?? ReviewPermissions.ForLevel(level), level)); return ok.Succeeded ? await action() : Forbidden(); }
    private static async Task<IResult> Undo(long id, UndoReviewRequest request, UndoReviewHandler handler, ISwiftReviewStore store, IAuthorizationService auth, HttpContext context, CancellationToken ct)
    { var resource = new MessageAuthorizationResource(await store.FindMessageAsync(id, ct) ?? throw new ResourceNotFoundException("Message not found."), await store.GetReviewsAsync(id, ct)); var ok = await auth.AuthorizeAsync(context.User, resource, new MessageActionRequirement(Permissions.ReviewUndo)); if (!ok.Succeeded) return Forbidden(); await handler.HandleAsync(id, request, ct); return Results.NoContent(); }
    private static async Task<IReadOnlyList<AuditEventDto>> Audit(long id, GetAuditTrailHandler handler, CancellationToken ct) => await handler.HandleAsync(id, ct);
    private static async Task<DashboardSummaryDto> Dashboard(GetDashboardSummaryHandler handler, CancellationToken ct) => await handler.HandleAsync(ct);
    private static Ok<CurrentUserResponse> Me(ICurrentUser current, HttpContext context) => TypedResults.Ok(new CurrentUserResponse(
        current.UserId, current.UserName,
        context.User.FindAll("permission").Select(x => x.Value).Order().ToList(),
        context.User.FindAll("branch").Select(x => int.Parse(x.Value)).Order().ToList(),
        context.User.FindAll("department").Select(x => int.Parse(x.Value)).Order().ToList()));
    private static Task<IReadOnlyList<WorkflowSummaryDto>> Workflows(GetWorkflowsHandler handler, CancellationToken ct) => handler.HandleAsync(ct);
    private static Task<IReadOnlyList<UserSummaryDto>> Users(GetUsersHandler handler, CancellationToken ct) => handler.HandleAsync(ct);
    private static Task<IReadOnlyList<ReferenceItemDto>> Branches(GetBranchesHandler handler, CancellationToken ct) => handler.HandleAsync(ct);
    private static Task<IReadOnlyList<ReferenceItemDto>> Departments(GetDepartmentsHandler handler, CancellationToken ct) => handler.HandleAsync(ct);
    private static Task<IReadOnlyList<string>> MessageTypes(GetMessageTypesHandler handler, CancellationToken ct) => handler.HandleAsync(ct);
    private static async Task<IResult> Notify(MessageChangedNotification notification, HttpContext context,
        IConfiguration config, IHubContext<MessagesHub> hub, InternalEventDeduplicator deduplicator)
    {
        var expectedKey = config["InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expectedKey) ||
            !string.Equals(context.Request.Headers["X-Internal-Key"].ToString(), expectedKey, StringComparison.Ordinal))
            return Results.Unauthorized();
        if (!string.Equals(context.Request.Headers["Idempotency-Key"].ToString(), notification.EventId,
                StringComparison.Ordinal))
            return Results.BadRequest();
        if (!deduplicator.TryBegin(notification.EventId)) return Results.Accepted();
        await Task.WhenAll(
            hub.Clients.Group($"message:{notification.MessageId}").SendAsync("MessageChanged", notification),
            hub.Clients.Group($"branch:{notification.BranchId}").SendAsync("MessageChanged", notification),
            hub.Clients.Group($"department:{notification.DepartmentId}").SendAsync("MessageChanged", notification));
        return Results.Accepted();
    }

    private static IResult Forbidden() => Results.Problem(statusCode: StatusCodes.Status403Forbidden,
        title: "Forbidden", detail: "The current user is not allowed to perform this action.");
}
