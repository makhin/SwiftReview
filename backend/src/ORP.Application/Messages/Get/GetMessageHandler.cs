using ORP.Application.Abstractions;

namespace ORP.Application.Messages.Get;

public sealed class GetMessageHandler(IMessageQueries queries, IUserAccessService users, ICurrentUser current)
{
    public async Task<MessageDetailsDto> HandleAsync(long id, CancellationToken ct)
    {
        var access = await users.GetByIdAsync(current.UserId, ct) ?? throw new UnauthorizedAccessException();
        return await queries.GetAsync(id, access, ct) ?? throw new ResourceNotFoundException("Message was not found or is outside the user's access scope.");
    }
}
