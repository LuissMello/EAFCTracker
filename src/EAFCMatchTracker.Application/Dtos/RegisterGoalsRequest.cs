namespace EAFCMatchTracker.Application.Dtos;

public class RegisterGoalsRequest
{
    public List<GoalRegistrationDto> Goals { get; set; } = new();
}
