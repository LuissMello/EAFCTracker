using EAFCMatchTracker.Application.Dtos;

namespace EAFCMatchTracker.Application.Interfaces.Services;

public interface ICalendarService
{
    Task<CalendarMonthDto> GetMonthlyCalendarAsync(int year, int month, HashSet<long> selectedIds, bool sessions, CancellationToken ct);
    Task<CalendarDayDetailsDto> GetDayDetailsAsync(DateOnly date, HashSet<long> selectedIds, bool sessions, CancellationToken ct);
}
