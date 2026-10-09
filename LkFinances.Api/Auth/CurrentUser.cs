using System.Security.Claims;

namespace LkFinances.Api.Auth;

public static class CurrentUser
{
    public static Guid Id(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated request without a user id claim."));
}
