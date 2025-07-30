// MatchEntity.cs
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public enum MatchType
{
    League = 1,
    Playoff = 2
}

public class MatchEntity
{
    [Key]
    public long MatchId { get; set; }
    public DateTime Timestamp { get; set; }
    public MatchType MatchType { get; set; }
    public ICollection<MatchClubEntity> Clubs { get; set; }
    public ICollection<MatchPlayerEntity> MatchPlayers { get; set; }
}

// MatchClubEntity.cs
public class MatchClubEntity
{
    [Key]
    public long Id { get; set; }
    public long ClubId { get; set; }
    public long MatchId { get; set; }
    public MatchEntity Match { get; set; }
    public DateTime Date { get; set; }
    public int GameNumber { get; set; }
    public short Goals { get; set; }
    public short GoalsAgainst { get; set; }
    public short Losses { get; set; }
    public short MatchType { get; set; }
    public short Result { get; set; }
    public short Score { get; set; }
    public short SeasonId { get; set; }
    public int Team { get; set; }
    public short Ties { get; set; }
    public bool WinnerByDnf { get; set; }
    public short Wins { get; set; }

    public ClubDetailsEntity Details { get; set; }
}

// ClubDetailsEntity.cs
public class ClubDetailsEntity
{
    public string Name { get; set; }
    public long ClubId { get; set; }
    public long RegionId { get; set; }
    public long TeamId { get; set; }
    public string StadName { get; set; }

    // Propriedades de CustomKit
    public string KitId { get; set; }
    public string CustomKitId { get; set; }
    public string CustomAwayKitId { get; set; }
    public string CustomThirdKitId { get; set; }
    public string CustomKeeperKitId { get; set; }
    public string KitColor1 { get; set; }
    public string KitColor2 { get; set; }
    public string KitColor3 { get; set; }
    public string KitColor4 { get; set; }
    public string KitAColor1 { get; set; }
    public string KitAColor2 { get; set; }
    public string KitAColor3 { get; set; }
    public string KitAColor4 { get; set; }
    public string KitThrdColor1 { get; set; }
    public string KitThrdColor2 { get; set; }
    public string KitThrdColor3 { get; set; }
    public string KitThrdColor4 { get; set; }
    public string DCustomKit { get; set; }
    public string CrestColor { get; set; }
    public string CrestAssetId { get; set; }
}

public class MatchPlayerEntity
{
    public long MatchId { get; set; }
    public long PlayerEntityId { get; set; }
    public long ClubId { get; set; }
    public short Assists { get; set; }
    public short Cleansheetsany { get; set; }
    public short Cleansheetsdef { get; set; }
    public short Cleansheetsgk { get; set; }
    public short Goals { get; set; }
    public short Goalsconceded { get; set; }
    public short Losses { get; set; }
    public bool Mom { get; set; }
    public short Namespace { get; set; }
    public short Passattempts { get; set; }
    public short Passesmade { get; set; }
    public string Pos { get; set; }
    public double Rating { get; set; }
    public string Realtimegame { get; set; }
    public string Realtimeidle { get; set; }
    public short Redcards { get; set; }
    public short Saves { get; set; }
    public short Score { get; set; }
    public short Shots { get; set; }
    public short Tackleattempts { get; set; }
    public short Tacklesmade { get; set; }
    public string Vproattr { get; set; }
    public string Vprohackreason { get; set; }
    public short Wins { get; set; }

    // Chave estrangeira para PlayerMatchStatsEntity
    public long PlayerMatchStatsEntityId { get; set; }  // Garante a correspondência com PlayerMatchStats.Id

    public PlayerEntity Player { get; set; }
    public MatchEntity Match { get; set; }
    public PlayerMatchStatsEntity PlayerMatchStats { get; set; }  // Relacionamento com PlayerMatchStatsEntity
}

public class PlayerEntity
{
    [Key]
    public long Id { get; set; }
    public long PlayerId { get; set; }
    public long ClubId { get; set; }
    public string Playername { get; set; }

    public PlayerMatchStatsEntity PlayerMatchStats { get; set; }
    public long? PlayerMatchStatsId { get; set; }

    public ICollection<MatchPlayerEntity> MatchPlayers { get; set; } = new List<MatchPlayerEntity>();
}

