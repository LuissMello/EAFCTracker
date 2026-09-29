using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Domain.Models;
using EAFCMatchTracker.Infrastructure.Http;
using EAFCMatchTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private const int MaxClubNameLength = 100;
    private const int MinGameVersion = 20;
    private const int MaxGameVersion = 99;
    private const int MaxGameVersionNameLength = 50;
    private const string DefaultSearchEndpoint = "/allTimeLeaderboard/search?platform=common-gen5&clubName={0}";

    private readonly IAppSettingRepository _settings;
    private readonly ITrackedClubRepository _clubs;
    private readonly IGameVersionRepository _versions;
    private readonly IEAHttpClient _ea;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminController> _logger;
    private readonly EAFCContext _db;

    public AdminController(
        IAppSettingRepository settings,
        ITrackedClubRepository clubs,
        IGameVersionRepository versions,
        IEAHttpClient ea,
        IConfiguration config,
        ILogger<AdminController> logger,
        EAFCContext db)
    {
        _settings = settings;
        _clubs = clubs;
        _versions = versions;
        _ea = ea;
        _config = config;
        _logger = logger;
        _db = db;
    }

    // GET /api/admin/ping  (protegido; permite ao frontend validar a credencial)
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { ok = true });

    // GET /api/admin/me  (protegido; sessão atual). expiresAtUtc = expiração do token Bearer, ou null com X-Api-Key
    [HttpGet("me")]
    public IActionResult Me()
    {
        DateTime? expiresAtUtc = HttpContext.Items.TryGetValue(Security.ApiKeyMiddleware.TokenExpiresItemKey, out var v)
            && v is DateTimeOffset expires
                ? expires.UtcDateTime
                : null;

        return Ok(new { authenticated = true, expiresAtUtc });
    }

    // GET /api/admin/clubs/search?name=Trash%20As%20Well
    [HttpGet("clubs/search")]
    public async Task<IActionResult> SearchClubs([FromQuery] string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(Invalid("Informe o nome do clube."));

        if (name.Trim().Length > MaxClubNameLength)
            return BadRequest(Invalid($"O nome do clube deve ter no máximo {MaxClubNameLength} caracteres."));

        var baseUrl = _config["EAFCSettings:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return StatusCode(500, "EAFCSettings:BaseUrl não configurado.");

        var template = _config["EAFCSettings:SearchClubsEndpoint"] ?? DefaultSearchEndpoint;
        var uri = new Uri(baseUrl.TrimEnd('/') + string.Format(template, Uri.EscapeDataString(name.Trim())));

        string? json;
        try
        {
            json = await _ea.GetStringAsync(uri, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Falha ao consultar EA para o clube '{Name}'.", Sanitize(name));
            return StatusCode(502, "Não foi possível consultar a API da EA.");
        }

        if (json is null)
            return StatusCode(502, "A API da EA retornou erro.");

        List<SearchClubResult> results;
        try
        {
            results = JsonSerializer.Deserialize<List<SearchClubResult>>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (JsonException)
        {
            results = new();
        }

        var tracked = (await _clubs.GetAllAsync(ct)).Select(c => c.ClubId).ToHashSet();

        var clubs = results
            .Select(r => new
            {
                ClubId = long.TryParse(r.clubId, out var id) ? id : r.clubInfo?.clubId ?? 0,
                Name = r.clubInfo?.name ?? r.clubName,
                CurrentDivision = r.currentDivision
            })
            .Where(c => c.ClubId > 0)
            .GroupBy(c => c.ClubId)
            .Select(g => g.First())
            .Select(c => new { c.ClubId, c.Name, c.CurrentDivision, AlreadyTracked = tracked.Contains(c.ClubId) });

        return Ok(clubs);
    }

    // GET /api/admin/settings
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var all = (await _settings.GetAllAsync(ct)).ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);

        // Apenas as chaves editáveis (whitelist); demais (ex.: live_until_utc) são internas
        var result = AppSettingEntity.Definitions.All.Select(def => new
        {
            Key = def.Key,
            Value = all.TryGetValue(def.Key, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : def.Default.ToString(CultureInfo.InvariantCulture)
        });

        return Ok(result);
    }

    // PUT /api/admin/settings/{key}
    [HttpPut("settings/{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest body, CancellationToken ct)
    {
        var def = AppSettingEntity.Definitions.Find(key);
        if (def is null)
            return BadRequest(Invalid("Chave de configuração desconhecida."));

        if (string.IsNullOrWhiteSpace(body.Value))
            return BadRequest(Invalid("Value não pode ser vazio."));

        if (!int.TryParse(body.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return BadRequest(Invalid($"O valor de '{def.Key}' deve ser um número inteiro."));

        if (number < def.Min || number > def.Max)
            return BadRequest(Invalid($"O valor de '{def.Key}' deve estar entre {def.Min} e {def.Max}."));

        var normalized = number.ToString(CultureInfo.InvariantCulture);
        await _settings.UpsertAsync(def.Key, normalized, ct);
        _logger.LogInformation("Configuração atualizada: {Key} = {Value}", def.Key, normalized);
        return Ok(new { key = def.Key, value = normalized });
    }

    // GET /api/admin/tracked-clubs
    [HttpGet("tracked-clubs")]
    public async Task<IActionResult> GetTrackedClubs(CancellationToken ct)
    {
        var clubs = await _clubs.GetAllAsync(ct);
        var versions = (await _versions.GetAllAsync(ct)).ToDictionary(v => v.Id);
        return Ok(clubs.Select(c => ToTrackedClubResponse(c, versions)));
    }

    // POST /api/admin/tracked-clubs   body: { clubId, name?, gameVersion? }  (gameVersion omitido = edição corrente)
    [HttpPost("tracked-clubs")]
    public async Task<IActionResult> AddTrackedClub([FromBody] AddTrackedClubRequest body, CancellationToken ct)
    {
        if (body.ClubId <= 0)
            return BadRequest(Invalid("ClubId inválido."));

        var name = string.IsNullOrWhiteSpace(body.Name) ? null : body.Name.Trim();
        if (name is { Length: > MaxClubNameLength })
            return BadRequest(Invalid($"O nome do clube deve ter no máximo {MaxClubNameLength} caracteres."));

        GameVersionEntity? version;
        if (body.GameVersion.HasValue)
        {
            version = await _versions.GetByVersionAsync(body.GameVersion.Value, ct);
            if (version is null)
                return BadRequest(Invalid($"Edição do jogo {body.GameVersion.Value} desconhecida."));
        }
        else
        {
            version = await _versions.GetCurrentAsync(ct);
        }

        var entity = new TrackedClubEntity
        {
            ClubId = body.ClubId,
            Name = name,
            AddedAt = DateTimeOffset.UtcNow,
            GameVersionId = version?.Id
        };

        var (stored, created) = await _clubs.AddAsync(entity, ct);
        if (created)
            _logger.LogInformation("Clube adicionado ao tracking: {ClubId} (edição {Version})", body.ClubId, version?.Version);

        var versions = (await _versions.GetAllAsync(ct)).ToDictionary(v => v.Id);
        return Ok(ToTrackedClubResponse(stored, versions));
    }

    // PUT /api/admin/tracked-clubs/{clubId}/game-version   body: { gameVersion }
    [HttpPut("tracked-clubs/{clubId:long}/game-version")]
    public async Task<IActionResult> SetTrackedClubGameVersion(long clubId, [FromBody] SetTrackedClubGameVersionRequest body, CancellationToken ct)
    {
        var version = await _versions.GetByVersionAsync(body.GameVersion, ct);
        if (version is null)
            return BadRequest(Invalid($"Edição do jogo {body.GameVersion} desconhecida."));

        var updated = await _clubs.SetGameVersionAsync(clubId, version.Id, ct);
        if (updated is null)
            return NotFound(Invalid($"Clube {clubId} não encontrado.", StatusCodes.Status404NotFound));

        _logger.LogInformation("Clube {ClubId} movido para a edição {Version}", clubId, version.Version);
        var versions = (await _versions.GetAllAsync(ct)).ToDictionary(v => v.Id);
        return Ok(ToTrackedClubResponse(updated, versions));
    }

    [HttpPut("tracked-clubs/{clubId:long}/session-settings")]
    public async Task<IActionResult> SetSessionSettings(long clubId, [FromBody] SessionSettingsRequest body, CancellationToken ct)
    {
        if (body.GapMinutes is < 15 or > 360)
            return BadRequest(Invalid("O intervalo entre partidas deve estar entre 15 e 360 minutos."));
        var timeZoneId = body.TimeZoneId?.Trim();
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Length > 100)
            return BadRequest(Invalid("Informe um fuso horário IANA válido."));
        try { TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) { return BadRequest(Invalid("Fuso horário desconhecido.")); }
        catch (InvalidTimeZoneException) { return BadRequest(Invalid("Fuso horário inválido.")); }

        var club = await _db.TrackedClubs.FirstOrDefaultAsync(c => c.ClubId == clubId, ct);
        if (club is null) return NotFound(Invalid($"Clube {clubId} não encontrado.", 404));
        club.TimeZoneId = timeZoneId;
        club.SessionGapMinutes = body.GapMinutes;
        await _db.SaveChangesAsync(ct);
        return Ok(new { club.ClubId, club.TimeZoneId, club.SessionGapMinutes });
    }

    [HttpPut("tracked-clubs/{clubId:long}/session-boundaries/{matchId:long}")]
    public async Task<IActionResult> SetSessionBoundary(long clubId, long matchId, [FromBody] SessionBoundaryRequest body, CancellationToken ct)
    {
        if (body.Mode is not ("auto" or "split" or "join"))
            return BadRequest(Invalid("Use auto, split ou join."));
        if (!await _db.MatchClubs.AnyAsync(c => c.ClubId == clubId && c.MatchId == matchId, ct))
            return NotFound(Invalid("Partida não encontrada para este clube.", 404));

        var boundary = await _db.SessionBoundaries.FindAsync([clubId, matchId], ct);
        if (body.Mode == "auto")
        {
            if (boundary is not null) _db.SessionBoundaries.Remove(boundary);
        }
        else if (boundary is null)
        {
            _db.SessionBoundaries.Add(new SessionBoundaryEntity
            {
                ClubId = clubId, MatchId = matchId, StartNewSession = body.Mode == "split"
            });
        }
        else boundary.StartNewSession = body.Mode == "split";

        await _db.SaveChangesAsync(ct);
        return Ok(new { clubId, matchId, body.Mode });
    }

    // DELETE /api/admin/tracked-clubs/{clubId}
    [HttpDelete("tracked-clubs/{clubId:long}")]
    public async Task<IActionResult> RemoveTrackedClub(long clubId, CancellationToken ct)
    {
        var removed = await _clubs.RemoveAsync(clubId, ct);
        if (!removed)
            return NotFound(Invalid($"Clube {clubId} não encontrado.", StatusCodes.Status404NotFound));

        _logger.LogInformation("Clube removido do tracking: {ClubId}", clubId);
        return NoContent();
    }

    // GET /api/admin/game-versions
    [HttpGet("game-versions")]
    public async Task<IActionResult> GetGameVersions(CancellationToken ct)
    {
        var all = await _versions.GetAllAsync(ct);
        return Ok(all.Select(GameVersionDto.From));
    }

    // POST /api/admin/game-versions   body: { version, name?, startsAt? }
    [HttpPost("game-versions")]
    public async Task<IActionResult> CreateGameVersion([FromBody] CreateGameVersionRequest body, CancellationToken ct)
    {
        if (body.Version < MinGameVersion || body.Version > MaxGameVersion)
            return BadRequest(Invalid($"A edição deve estar entre {MinGameVersion} e {MaxGameVersion}."));

        var name = string.IsNullOrWhiteSpace(body.Name) ? $"FC{body.Version}" : body.Name.Trim();
        if (name.Length > MaxGameVersionNameLength)
            return BadRequest(Invalid($"O nome da edição deve ter no máximo {MaxGameVersionNameLength} caracteres."));

        DateTimeOffset? startsAt = null;
        if (!string.IsNullOrWhiteSpace(body.StartsAt))
        {
            if (!DateTimeOffset.TryParse(body.StartsAt.Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                return BadRequest(Invalid("startsAt inválido (use formato ISO 8601, ex.: 2026-09-25 ou 2026-09-25T00:00:00Z)."));
            startsAt = parsed;
        }

        var (entity, created) = await _versions.CreateAsync(body.Version, name, startsAt, ct);
        if (!created || entity is null)
            return Conflict(Invalid($"A edição {body.Version} já existe.", StatusCodes.Status409Conflict));

        _logger.LogInformation("Edição do jogo criada: {Version} ({Name})", entity.Version, entity.Name);
        return Ok(GameVersionDto.From(entity));
    }

    // PUT /api/admin/game-versions/{version}/current   (torna corrente; desmarca as demais atomicamente)
    [HttpPut("game-versions/{version:int}/current")]
    public async Task<IActionResult> SetCurrentGameVersion(int version, CancellationToken ct)
    {
        var updated = await _versions.SetCurrentAsync(version, ct);
        if (updated is null)
            return NotFound(Invalid($"Edição {version} não encontrada.", StatusCodes.Status404NotFound));

        _logger.LogInformation("Edição corrente do jogo alterada para {Version}", version);
        return Ok(GameVersionDto.From(updated));
    }

    private static object ToTrackedClubResponse(TrackedClubEntity c, IReadOnlyDictionary<int, GameVersionEntity> versions)
    {
        GameVersionEntity? v = null;
        if (c.GameVersionId.HasValue) versions.TryGetValue(c.GameVersionId.Value, out v);
        return new { c.ClubId, c.Name, c.AddedAt, c.TimeZoneId, c.SessionGapMinutes, GameVersion = v?.Version, GameVersionName = v?.Name };
    }

    private static ProblemDetails Invalid(string detail, int status = StatusCodes.Status400BadRequest) => new()
    {
        Status = status,
        Title = status == StatusCodes.Status404NotFound ? "Not found"
              : status == StatusCodes.Status409Conflict ? "Conflict"
              : "Invalid request",
        Detail = detail
    };

    // Remove caracteres de controle (log forging) e limita o tamanho de entrada do usuário nos logs
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var sb = new StringBuilder(Math.Min(value.Length, 100));
        foreach (var c in value)
        {
            if (sb.Length >= 100) break;
            sb.Append(char.IsControl(c) ? '_' : c);
        }
        return sb.ToString();
    }
}

public record UpdateSettingRequest(string Value);
public record AddTrackedClubRequest(long ClubId, string? Name, int? GameVersion = null);
public record SetTrackedClubGameVersionRequest(int GameVersion);
public record SessionSettingsRequest(string TimeZoneId, int GapMinutes);
public record SessionBoundaryRequest(string Mode);
public record CreateGameVersionRequest(int Version, string? Name, string? StartsAt);
