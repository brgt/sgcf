using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Atualiza limite operacional com semântica PATCH. SPEC §6.1.
/// NovoValorLimiteBrl null = preservar valor atual.
/// GarantiasExigidas null = preservar garantias atuais; lista vazia = remover todas; populada = substituir todas.
/// NovaDataVigenciaFim null = preservar; informado = encerrar vigência na data indicada (RV-01).
/// MotivoEncerramento null = preservar; informado = registrar motivo do encerramento (RV-01).
/// Campos de antecipação: quando o campo está ausente (null) no request, é preservado o valor atual.
/// </summary>
public sealed record UpdateLimiteBancoCommand(
    Guid LimiteId,
    decimal? NovoValorLimiteBrl = null,
    IReadOnlyList<CriarGarantiaExigidaItemRequest>? GarantiasExigidas = null,
    bool ConfigurarAntecipacao = false,
    string? PadraoAntecipacao = null,
    decimal? BreakFundingFeePct = null,
    decimal? TlaPctSobreSaldo = null,
    decimal? TlaPctPorMesRemanescente = null,
    decimal? ValorMinimoParcialPct = null,
    string? ObservacoesAntecipacao = null,
    DateOnly? NovaDataVigenciaFim = null,
    DateOnly? NovaDataVigenciaInicio = null,
    string? MotivoEncerramento = null) : IRequest<AtualizarLimiteBancoResponse>;

public sealed class UpdateLimiteBancoCommandValidator : AbstractValidator<UpdateLimiteBancoCommand>
{
    public UpdateLimiteBancoCommandValidator()
    {
        RuleFor(c => c.LimiteId).NotEmpty();

        RuleFor(c => c.NovoValorLimiteBrl)
            .GreaterThan(0m)
            .WithMessage("NovoValorLimiteBrl deve ser maior que zero.")
            .When(c => c.NovoValorLimiteBrl.HasValue);

        // Validate enum names only — XOR delegated to domain.
        RuleForEach(c => c.GarantiasExigidas)
            .ChildRules(g =>
                g.RuleFor(r => r.Tipo)
                 .NotEmpty()
                 .Must(v => Enum.TryParse<TipoGarantia>(v, ignoreCase: true, out _))
                 .WithMessage(r => $"Tipo de garantia inválido: '{r.Tipo}'. Valores aceitos: {string.Join(", ", Enum.GetNames<TipoGarantia>())}."))
            .When(c => c.GarantiasExigidas is not null);

        RuleFor(c => c.PadraoAntecipacao)
            .Must(v => Enum.TryParse<Domain.Common.PadraoAntecipacao>(v!, true, out _))
            .WithMessage($"PadraoAntecipacao deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Common.PadraoAntecipacao>())}.")
            .When(c => c.ConfigurarAntecipacao && c.PadraoAntecipacao is not null);

        RuleFor(c => c.MotivoEncerramento)
            .Null()
            .When(c => !c.NovaDataVigenciaFim.HasValue)
            .WithMessage("MotivoEncerramento só é válido quando NovaDataVigenciaFim também é informado.");

        RuleFor(c => c.BreakFundingFeePct).GreaterThanOrEqualTo(0).When(c => c.BreakFundingFeePct.HasValue);
        RuleFor(c => c.TlaPctSobreSaldo).GreaterThanOrEqualTo(0).When(c => c.TlaPctSobreSaldo.HasValue);
        RuleFor(c => c.TlaPctPorMesRemanescente).GreaterThanOrEqualTo(0).When(c => c.TlaPctPorMesRemanescente.HasValue);
        RuleFor(c => c.ValorMinimoParcialPct).GreaterThanOrEqualTo(0).When(c => c.ValorMinimoParcialPct.HasValue);
    }
}