public static class PlayerMatchStatsExtensions
{
    public static bool IsEqualTo(this PlayerMatchStatsEntity current, PlayerMatchStatsEntity other)
    {
        if (other == null) return false;

        return current.Aceleracao == other.Aceleracao &&
               current.Pique == other.Pique &&
               current.Finalizacao == other.Finalizacao &&
               current.Falta == other.Falta &&
               current.Cabeceio == other.Cabeceio &&
               current.ForcaDoChute == other.ForcaDoChute &&
               current.ChuteLonge == other.ChuteLonge &&
               current.Voleio == other.Voleio &&
               current.Penalti == other.Penalti &&
               current.Visao == other.Visao &&
               current.Cruzamento == other.Cruzamento &&
               current.Lancamento == other.Lancamento &&
               current.PasseCurto == other.PasseCurto &&
               current.Curva == other.Curva &&
               current.Agilidade == other.Agilidade &&
               current.Equilibrio == other.Equilibrio &&
               current.PosAtaqueInutil == other.PosAtaqueInutil &&
               current.ControleBola == other.ControleBola &&
               current.Conducao == other.Conducao &&
               current.Interceptacaos == other.Interceptacaos &&
               current.NocaoDefensiva == other.NocaoDefensiva &&
               current.DivididaEmPe == other.DivididaEmPe &&
               current.Carrinho == other.Carrinho &&
               current.Impulsao == other.Impulsao &&
               current.Folego == other.Folego &&
               current.Forca == other.Forca &&
               current.Reacao == other.Reacao &&
               current.Combatividade == other.Combatividade &&
               current.Frieza == other.Frieza &&
               current.ElasticidadeGL == other.ElasticidadeGL &&
               current.ManejoGL == other.ManejoGL &&
               current.ChuteGL == other.ChuteGL &&
               current.ReflexosGL == other.ReflexosGL &&
               current.PosGL == other.PosGL;
    }
}

public class PlayerMatchStatsEntity
{
    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(Player))]
    public long PlayerEntityId { get; set; }
    public PlayerEntity Player { get; set; }

    // Propriedades de estatísticas do jogador
    public short Aceleracao { get; set; }
    public short Pique { get; set; }
    public short Finalizacao { get; set; }
    public short Falta { get; set; }
    public short Cabeceio { get; set; }
    public short ForcaDoChute { get; set; }
    public short ChuteLonge { get; set; }
    public short Voleio { get; set; }
    public short Penalti { get; set; }
    public short Visao { get; set; }
    public short Cruzamento { get; set; }
    public short Lancamento { get; set; }
    public short PasseCurto { get; set; }
    public short Curva { get; set; }
    public short Agilidade { get; set; }
    public short Equilibrio { get; set; }
    public short PosAtaqueInutil { get; set; }
    public short ControleBola { get; set; }
    public short Conducao { get; set; }
    public short Interceptacaos { get; set; }
    public short NocaoDefensiva { get; set; }
    public short DivididaEmPe { get; set; }
    public short Carrinho { get; set; }
    public short Impulsao { get; set; }
    public short Folego { get; set; }
    public short Forca { get; set; }
    public short Reacao { get; set; }
    public short Combatividade { get; set; }
    public short Frieza { get; set; }
    public short ElasticidadeGL { get; set; }
    public short ManejoGL { get; set; }
    public short ChuteGL { get; set; }
    public short ReflexosGL { get; set; }
    public short PosGL { get; set; }

    public ICollection<MatchPlayerEntity> MatchPlayers { get; set; } = new List<MatchPlayerEntity>();

    public static PlayerMatchStatsEntity Parse(string input)
    {

        if (string.IsNullOrEmpty(input) || input == "NH")
            return new PlayerMatchStatsEntity();

        var parts = input.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 34)
            throw new ArgumentException("Input does not have the expected 34 fields.");

        var player = new PlayerMatchStatsEntity
        {
            Aceleracao = short.Parse(parts[0]),
            Pique = short.Parse(parts[1]),
            Agilidade = short.Parse(parts[2]),
            Equilibrio = short.Parse(parts[3]),
            Impulsao = short.Parse(parts[4]),
            Folego = short.Parse(parts[5]),
            Forca = short.Parse(parts[6]),
            Reacao = short.Parse(parts[7]),
            Combatividade = short.Parse(parts[8]),
            Frieza = short.Parse(parts[9]),
            Interceptacaos = short.Parse(parts[10]),
            PosAtaqueInutil = short.Parse(parts[11]),
            Visao = short.Parse(parts[12]),
            ControleBola = short.Parse(parts[13]),
            Cruzamento = short.Parse(parts[14]),
            Conducao = short.Parse(parts[15]),
            Finalizacao = short.Parse(parts[16]),
            Falta = short.Parse(parts[17]),
            Cabeceio = short.Parse(parts[18]),
            Lancamento = short.Parse(parts[19]),
            PasseCurto = short.Parse(parts[20]),
            NocaoDefensiva = short.Parse(parts[21]),
            ForcaDoChute = short.Parse(parts[22]),
            ChuteLonge = short.Parse(parts[23]),
            DivididaEmPe = short.Parse(parts[24]),
            Carrinho = short.Parse(parts[25]),
            Voleio = short.Parse(parts[26]),
            Curva = short.Parse(parts[27]),
            Penalti = short.Parse(parts[28]),
            ElasticidadeGL = short.Parse(parts[29]),
            ManejoGL = short.Parse(parts[30]),
            ChuteGL = short.Parse(parts[31]),
            ReflexosGL = short.Parse(parts[32]),
            PosGL = short.Parse(parts[33])
        };

        return player;
    }
}