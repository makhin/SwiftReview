using Microsoft.Extensions.Logging;

namespace ORP.Application.Authorization;

internal static partial class AuthorizationLog
{
    [LoggerMessage(EventId = 1011, EventName = "MessageAuthorizationDenied", Level = LogLevel.Warning,
        Message = "Message action authorization denied for message {MessageId}, user {UserId}, permission {Permission}, review level {ReviewLevel}, ownership {Ownership}; permission={HasPermission}, branch={HasBranchAccess}, department={HasDepartmentAccess}, state={StateAllowed}, four-eyes={FourEyesAllowed}, owner={OwnershipAllowed}; correlation {CorrelationId}")]
    public static partial void MessageAuthorizationDenied(ILogger logger, long messageId, int? userId,
        string permission, int? reviewLevel, string ownership, bool hasPermission, bool hasBranchAccess,
        bool hasDepartmentAccess, bool stateAllowed, bool fourEyesAllowed, bool ownershipAllowed,
        string correlationId);

}
