using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Application.Services.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace EAFCMatchTracker.Api.Controllers;

/// <summary>
/// Catálogo de arquétipos no Admin (protegido pelo ApiKeyMiddleware: tudo sob /api/admin). O PUT respeita o modo
/// somente leitura (ReadOnlyGuardMiddleware responde 403).
/// </summary>
[ApiController]
[Route("api/admin/archetypes")]
public class AdminArchetypesController : ControllerBase
{
    private readonly IArchetypeService _service;

    public AdminArchetypesController(IArchetypeService service) => _service = service;

    // GET /api/admin/archetypes -> todos os ids observados + os do catálogo
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _service.GetAdminListAsync(ct));

    // PUT /api/admin/archetypes/{id}  body: { name, shortName, positionGroup }  (upsert)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AdminArchetypeUpdateDto body, CancellationToken ct)
    {
        var error = ArchetypeService.Validate(id, body, out var normalized);
        if (error is not null)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid request", Detail = error });

        try
        {
            return Ok(await _service.UpdateAsync(id, normalized, ct));
        }
        catch (ArchetypeTableMissingException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Status = 503, Title = "Service unavailable", Detail = ex.Message });
        }
    }
}
