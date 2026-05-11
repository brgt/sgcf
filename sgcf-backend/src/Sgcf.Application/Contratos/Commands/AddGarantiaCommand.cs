using MediatR;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos.Commands;

// ── Payload records (type-specific optional groups) ───────────────────────────

public sealed record AddGarantiaCdbPayload(
    string BancoCustodia,
    string NumeroCdb,
    DateOnly DataEmissaoCdb,
    DateOnly DataVencimentoCdb,
    decimal? RendimentoAaPct,
    decimal? PercentualCdiPct,
    decimal? TaxaIrrfAplicacaoPct);

public sealed record AddGarantiaSblcPayload(
    string BancoEmissor,
    string PaisEmissor,
    string? SwiftCode,
    int ValidadeDias,
    decimal? ComissaoAaPct,
    string? NumeroSblc);

public sealed record AddGarantiaAvalPayload(
    string AvalistaTipo,
    string AvalistaNome,
    string AvalistaDocumento,
    decimal ValorAvalBrl,
    DateOnly? VigenciaAte);

public sealed record AddGarantiaAlienacaoPayload(
    string TipoBem,
    string DescricaoBem,
    decimal ValorAvaliadoBrl,
    string? MatriculaOuChassi,
    string? CartorioRegistro);

public sealed record AddGarantiaDuplicatasPayload(
    decimal PercentualDescontoPct,
    DateOnly VencimentoEscalonadoInicio,
    DateOnly VencimentoEscalonadoFim,
    int QtdDuplicatasCedidas,
    decimal ValorTotalDuplicatasBrl,
    DateOnly? InstrumentoCessaoData);

public sealed record AddGarantiaRecebiveisPayload(
    string OperadoraCartao,
    string TipoRecebivel,
    decimal PercentualFaturamentoPct,
    decimal? ValorMedioMensalBrl,
    int PrazoRecebimentoDias,
    string? TermoCessaoUrl);

public sealed record AddGarantiaBoletoPayload(
    string BancoEmissor,
    int QuantidadeBoletos,
    decimal ValorUnitarioBrl,
    DateOnly DataEmissaoInicial,
    DateOnly DataVencimentoInicial,
    DateOnly DataVencimentoFinal,
    string Periodicidade);

