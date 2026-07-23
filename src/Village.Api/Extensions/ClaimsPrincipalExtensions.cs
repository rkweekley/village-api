using System.Security.Claims;

namespace Village.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("sub")?.Value;
        if (sub != null && Guid.TryParse(sub, out var id))
            return id;
        return null;
    }

    public static Guid? GetFamilyId(this ClaimsPrincipal principal)
    {
        var familyId = principal.FindFirst("family_id")?.Value;
        if (familyId != null && Guid.TryParse(familyId, out var id))
            return id;
        return null;
    }

    public static string? GetEmail(this ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.Email)?.Value;

    public static string? GetDisplayName(this ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.Name)?.Value;

    public static string? GetRole(this ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.Role)?.Value;
}
