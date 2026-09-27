using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace PasswordManager.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId
    {
        get
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(idClaim, out var id) ? id : Guid.Empty;
        }
    }

    protected bool IsCurrentUserAdmin => User.FindFirstValue("is_admin") == "true";
}
