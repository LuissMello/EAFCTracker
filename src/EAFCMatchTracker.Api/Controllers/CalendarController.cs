using EAFCMatchTracker.Application.Dtos;
using EAFCMatchTracker.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace EAFCMatchTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendarService;
    private readonly ILogger<CalendarController> _logger;

    public CalendarController(ICalendarService calendarService, ILogger<CalendarController> logger)
    {
        _calendarService = calendarService;
        _logger = logger;
    }

    // GET /api/Calendar?year=2025&month=9&clubId=123
    // GET /api/Calendar?year=2025&month=9&clubIds=1,2,3
    [HttpGet]
    public async Task<ActionResult<CalendarMonthDto>> GetMonthlyCalendar(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] long? clubId = null,
        [FromQuery] string? clubIds = null)
    {
        _logger.LogInformation("GetMonthlyCalendar called with year={Year}, month={Month}, clubId={ClubId}, clubIds={ClubIds}", year, month, clubId, clubIds);

        if (year <= 0 || month < 1 || month > 12)
        {
            _logger.LogWarning("Invalid date parameters: year={Year}, month={Month}", year, month);
            return BadRequest("Parâmetros de data inválidos.");
        }

        try
        {
            var selectedIds = ParseClubIds(clubIds);
            if (selectedIds.Count == 0)
            {
                if (clubId is null or <= 0)
                {
                    _logger.LogWarning("No clubId or clubIds provided.");
                    return BadRequest("Informe clubId ou clubIds.");
                }
                selectedIds.Add(clubId!.Value);
            }

            var result = await _calendarService.GetMonthlyCalendarAsync(year, month, selectedIds.ToHashSet(), default);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMonthlyCalendar for year={Year}, month={Month}", year, month);
            return StatusCode(500, "Erro interno ao buscar calendário mensal.");
        }
    }

    // GET /api/Calendar/day?date=2025-09-12&clubId=123
    // GET /api/Calendar/day?date=2025-09-12&clubIds=1,2,3
    [HttpGet("day")]
    public async Task<ActionResult<CalendarDayDetailsDto>> GetDayDetails(
        [FromQuery] DateOnly date,
        [FromQuery] long? clubId = null,
        [FromQuery] string? clubIds = null)
    {
        _logger.LogInformation("GetDayDetails called with date={Date}, clubId={ClubId}, clubIds={ClubIds}", date, clubId, clubIds);

        try
        {
            var selectedIds = ParseClubIds(clubIds);
            if (selectedIds.Count == 0)
            {
                if (clubId is null or <= 0)
                {
                    _logger.LogWarning("No clubId or clubIds provided for day details.");
                    return BadRequest("Informe clubId ou clubIds.");
                }
                selectedIds.Add(clubId!.Value);
            }

            var result = await _calendarService.GetDayDetailsAsync(date, selectedIds.ToHashSet(), default);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetDayDetails for date={Date}", date);
            return StatusCode(500, "Erro interno ao buscar detalhes do dia.");
        }
    }

    private static List<long> ParseClubIds(string? csv)
    {
        var list = new List<long>();
        if (string.IsNullOrWhiteSpace(csv)) return list;

        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
                list.Add(id);
        }
        return list.Distinct().ToList();
    }
}
