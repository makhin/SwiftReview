using DevExtreme.AspNet.Data.ResponseModel;
using ORP.Api.Infrastructure;
using ORP.Application.Abstractions;
using ORP.Application.Assignments.Assign;
using ORP.Application.Assignments.GetCandidates;
using ORP.Application.Assignments.Reassign;
using ORP.Application.Audit.GetAuditTrail;
using ORP.Application.Messages.Get;
using ORP.Application.Messages.ChangeWorkflow;
using ORP.Application.Messages.Search;
using ORP.Domain.Identity;
using ORP.Infrastructure.Persistence;

namespace ORP.Api.Endpoints;

public static class MessageEndpoints
{
    public static void MapMessageEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/messages").WithTags("Message");
        group.MapGet("/state-counts", GetStateCounts)
            .WithName(nameof(GetStateCounts)).WithSummary("Get accessible message counts by state.")
            .Produces<IReadOnlyList<MessageStateCountDto>>();
        group.MapGet("/grid", GetMessagesGrid)
            .WithName(nameof(GetMessagesGrid)).WithSummary("Load accessible messages using DevExtreme options.")
            .Produces<MessageGridLoadResultDto>().ProducesProblem(400)
            .AddOpenApiOperationTransformer(MessageGridOpenApi.DescribeResponse);
        group.MapGet("/{id:long}", GetMessage)
            .WithName(nameof(GetMessage)).WithSummary("Get an accessible message.")
            .Produces<MessageDetailsDto>().ProducesProblem(404);
        group.MapPut("/{id:long}/workflow", ChangeMessageWorkflow)
            .WithName(nameof(ChangeMessageWorkflow)).WithSummary("Set the workflow for a message.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/search", SearchMessages)
            .WithName(nameof(SearchMessages)).WithSummary("Search accessible messages with a JSON filter.")
            .Produces<PagedResult<MessageListItemDto>>().ProducesProblem(400);
        group.MapPost("/{id:long}/assign", AssignMessage)
            .WithName(nameof(AssignMessage)).WithSummary("Assign an unassigned message.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/{id:long}/reassign", ReassignMessage)
            .WithName(nameof(ReassignMessage)).WithSummary("Change the assignee of an assigned message.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/{id:long}/assignment-candidates", GetAssignmentCandidates)
            .WithName(nameof(GetAssignmentCandidates)).WithSummary("Get eligible assignees for a message.")
            .Produces<IReadOnlyList<AssignmentCandidateDto>>().ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/{id:long}/audit", GetAuditTrail)
            .WithName(nameof(GetAuditTrail)).WithSummary("Get a paged audit trail for a message.")
            .Produces<PagedResult<AuditEventDto>>().ProducesProblem(400).ProducesProblem(404);
    }

    private static async Task<IReadOnlyList<MessageStateCountDto>> GetStateCounts(MessageGridQueries queries,
        IUserAccessService users, ICurrentUser current, CancellationToken ct) =>
        await queries.StateCountsAsync(await users.GetByIdAsync(current.UserId, ct)
            ?? throw new UnauthorizedAccessException(), ct);

    private static async Task<MessageDetailsDto> GetMessage(long id, GetMessageHandler handler, CancellationToken ct) => await handler.HandleAsync(id, ct);

    private static async Task<IResult> ChangeMessageWorkflow(long id, ChangeMessageWorkflowRequest request,
        ChangeMessageWorkflowHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<LoadResult> GetMessagesGrid([AsParameters] DevExtremeGridRequest request, HttpRequest httpRequest, MessageGridQueries queries, ICurrentUser currentUser,
        IUserAccessService accessService, CancellationToken ct)
    {
        var access = await accessService.GetByIdAsync(currentUser.UserId, ct) ?? throw new UnauthorizedAccessException();
        if (request.AssignmentScope == MessageAssignmentScopes.Assignable &&
            !access.Permissions.Contains(Permissions.MessageAssign))
            throw new UnauthorizedAccessException("The current user is not allowed to assign messages.");
        return await queries.LoadAsync(DevExtremeLoadOptions.Parse(httpRequest.Query), access, request.AssignmentScope, ct);
    }

    private static Task<PagedResult<MessageListItemDto>> SearchMessages(MessageSearchRequest request, SearchMessagesHandler handler, CancellationToken ct) => handler.HandleAsync(request, ct);

    private static async Task<IResult> AssignMessage(long id, AssignMessageRequest request,
        AssignMessageHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ReassignMessage(long id, AssignMessageRequest request,
        ReassignMessageHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static Task<IReadOnlyList<AssignmentCandidateDto>> GetAssignmentCandidates(long id,
        GetAssignmentCandidatesHandler handler, CancellationToken ct) => handler.HandleAsync(id, ct);

    private static Task<PagedResult<AuditEventDto>> GetAuditTrail(long id, GetAuditTrailHandler handler,
        CancellationToken ct, int skip = 0, int take = 100) =>
        handler.HandleAsync(id, new AuditTrailRequest(skip, take), ct);
}
