using FluentValidation;
using ORP.Application.Abstractions;

namespace ORP.Application.Messages.Search;

public sealed class MessageSearchValidator : AbstractValidator<MessageSearchRequest>
{
    private static readonly string[] Fields = ["receivedAt", "state", "messageType", "amount", "externalId"];
    public MessageSearchValidator()
    {
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).InclusiveBetween(1, 500);
        RuleForEach(x => x.Sort).ChildRules(sort =>
        {
            sort.RuleFor(x => x.Field).Must(x => Fields.Contains(x, StringComparer.OrdinalIgnoreCase)).WithMessage("Unsupported sort field.");
            sort.RuleFor(x => x.Direction).Must(x => x.Equals("asc", StringComparison.OrdinalIgnoreCase) || x.Equals("desc", StringComparison.OrdinalIgnoreCase));
        });
    }
}

public sealed class SearchMessagesHandler(IMessageQueries queries, IValidator<MessageSearchRequest> validator,
    ICurrentUser currentUser, IUserAccessService accessService)
{
    public async Task<PagedResult<MessageListItemDto>> HandleAsync(MessageSearchRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var access = await accessService.GetByIdAsync(currentUser.UserId, cancellationToken) ?? throw new UnauthorizedAccessException();
        return await queries.SearchAsync(request, access, cancellationToken);
    }
}
