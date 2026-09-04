using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ORP.Application.Abstractions;

namespace ORP.Api.Authentication;

public sealed class DebugAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IUserAccessService users, IWebHostEnvironment environment) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment()) return AuthenticateResult.Fail("Debug authentication is disabled outside Development.");
        if (!Context.Request.Headers.TryGetValue("X-Debug-User", out var value)) return AuthenticateResult.NoResult();
        var requestedUser = value.ToString().Trim();
        var access = int.TryParse(requestedUser, out var userId)
            ? await users.GetByIdAsync(userId, Context.RequestAborted)
            : await users.GetByUserNameAsync(requestedUser, Context.RequestAborted);
        if (access is null) return AuthenticateResult.Fail("Unknown development user.");
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, access.UserId.ToString()), new(ClaimTypes.Name, access.UserName) };
        claims.AddRange(access.Permissions.Select(x => new Claim("permission", x)));
        claims.AddRange(access.BranchIds.Select(x => new Claim("branch", x.ToString())));
        claims.AddRange(access.DepartmentIds.Select(x => new Claim("department", x.ToString())));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal => accessor.HttpContext?.User ?? throw new UnauthorizedAccessException();
    public int UserId => int.Parse(Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
    public string UserName => Principal.Identity?.Name ?? throw new UnauthorizedAccessException();
}