public sealed class UpdateLimiteBancoCommandHandler(
    ILimiteBancoRepository repo,
    ILimiteGlobalBancoRepository limiteGlobalRepo,
    IClock clock)
    : IRequestHandler<UpdateLimiteBancoCommand, AtualizarLimiteBancoResponse>
{
    private static readonly DateTimeZone FusoBrasilia =
        DateTimeZoneProviders.Tzdb["America/Sao_Paulo"];

    public async Task<AtualizarLimiteBancoResponse> Handle(UpdateLimiteBancoCommand cmd, CancellationToken cancellationToken)
    {
        LimiteBanco limite = await repo.GetByIdTrackingAsync(cmd.LimiteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Limite '{cmd.LimiteId}' não encontrado.");

        if (cmd.NovoValorLimiteBrl.HasValue)
        {
            LocalDate hoje = clock.GetCurrentInstant().InZone(FusoBrasilia).Date;

            // LG-09: o novo valor do limite por modalidade não pode superar o limite global vigente do banco.
            LimiteGlobalBanco? limiteGlobal = await limiteGlobalRepo.GetVigenteByBancoAsync(limite.BancoId, hoje, cancellationToken);
            if (limiteGlobal is not null)
            {
                Money novoValorVerificacao = new(cmd.NovoValorLimiteBrl.Value, Moeda.Brl);
                if (novoValorVerificacao.MaiorQue(limiteGlobal.ValorLimiteBrl))
                {
                    throw new InvalidOperationException(
                        $"O valor do limite por modalidade ({novoValorVerificacao}) não pode superar o limite global vigente do banco ({limiteGlobal.ValorLimiteBrl}). [LG-09]");
                }
            }

            Money novoValor = new(cmd.NovoValorLimiteBrl.Value, Moeda.Brl);
            limite.Atualizar(clock, novoLimiteBrl: novoValor);
        }

        // RV-01: encerrar / ajustar vigência.
        LocalDate? novaDataVigenciaFim = cmd.NovaDataVigenciaFim?.ToLocalDate();
        LocalDate? novaDataVigenciaInicio = cmd.NovaDataVigenciaInicio?.ToLocalDate();

        if (novaDataVigenciaFim.HasValue || novaDataVigenciaInicio.HasValue)
        {
            // RV-01-B: verificar sobreposição excluindo o próprio limite.
            LocalDate inicioParaChecagem = novaDataVigenciaInicio ?? limite.DataVigenciaInicio;
            LimiteBanco? conflito = await repo.FindOverlappingAsync(
                limite.BancoId,
                limite.Modalidade,
                inicioParaChecagem,
                novaDataVigenciaFim,
                excluirId: limite.Id,
                cancellationToken: cancellationToken);

            if (conflito is not null)
            {
                string fimConflito = conflito.DataVigenciaFim.HasValue
                    ? conflito.DataVigenciaFim.Value.ToString("uuuu-MM-dd", null)
                    : "em aberto";

                throw new InvalidOperationException(
                    $"A nova vigência causa sobreposição com o limite '{conflito.Id}' " +
                    $"(vigência: {conflito.DataVigenciaInicio:uuuu-MM-dd} – {fimConflito}). [RV-01-B]");
            }

            limite.Atualizar(clock,
                novaDataVigenciaInicio: novaDataVigenciaInicio,
                novaDataVigenciaFim: novaDataVigenciaFim,
                motivoEncerramento: cmd.MotivoEncerramento);
        }
        else if (cmd.MotivoEncerramento is not null)
        {
            limite.Atualizar(clock, motivoEncerramento: cmd.MotivoEncerramento);
        }

        if (cmd.GarantiasExigidas is not null)
        {
            IEnumerable<GarantiaExigidaItemSpec> specs = cmd.GarantiasExigidas.Select(r => r.ParaSpec());
            limite.SubstituirGarantiasExigidas(specs, clock);
        }

        if (cmd.ConfigurarAntecipacao)
        {
            Domain.Common.PadraoAntecipacao? padrao = cmd.PadraoAntecipacao is not null
                ? Enum.Parse<Domain.Common.PadraoAntecipacao>(cmd.PadraoAntecipacao, true)
                : (Domain.Common.PadraoAntecipacao?)null;

            decimal? breakFrac = cmd.BreakFundingFeePct.HasValue
                ? Domain.Common.Percentual.De(cmd.BreakFundingFeePct.Value).AsDecimal
                : (decimal?)null;

            decimal? tlaSaldoFrac = cmd.TlaPctSobreSaldo.HasValue
                ? Domain.Common.Percentual.De(cmd.TlaPctSobreSaldo.Value).AsDecimal
                : (decimal?)null;

            decimal? tlaMesFrac = cmd.TlaPctPorMesRemanescente.HasValue
                ? Domain.Common.Percentual.De(cmd.TlaPctPorMesRemanescente.Value).AsDecimal
                : (decimal?)null;

            decimal? minParcialFrac = cmd.ValorMinimoParcialPct.HasValue
                ? Domain.Common.Percentual.De(cmd.ValorMinimoParcialPct.Value).AsDecimal
                : (decimal?)null;

            limite.ConfigurarAntecipacao(
                padrao,
                breakFrac,
                tlaSaldoFrac,
                tlaMesFrac,
                minParcialFrac,
                cmd.ObservacoesAntecipacao,
                clock);
        }

        repo.Update(limite);
        await repo.SaveChangesAsync(cancellationToken);

        LimiteBancoDto dto = LimiteBancoDto.From(limite);
        List<string> avisos = BuildAvisos(limite, novaDataVigenciaFim);
        return new AtualizarLimiteBancoResponse(dto, avisos.AsReadOnly());
    }

    private static List<string> BuildAvisos(LimiteBanco limite, LocalDate? novaDataVigenciaFim)
    {
        var avisos = new List<string>();

        if (novaDataVigenciaFim.HasValue && limite.ValorUtilizadoBrl.Valor > 0)
        {
            string valor = limite.ValorUtilizadoBrl.Valor.ToString("N0",
                System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            string dataFim = novaDataVigenciaFim.Value.ToString("uuuu-MM-dd", null);
            avisos.Add(
                $"Este limite possui BRL {valor} em utilização ativa. " +
                $"Contratos vinculados não são afetados, mas nenhuma nova cotação " +
                $"poderá usar este limite após {dataFim}.");
        }

        return avisos;
    }
}
