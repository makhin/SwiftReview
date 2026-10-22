using ORP.Api.Infrastructure;
using ORP.Application.Grids;
using ORP.Application.Abstractions;
using ORP.Application.Assignments.Assign;
using ORP.Application.Assignments.GetCandidates;
using ORP.Application.Audit.GetAuditTrail;
using ORP.Application.Messages.Get;
using ORP.Application.Messages.ChangeWorkflow;
using ORP.Application.Messages.Search;
using ORP.Domain.Identity;
using ORP.Domain.Auditing;
using ORP.Infrastructure.Persistence;
using ORP.Infrastructure.Documents;

namespace ORP.Api.Endpoints;

public static class MessageEndpoints
{
    public static void MapMessageEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/messages").WithTags("Message");
        group.MapGet("/state-counts", GetStateCounts)
            .WithName(nameof(GetStateCounts)).WithSummary("Get accessible message counts by state.")
            .WithDescription("Returns items for every state, including zeros, with count and a messageType/count breakdown, and total with the same count/breakdown structure summed across all states. Counts include only accessible messages. Optional assignmentScope: null (or omission) for all accessible messages, mine for the current assignee or active reviewer. Grid filters do not apply; unsupported assignmentScope values return 400.")
            .Produces<MessageStateCountsDto>().ProducesProblem(400);
        group.MapPost("/grid", GetMessagesGrid)
            .WithName(nameof(GetMessagesGrid)).WithSummary("Load accessible messages with paging, filtering and sorting.")
            .WithDescription(GridEndpointDescriptions.Messages)
            .Produces<PagedResult<MessageGridRowDto>>().ProducesProblem(400);
        group.MapGet("/{id:long}", GetMessage)
            .WithName(nameof(GetMessage)).WithSummary("Get an accessible message.")
            .Produces<MessageDetailsDto>().ProducesProblem(404);
        group.MapGet("/{id:long}/view", GetMessageView)
            .WithName(nameof(GetMessageView)).WithSummary("Get a message card with approvals and complete audit history.")
            .WithDescription("Requires message and audit access. Roles are current roles in the message branch and visible departments, not historical roles. Approval levels follow the current workflow. Audit events are ordered oldest first. rawText is the stored body without conversion.")
            .Produces<MessageViewDto>().ProducesProblem(403).ProducesProblem(404);
        group.MapGet("/{messageId:long}/pdf", GetMessagePdf)
            .WithName(nameof(GetMessagePdf)).WithSummary("Get the PDF for an accessible message.")
            .WithDescription("Returns the message body as a PDF, preserving plain-text spacing and line breaks. Each successful generation records MessagePdfGenerated in the message audit trail.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf").ProducesProblem(404);
        group.MapPut("/{id:long}/workflow", ChangeMessageWorkflow)
            .WithName(nameof(ChangeMessageWorkflow)).WithSummary("Set the workflow for a message.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapPost("/search", SearchMessages)
            .WithName(nameof(SearchMessages)).WithSummary("Search accessible messages with a JSON filter.")
            .Produces<PagedResult<MessageListItemDto>>().ProducesProblem(400);
        group.MapPost("/{id:long}/assign", AssignMessage)
            .WithName(nameof(AssignMessage)).WithSummary("Assign or change the assignee of a message.")
            .WithDescription("Checks current permissions, assignment eligibility and message state inside a transaction. Audit records distinguish initial assignment from reassignment.")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/{id:long}/assignment-candidates", GetAssignmentCandidates)
            .WithName(nameof(GetAssignmentCandidates)).WithSummary("Get eligible assignees for a message.")
            .Produces<IReadOnlyList<AssignmentCandidateDto>>().ProducesProblem(404).ProducesProblem(409);
        group.MapGet("/{id:long}/audit", GetAuditTrail)
            .WithName(nameof(GetAuditTrail)).WithSummary("Get a paged audit trail for a message.")
            .Produces<PagedResult<AuditEventDto>>().ProducesProblem(400).ProducesProblem(404);
    }

    private static async Task<MessageStateCountsDto> GetStateCounts(string? assignmentScope, MessageGridQueries queries,
        IUserAccessService users, ICurrentUser current, CancellationToken ct) =>
        await queries.StateCountsAsync(await users.GetByIdAsync(current.UserId, ct)
            ?? throw new UnauthorizedAccessException(), assignmentScope, ct);

    private static async Task<MessageDetailsDto> GetMessage(long id, GetMessageHandler handler, CancellationToken ct) => await handler.HandleAsync(id, ct);

    private static Task<MessageViewDto> GetMessageView(long id, GetMessageViewHandler handler, CancellationToken ct) =>
        handler.HandleAsync(id, ct);

    private static async Task<IResult> GetMessagePdf(long messageId, GetMessageHandler handler, TextToPdfConverter converter,
        IORPStore store, ICurrentUser current, IClock clock, ICorrelationContext correlation, CancellationToken ct)
    {
        var message = await handler.HandleAsync(messageId, ct);
        var pdf = converter.Convert(message.Body);
        store.AddAudit(new AuditEvent(messageId, AuditEventType.MessagePdfGenerated, current.UserId,
            clock.UtcNow, null, null, "{}", correlation.CorrelationId));
        await store.SaveChangesAsync(ct);
        return Results.File(pdf, "application/pdf");
    }

    private static async Task<IResult> ChangeMessageWorkflow(long id, ChangeMessageWorkflowRequest request,
        ChangeMessageWorkflowHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<PagedResult<MessageGridRowDto>> GetMessagesGrid(MessageGridRequest request, MessageGridQueries queries, ICurrentUser currentUser,
        IUserAccessService accessService, CancellationToken ct)
    {
        var access = await accessService.GetByIdAsync(currentUser.UserId, ct) ?? throw new UnauthorizedAccessException();
        return await queries.LoadAsync(request, access, ct);
    }

    private static Task<PagedResult<MessageListItemDto>> SearchMessages(MessageSearchRequest request, SearchMessagesHandler handler, CancellationToken ct) => handler.HandleAsync(request, ct);

    private static async Task<IResult> AssignMessage(long id, AssignMessageRequest request,
        AssignMessageHandler handler, CancellationToken ct)
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
