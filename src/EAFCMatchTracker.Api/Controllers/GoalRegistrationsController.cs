using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Exceptions;
using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EAFCMatchTracker.Api.Controllers;

/// <summary>
/// Registro de gols ao vivo / antecipado. PÚBLICO (sem login): as escritas são liberadas no ApiKeyMiddleware e
/// protegidas por rate limit por IP (políticas em Program.cs).
/// </summary>
[ApiController]
[Route("api/goal-registrations")]
public class GoalRegistrationsController : ControllerBase
{
    public const string SearchPolicy = "goalreg-search";
    public const string PreviewPolicy = "goalreg-preview";
    public const string WritePolicy = "goalreg-write";

    private readonly IGoalRegistrationService _service;
    private readonly IOpponentSearchService _search;
    private readonly IOpponentPreviewService _preview;

    public GoalRegistrationsController(
        IGoalRegistrationService service,
        IOpponentSearchService search,
        IOpponentPreviewService preview)
    {
        _service = service;
        _search = search;
        _preview = preview;
    }

    // GET /api/goal-registrations/roster?clubId=
    [HttpGet("roster")]
    public Task<IActionResult> GetRoster([FromQuery] long clubId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await _service.GetRosterAsync(clubId, ct)));

    // GET /api/goal-registrations/opponents/search?q=&clubId=&limit=15
    [HttpGet("opponents/search")]
    [EnableRateLimiting(SearchPolicy)]
    public Task<IActionResult> SearchOpponents(
        [FromQuery] string? q, [FromQuery] long? clubId, [FromQuery] int? limit, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await _search.SearchAsync(q ?? string.Empty, clubId, limit, ct)));

    // GET /api/goal-registrations/opponents/{opponentClubId}/preview?clubId=&name=
    [HttpGet("opponents/{opponentClubId:long}/preview")]
    [EnableRateLimiting(PreviewPolicy)]
    public Task<IActionResult> PreviewOpponent(
        long opponentClubId, [FromQuery] long? clubId, [FromQuery] string? name, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await _preview.GetPreviewAsync(opponentClubId, clubId, name, ct)));

    // GET /api/goal-registrations/current?clubId=   -> 200 Registration | 204
    [HttpGet("current")]
    public Task<IActionResult> GetCurrent([FromQuery] long clubId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            if (clubId <= 0) throw new DomainValidationException("Informe o clube (clubId).");
            var current = await _service.GetCurrentAsync(clubId, ct);
            return current is null ? NoContent() : Ok(current);
        });

    // GET /api/goal-registrations?clubId=&status=&limit=
    [HttpGet]
    public Task<IActionResult> List(
        [FromQuery] long? clubId, [FromQuery] string? status, [FromQuery] int? limit, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await _service.ListAsync(clubId, status, limit, ct)));

    // GET /api/goal-registrations/{id}
    [HttpGet("{id:long}")]
    public Task<IActionResult> Get(long id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var reg = await _service.GetAsync(id, ct);
            return reg is null ? throw new KeyNotFoundException($"Registro {id} não encontrado.") : Ok(reg);
        });

    // POST /api/goal-registrations
    [HttpPost]
    [EnableRateLimiting(WritePolicy)]
    public Task<IActionResult> Create([FromBody] CreateGoalRegistrationRequest body, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var created = await _service.CreateAsync(body, ct);
            return Created($"/api/goal-registrations/{created.Id}", created);
        });

    // PUT /api/goal-registrations/{id}
    [HttpPut("{id:long}")]
    [EnableRateLimiting(WritePolicy)]
    public Task<IActionResult> Update(long id, [FromBody] UpdateGoalRegistrationRequest body, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await _service.UpdateAsync(id, body, ct)));

    // DELETE /api/goal-registrations/{id}
    [HttpDelete("{id:long}")]
    [EnableRateLimiting(WritePolicy)]
    public Task<IActionResult> Delete(long id, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        });

    // POST /api/goal-registrations/{id}/goals
    [HttpPost("{id:long}/goals")]
    [EnableRateLimiting(WritePolicy)]
    public Task<IActionResult> AddGoal(long id, [FromBody] GoalRegistrationDto body, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var reg = await _service.AddGoalAsync(id, body, ct);
            return Created($"/api/goal-registrations/{id}", reg);
        });

    // PUT /api/goal-registrations/{id}/goals/{goalId}
    [HttpPut("{id:long}/goals/{goalId:long}")]
    [EnableRateLimiting(WritePolicy)]
    public Task<IActionResult> UpdateGoal(long id, long goalId, [FromBody] GoalRegistrationDto body, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await _service.UpdateGoalAsync(id, goalId, body, ct)));

    // DELETE /api/goal-registrations/{id}/goals/{goalId}   -> 200 com o registro completo
    [HttpDelete("{id:long}/goals/{goalId:long}")]
    [EnableRateLimiting(WritePolicy)]
    public Task<IActionResult> DeleteGoal(long id, long goalId, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await _service.DeleteGoalAsync(id, goalId, ct)));

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DomainValidationException ex)
        {
            return Problem(ex.Message, StatusCodes.Status400BadRequest, "Invalid request");
        }
        catch (DomainConflictException ex)
        {
            return Problem(ex.Message, StatusCodes.Status409Conflict, "Conflict");
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(ex.Message, StatusCodes.Status404NotFound, "Not found");
        }
    }

    private ObjectResult Problem(string detail, int status, string title) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
