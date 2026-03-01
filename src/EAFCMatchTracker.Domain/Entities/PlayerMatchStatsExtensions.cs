namespace EAFCMatchTracker.Domain.Entities;

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
