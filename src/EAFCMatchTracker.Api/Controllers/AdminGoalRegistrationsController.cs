using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

/// <summary>Ações administrativas do registro de gols (protegidas pelo ApiKeyMiddleware: tudo sob /api/admin).</summary>
[ApiController]
[Route("api/admin/goal-registrations")]
public class AdminGoalRegistrationsController : ControllerBase
{
    private readonly IGoalRegistrationLinker _linker;

    public AdminGoalRegistrationsController(IGoalRegistrationLinker linker)
    {
        _linker = linker;
    }

    // POST /api/admin/goal-registrations/link-pending  -> { linked, needsReview, expired }
    [HttpPost("link-pending")]
    public async Task<IActionResult> LinkPending(CancellationToken ct)
    {
        var result = await _linker.RunAsync(ct: ct);
        return Ok(new { linked = result.Linked, needsReview = result.NeedsReview, expired = result.Expired });
    }
}