public sealed record AddGarantiaFgiPayload(
    string TipoFgi,
    decimal PercentualCoberturaPct,
    decimal? TaxaFgiAaPct,
    string? BancoIntermediario,
    string? CodigoOperacaoBndes);

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Comando para adicionar uma garantia polimórfica a um contrato existente.
/// Exatamente um dos payloads de tipo deve ser fornecido, correspondendo ao <c>Tipo</c>.
/// </summary>
public sealed record AddGarantiaCommand(
    Guid ContratoId,
    string Tipo,
    decimal ValorBrl,
    DateOnly DataConstituicao,
    DateOnly? DataLiberacaoPrevista,
    string? Observacoes,
    string CreatedBy,
    AddGarantiaCdbPayload? Cdb = null,
    AddGarantiaSblcPayload? Sblc = null,
    AddGarantiaAvalPayload? Aval = null,
    AddGarantiaAlienacaoPayload? Alienacao = null,
    AddGarantiaDuplicatasPayload? Duplicatas = null,
    AddGarantiaRecebiveisPayload? Recebiveis = null,
    AddGarantiaBoletoPayload? Boleto = null,
    AddGarantiaFgiPayload? FgiDetail = null)
    : IRequest<GarantiaDto>;

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Processa <see cref="AddGarantiaCommand"/>:
/// 1. Carrega o contrato para validações e cálculo de percentual.
/// 2. Aplica validações de negócio (bloqueantes e alertas).
/// 3. Cria a entidade mestre <see cref="Garantia"/> e o detalhe correspondente ao tipo.
/// 4. Persiste ambos e retorna o DTO com alertas calculados.
/// </summary>
public sealed class AddGarantiaCommandHandler(
    IContratoRepository contratoRepo,
    IGarantiaRepository garantiaRepo,
    IClock clock)
    : IRequestHandler<AddGarantiaCommand, GarantiaDto>
{
    public async Task<GarantiaDto> Handle(AddGarantiaCommand command, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TipoGarantia>(command.Tipo, ignoreCase: true, out TipoGarantia tipo))
        {
            throw new ArgumentException(
                $"TipoGarantia inválido: '{command.Tipo}'. Valores aceitos: {string.Join(", ", Enum.GetNames<TipoGarantia>())}.");
        }

        Contrato contrato = await contratoRepo.GetByIdAsync(command.ContratoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Contrato com Id '{command.ContratoId}' não encontrado.");

        LocalDate dataConstituicao = ToLocalDate(command.DataConstituicao);
        LocalDate? dataLiberacaoPrevista = command.DataLiberacaoPrevista.HasValue
            ? ToLocalDate(command.DataLiberacaoPrevista.Value)
            : null;

        // ── Validações bloqueantes ────────────────────────────────────────────

        if (dataConstituicao > contrato.DataVencimento)
        {
            throw new ArgumentException(
                "Não é possível constituir garantia após o vencimento do contrato.");
        }

        if (tipo == TipoGarantia.CdbCativo && command.Cdb is not null)
        {
            LocalDate vencCdb = ToLocalDate(command.Cdb.DataVencimentoCdb);
            if (vencCdb < contrato.DataVencimento)
            {
                throw new ArgumentException(
                    "O vencimento do CDB cativo deve ser igual ou posterior ao vencimento do contrato.");
            }
        }

        if (tipo == TipoGarantia.RecebiveisCartao && command.Recebiveis is not null)
        {
            decimal totalExistente = await garantiaRepo.GetTotalPercentualFaturamentoCartaoAsync(
                command.ContratoId, cancellationToken);
            // totalExistente is already a fraction; convert new pct to fraction for comparison
            decimal novoTotalFracao = totalExistente + command.Recebiveis.PercentualFaturamentoPct / 100m;
            if (novoTotalFracao > 1m)
            {
                decimal novoTotalPct = novoTotalFracao * 100m;
                throw new ArgumentException(
                    $"Comprometimento total de faturamento de cartão ({novoTotalPct:F1}%) excederia 100%.");
            }
        }

        // ── Criação da garantia mestre ────────────────────────────────────────

        // Convert principal to BRL for ratio calculation.
        // When contrato is in foreign currency, principalBrl is only an approximation
        // without a real FX rate — we store null in that case and set the alert.
        decimal? principalBrlParaCalculo = contrato.Moeda == Moeda.Brl
            ? contrato.ValorPrincipal.Valor
            : null;

        Money valorBrl = new(command.ValorBrl, Moeda.Brl);

        Garantia garantia = Garantia.Criar(
            contratoId: command.ContratoId,
            tipo: tipo,
            valorBrl: valorBrl,
            principalBrlParaCalculo: principalBrlParaCalculo,
            dataConstituicao: dataConstituicao,
            dataLiberacaoPrevista: dataLiberacaoPrevista,
            observacoes: command.Observacoes,
            createdBy: command.CreatedBy,
            clock: clock);

        garantiaRepo.Add(garantia);

        // ── Criação do detalhe específico do tipo ─────────────────────────────

        CriarDetalhe(garantia.Id, tipo, command);

        await garantiaRepo.SaveChangesAsync(cancellationToken);

        // ── Alertas não-bloqueantes ───────────────────────────────────────────

        List<string> alertas = ComputarAlertas(garantia, contrato, principalBrlParaCalculo);

        return ToDto(garantia, alertas, command);
    }

    private void CriarDetalhe(Guid garantiaId, TipoGarantia tipo, AddGarantiaCommand cmd)
    {
        switch (tipo)
        {
            case TipoGarantia.CdbCativo:
            {
                AddGarantiaCdbPayload cdb = cmd.Cdb
                    ?? throw new ArgumentException("Payload Cdb é obrigatório para tipo CdbCativo.");
                garantiaRepo.AddCdbCativoDetail(GarantiaCdbCativoDetail.Criar(
                    garantiaId,
                    cdb.BancoCustodia,
                    cdb.NumeroCdb,
                    ToLocalDate(cdb.DataEmissaoCdb),
                    ToLocalDate(cdb.DataVencimentoCdb),
                    cdb.RendimentoAaPct,
                    cdb.PercentualCdiPct,
                    cdb.TaxaIrrfAplicacaoPct));
                break;
            }
            case TipoGarantia.Sblc:
            {
                AddGarantiaSblcPayload sblc = cmd.Sblc
                    ?? throw new ArgumentException("Payload Sblc é obrigatório para tipo Sblc.");
                garantiaRepo.AddSblcDetail(GarantiaSblcDetail.Criar(
                    garantiaId,
                    sblc.BancoEmissor,
                    sblc.PaisEmissor,
                    sblc.SwiftCode,
                    sblc.ValidadeDias,
                    sblc.ComissaoAaPct,
                    sblc.NumeroSblc));
                break;
            }
            case TipoGarantia.Aval:
            {
                AddGarantiaAvalPayload aval = cmd.Aval
                    ?? throw new ArgumentException("Payload Aval é obrigatório para tipo Aval.");
                LocalDate? vigenciaAte = aval.VigenciaAte.HasValue
                    ? ToLocalDate(aval.VigenciaAte.Value)
                    : null;
                garantiaRepo.AddAvalDetail(GarantiaAvalDetail.Criar(
                    garantiaId,
                    aval.AvalistaTipo,
                    aval.AvalistaNome,
                    aval.AvalistaDocumento,
                    new Money(aval.ValorAvalBrl, Moeda.Brl),
                    vigenciaAte));
                break;
            }
            case TipoGarantia.AlienacaoFiduciaria:
            {
                AddGarantiaAlienacaoPayload ali = cmd.Alienacao
                    ?? throw new ArgumentException("Payload Alienacao é obrigatório para tipo AlienacaoFiduciaria.");
                garantiaRepo.AddAlienacaoFiduciariaDetail(GarantiaAlienacaoFiduciariaDetail.Criar(
                    garantiaId,
                    ali.TipoBem,
                    ali.DescricaoBem,
                    new Money(ali.ValorAvaliadoBrl, Moeda.Brl),
                    ali.MatriculaOuChassi,
                    ali.CartorioRegistro));
                break;
            }
            case TipoGarantia.Duplicatas:
            {
                AddGarantiaDuplicatasPayload dup = cmd.Duplicatas
                    ?? throw new ArgumentException("Payload Duplicatas é obrigatório para tipo Duplicatas.");
                LocalDate? cessaoData = dup.InstrumentoCessaoData.HasValue
                    ? ToLocalDate(dup.InstrumentoCessaoData.Value)
                    : null;
                garantiaRepo.AddDuplicatasDetail(GarantiaDuplicatasDetail.Criar(
                    garantiaId,
                    dup.PercentualDescontoPct,
                    ToLocalDate(dup.VencimentoEscalonadoInicio),
                    ToLocalDate(dup.VencimentoEscalonadoFim),
                    dup.QtdDuplicatasCedidas,
                    new Money(dup.ValorTotalDuplicatasBrl, Moeda.Brl),
                    cessaoData));
                break;
            }
            case TipoGarantia.RecebiveisCartao:
            {
                AddGarantiaRecebiveisPayload rec = cmd.Recebiveis
                    ?? throw new ArgumentException("Payload Recebiveis é obrigatório para tipo RecebiveisCartao.");
                garantiaRepo.AddRecebiveisCartaoDetail(GarantiaRecebiveisCartaoDetail.Criar(
                    garantiaId,
                    rec.OperadoraCartao,
                    rec.TipoRecebivel,
                    rec.PercentualFaturamentoPct,
                    rec.ValorMedioMensalBrl,
                    rec.PrazoRecebimentoDias,
                    rec.TermoCessaoUrl));
                break;
            }
            case TipoGarantia.BoletoBancario:
            {
                AddGarantiaBoletoPayload bol = cmd.Boleto
                    ?? throw new ArgumentException("Payload Boleto é obrigatório para tipo BoletoBancario.");
                garantiaRepo.AddBoletoBancarioDetail(GarantiaBoletoBancarioDetail.Criar(
                    garantiaId,
                    bol.BancoEmissor,
                    bol.QuantidadeBoletos,
                    new Money(bol.ValorUnitarioBrl, Moeda.Brl),
                    ToLocalDate(bol.DataEmissaoInicial),
                    ToLocalDate(bol.DataVencimentoInicial),
                    ToLocalDate(bol.DataVencimentoFinal),
                    bol.Periodicidade));
                break;
            }
            case TipoGarantia.Fgi:
            {
                AddGarantiaFgiPayload fgi = cmd.FgiDetail
                    ?? throw new ArgumentException("Payload FgiDetail é obrigatório para tipo Fgi.");
                garantiaRepo.AddFgiDetail(GarantiaFgiDetail.Criar(
                    garantiaId,
                    fgi.TipoFgi,
                    fgi.PercentualCoberturaPct,
                    fgi.TaxaFgiAaPct,
                    fgi.BancoIntermediario,
                    fgi.CodigoOperacaoBndes));
                break;
            }
            default:
                throw new InvalidOperationException($"TipoGarantia '{tipo}' não mapeado para detalhe.");
        }
    }

    private static List<string> ComputarAlertas(
        Garantia garantia,
        Contrato contrato,
        decimal? principalBrlParaCalculo)
    {
        List<string> alertas = [];

        // Alerta: cobertura abaixo de 100% do principal (somente quando é possível calcular)
        if (principalBrlParaCalculo.HasValue && principalBrlParaCalculo.Value > 0m)
        {
            decimal coberturaPct = garantia.PercentualPrincipal?.AsHumano ?? 0m;
            if (coberturaPct < 100m)
            {
                alertas.Add(
                    $"Atenção: Cobertura da garantia é {coberturaPct:F1}% do principal do contrato.");
            }
        }

        // Alerta crítico: operação em moeda estrangeira sem cobertura cambial (NDF)
        // Phase 6 will wire the real NDF check; for now always alert on foreign-currency contracts.
        if (contrato.Moeda != Moeda.Brl)
        {
            alertas.Add(
                "ALERTA CRÍTICO: Operação em moeda estrangeira sem cobertura cambial (NDF). Verifique exposição cambial.");
        }

        return alertas;
    }

    private static GarantiaDto ToDto(Garantia g, List<string> alertas, AddGarantiaCommand cmd)
    {
        return new GarantiaDto(
            Id: g.Id,
            ContratoId: g.ContratoId,
            Tipo: g.Tipo.ToString(),
            ValorBrl: g.ValorBrl.Valor,
            PercentualPrincipalPct: g.PercentualPrincipal?.AsHumano,
            DataConstituicao: g.DataConstituicao.ToString(),
            DataLiberacaoPrevista: g.DataLiberacaoPrevista?.ToString(),
            DataLiberacaoEfetiva: g.DataLiberacaoEfetiva?.ToString(),
            Status: g.Status.ToString(),
            Observacoes: g.Observacoes,
            Alertas: alertas.AsReadOnly(),
            Cdb: cmd.Cdb is null ? null : MapCdbDto(cmd.Cdb),
            Sblc: cmd.Sblc is null ? null : MapSblcDto(cmd.Sblc),
            Aval: cmd.Aval is null ? null : MapAvalDto(cmd.Aval),
            Alienacao: cmd.Alienacao is null ? null : MapAlienacaoDto(cmd.Alienacao),
            Duplicatas: cmd.Duplicatas is null ? null : MapDuplicatasDto(cmd.Duplicatas),
            Recebiveis: cmd.Recebiveis is null ? null : MapRecebiveisDto(cmd.Recebiveis),
            Boleto: cmd.Boleto is null ? null : MapBoletoDto(cmd.Boleto),
            FgiDetalhe: cmd.FgiDetail is null ? null : MapFgiDto(cmd.FgiDetail));
    }

    private static GarantiaCdbDto MapCdbDto(AddGarantiaCdbPayload p) =>
        new(p.BancoCustodia, p.NumeroCdb,
            p.DataEmissaoCdb.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            p.DataVencimentoCdb.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            p.RendimentoAaPct, p.PercentualCdiPct, p.TaxaIrrfAplicacaoPct);

    private static GarantiaSblcDto MapSblcDto(AddGarantiaSblcPayload p) =>
        new(p.BancoEmissor, p.PaisEmissor, p.SwiftCode, p.ValidadeDias, p.ComissaoAaPct, p.NumeroSblc);

    private static GarantiaAvalDto MapAvalDto(AddGarantiaAvalPayload p) =>
        new(p.AvalistaTipo, p.AvalistaNome, p.AvalistaDocumento, p.ValorAvalBrl,
            p.VigenciaAte?.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

    private static GarantiaAlienacaoDto MapAlienacaoDto(AddGarantiaAlienacaoPayload p) =>
        new(p.TipoBem, p.DescricaoBem, p.ValorAvaliadoBrl, p.MatriculaOuChassi, p.CartorioRegistro);

    private static GarantiaDuplicatasDto MapDuplicatasDto(AddGarantiaDuplicatasPayload p) =>
        new(p.PercentualDescontoPct,
            p.VencimentoEscalonadoInicio.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            p.VencimentoEscalonadoFim.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            p.QtdDuplicatasCedidas,
            p.ValorTotalDuplicatasBrl,
            p.InstrumentoCessaoData?.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

    private static GarantiaRecebiveisDto MapRecebiveisDto(AddGarantiaRecebiveisPayload p) =>
        new(p.OperadoraCartao, p.TipoRecebivel, p.PercentualFaturamentoPct,
            p.ValorMedioMensalBrl, p.PrazoRecebimentoDias, p.TermoCessaoUrl);

    private static GarantiaBoletoBancarioDto MapBoletoDto(AddGarantiaBoletoPayload p) =>
        new(p.BancoEmissor, p.QuantidadeBoletos, p.ValorUnitarioBrl,
            p.DataEmissaoInicial.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            p.DataVencimentoInicial.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            p.DataVencimentoFinal.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            p.Periodicidade);

    private static GarantiaFgiDto MapFgiDto(AddGarantiaFgiPayload p) =>
        new(p.TipoFgi, p.PercentualCoberturaPct, p.TaxaFgiAaPct, p.BancoIntermediario, p.CodigoOperacaoBndes);

    private static LocalDate ToLocalDate(DateOnly d) => new(d.Year, d.Month, d.Day);
}
