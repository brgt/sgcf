using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contabilidade;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;
using Sgcf.Domain.Hedge;

namespace Sgcf.Infrastructure.Persistence;

// Expression trees do not support switch expressions or throw expressions,
// so complex enum mappings use nested ternaries, and simple ones use Enum.Parse.
internal static class SgcfConverters
{
    internal static readonly ValueConverter<Moeda, string> Moeda =
        new(m => m.ToString().ToUpperInvariant(),
            s => Enum.Parse<Domain.Common.Moeda>(s, true));

    internal static readonly ValueConverter<ModalidadeContrato, string> Modalidade =
        new(
            m => m == ModalidadeContrato.Finimp ? "FINIMP"
               : m == ModalidadeContrato.Refinimp ? "REFINIMP"
               : m == ModalidadeContrato.Lei4131 ? "LEI_4131"
               : m == ModalidadeContrato.Nce ? "NCE"
               : m == ModalidadeContrato.BalcaoCaixa ? "BALCAO_CAIXA"
               : "FGI",
            s => s == "FINIMP" ? ModalidadeContrato.Finimp
               : s == "REFINIMP" ? ModalidadeContrato.Refinimp
               : s == "LEI_4131" ? ModalidadeContrato.Lei4131
               : s == "NCE" ? ModalidadeContrato.Nce
               : s == "BALCAO_CAIXA" ? ModalidadeContrato.BalcaoCaixa
               : ModalidadeContrato.Fgi);

    internal static readonly ValueConverter<StatusContrato, string> StatusContrato =
        new(s => s.ToString().ToUpperInvariant(),
            s => Enum.Parse<Domain.Contratos.StatusContrato>(s, true));

    internal static readonly ValueConverter<StatusParcela, string> StatusParcela =
        new(s => s.ToString().ToUpperInvariant(),
            s => Enum.Parse<Domain.Contratos.StatusParcela>(s, true));

    internal static readonly ValueConverter<TipoGarantia, string> TipoGarantia =
        new(
            t => t == Domain.Contratos.TipoGarantia.CdbCativo ? "CDB_CATIVO"
               : t == Domain.Contratos.TipoGarantia.Sblc ? "SBLC"
               : t == Domain.Contratos.TipoGarantia.Aval ? "AVAL"
               : t == Domain.Contratos.TipoGarantia.AlienacaoFiduciaria ? "ALIENACAO_FIDUCIARIA"
               : t == Domain.Contratos.TipoGarantia.Duplicatas ? "DUPLICATAS"
               : t == Domain.Contratos.TipoGarantia.RecebiveisCartao ? "RECEBIVEIS_CARTAO"
               : t == Domain.Contratos.TipoGarantia.BoletoBancario ? "BOLETO_BANCARIO"
               : "FGI",
            s => s == "CDB_CATIVO" ? Domain.Contratos.TipoGarantia.CdbCativo
               : s == "SBLC" ? Domain.Contratos.TipoGarantia.Sblc
               : s == "AVAL" ? Domain.Contratos.TipoGarantia.Aval
               : s == "ALIENACAO_FIDUCIARIA" ? Domain.Contratos.TipoGarantia.AlienacaoFiduciaria
               : s == "DUPLICATAS" ? Domain.Contratos.TipoGarantia.Duplicatas
               : s == "RECEBIVEIS_CARTAO" ? Domain.Contratos.TipoGarantia.RecebiveisCartao
               : s == "BOLETO_BANCARIO" ? Domain.Contratos.TipoGarantia.BoletoBancario
               : Domain.Contratos.TipoGarantia.Fgi);

    internal static readonly ValueConverter<StatusGarantia, string> StatusGarantia =
        new(s => s.ToString().ToUpperInvariant(),
            s => Enum.Parse<Domain.Contratos.StatusGarantia>(s, true));

    internal static readonly ValueConverter<TipoHedge, string> TipoHedge =
        new(
            t => t == Domain.Hedge.TipoHedge.NdfForward ? "NDF_FORWARD" : "NDF_COLLAR",
            s => s == "NDF_FORWARD" ? Domain.Hedge.TipoHedge.NdfForward : Domain.Hedge.TipoHedge.NdfCollar);

    internal static readonly ValueConverter<StatusHedge, string> StatusHedge =
        new(s => s.ToString().ToUpperInvariant(),
            s => Enum.Parse<Domain.Hedge.StatusHedge>(s, true));

    internal static readonly ValueConverter<TipoCotacao, string> TipoCotacao =
        new(
            t => t == Domain.Cotacoes.TipoCotacao.PtaxD0 ? "PTAX_D0"
               : t == Domain.Cotacoes.TipoCotacao.PtaxD1 ? "PTAX_D1"
               : t == Domain.Cotacoes.TipoCotacao.SpotIntraday ? "SPOT_INTRADAY"
               : "FIXING",
            s => s == "PTAX_D0" ? Domain.Cotacoes.TipoCotacao.PtaxD0
               : s == "PTAX_D1" ? Domain.Cotacoes.TipoCotacao.PtaxD1
               : s == "SPOT_INTRADAY" ? Domain.Cotacoes.TipoCotacao.SpotIntraday
               : Domain.Cotacoes.TipoCotacao.Fixing);

    internal static readonly ValueConverter<BaseCalculo, short> BaseCalculo =
        new(b => (short)(int)b, s => (Domain.Common.BaseCalculo)(int)s);

    internal static readonly ValueConverter<PadraoAntecipacao, short> PadraoAntecipacao =
        new(p => (short)(int)p, s => (Domain.Common.PadraoAntecipacao)(int)s);

    internal static readonly ValueConverter<NaturezaConta, string> NaturezaConta =
        new(n => n.ToString().ToUpperInvariant(),
            s => Enum.Parse<Domain.Contabilidade.NaturezaConta>(s, true));
}
