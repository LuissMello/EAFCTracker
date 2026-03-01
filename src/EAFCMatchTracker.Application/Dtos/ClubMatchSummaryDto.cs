namespace EAFCMatchTracker.Application.Dtos;

public sealed class ClubMatchSummaryDto
{
    public bool Disconnected { get; set; }
    public short RedCards { get; set; }
    public bool HadHatTrick { get; set; }

    // nomes (podem conter null se o Player estiver ausente)
    public List<string?> HatTrickPlayerNames { get; set; } = new();

    // único goleiro do clube (nome ou null)
    public string? GoalkeeperPlayerName { get; set; }

    // único Man of the Match do jogo, preenchido apenas no clube correto (nome ou null)
    public string? ManOfTheMatchPlayerName { get; set; }
}
