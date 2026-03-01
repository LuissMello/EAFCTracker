using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EAFCMatchTracker.Domain.Entities;

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
