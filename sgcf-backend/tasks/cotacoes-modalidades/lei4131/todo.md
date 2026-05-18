# Todo — Cotações de Lei 4131/62

Lista de tarefas em ordem de execução. Marque cada item conforme concluir.
Plano completo em `plan.md` (mesmo diretório).

---

## Bloqueios externos

- [ ] **PRÉ-REQUISITO 1:** MVP de Cotações FINIMP em produção (SPEC v1.0 implementado até `ConverterEmContratoCommand`).
- [ ] **PRÉ-REQUISITO 2:** `tasks/garantias-em-limites/` — Fase 4 (pré-preenchimento de garantia) mergeada.
- [ ] **DECISÕES PENDENTES (Q1..Q6 do plan.md):** confirmadas com PO antes de iniciar Fase 3.

---

## Fase 1 — Domínio

- [ ] **Task 1.1** Adicionar `TaxaIrrfAaPercentual` (nullable) em `Proposta`
  - [ ] Propriedade pública + backing field decimal
  - [ ] Construtor `internal` aceita parâmetro opcional (compat preservada)
  - [ ] `ValidarInvariantes`: se HasValue, valor em `[0, 1]`
  - [ ] Cache de CET **não** é invalidado por mudança nesse campo
  - [ ] Testes unitários: criação com/sem IRRF; rejeições de valor < 0 e > 1

- [ ] **Checkpoint A** — Build verde + testes de domínio passam + revisão humana das ADs

---

## Fase 2 — Persistência

- [ ] **Task 2.1** Migration `S6_PropostaIrrf`
  - [ ] `dotnet ef migrations add S6_PropostaIrrf --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api`
  - [ ] Up: `AddColumn` nullable em `proposta.taxa_irrf_aa_percentual`
  - [ ] Down: `DropColumn`
  - [ ] Aplica sem afetar registros existentes

- [ ] **Task 2.2** `PropostaConfiguration` mapeia coluna nova
  - [ ] `HasColumnName("taxa_irrf_aa_percentual").HasPrecision(18, 8).IsRequired(false)`
  - [ ] Round-trip persiste/recupera valor preservando null

- [ ] **Checkpoint B** — Migration aplica/reverte limpo + round-trip OK

---

## Fase 3 — Application

- [ ] **Task 3.1** `RegistrarPropostaCommand` e `AtualizarPropostaCommand` aceitam `TaxaIrrfAaPercentual`
  - [ ] Records ganham campo opcional ao final
  - [ ] Validators aceitam `[0, 100]` quando HasValue
  - [ ] Handlers convertem percentual humano → fração antes de passar ao domínio
  - [ ] `PropostaDto.From` inclui o campo
  - [ ] Teste E2E: proposta Lei 4131 com IRRF 15 → 201; payload retorna 0.15
  - [ ] Regressão FINIMP sem o campo continua verde

- [ ] **Task 3.2** `CalculadoraIrrfEstimado` (helper puro Application)
  - [ ] Função pura: sem I/O, sem `IClock`
  - [ ] Retorna 0 quando `TaxaIrrfAaPercentual == null`
  - [ ] `Math.Round(..., 2, MidpointRounding.AwayFromZero)` no BRL final
  - [ ] `CompararPropostasQuery` expõe `IrrfEstimadoBrl` por proposta
  - [ ] Teste unitário com cenário documentado (USD 1M, 5%, 180d, IRRF 15%)

- [ ] **Task 3.3** Validar pré-preenchimento de garantia SBLC
  - [ ] Teste E2E: `LimiteBanco` Lei 4131 com `GarantiaExigidaLimite` `Sblc 100%` → `Proposta.GarantiaExigida` string correta
  - [ ] Confirmar `FormatadorGarantiaExigida` cobre Sblc; patch XS se necessário

- [ ] **Checkpoint C** — Application + comparativo retornam IRRF estimado; pré-preenchimento SBLC OK; Bruno valida manualmente até "Comparada"

---

## Fase 4 — Conversão em Contrato (Lei 4131)

- [ ] **Task 4.1** `ConverterEmContratoCommand` aceita `Lei4131ConversaoDetail`
  - [ ] Record `Lei4131ConversaoDetail(SblcNumero?, SblcBancoEmissor?, SblcValorUsd?, TemMarketFlex, BreakFundingFeePercentual?)`
  - [ ] Command record ganha campo opcional ao final
  - [ ] Validator: quando `cotacao.Modalidade == Lei4131`, payload é obrigatório
  - [ ] Compat: FINIMP continua sem mudança no payload
  - [ ] Teste E2E feliz + negativo (sem detail → 400)

- [ ] **Task 4.2** Branch Lei 4131 em `ConverterEmContratoCommandHandler`
  - [ ] Bloco `if (cotacao.Modalidade == ModalidadeContrato.Lei4131)` após branch FINIMP
  - [ ] Cria `Lei4131Detail` via `Lei4131Detail.Criar(...)`
  - [ ] Chama `contratoRepo.AddLei4131Detail(...)`
  - [ ] `ContratoDto.From` retorna com `lei4131Detail` populado
  - [ ] Snapshot JSON do contrato inclui campos Lei 4131
  - [ ] Golden E2E: cotação Lei 4131 USD 5M, SBLC USD 5M, taxa 6%, 360d, sem NDF → conversão atômica

- [ ] **Checkpoint D** — Conversão Lei 4131 cria Contrato + Lei4131Detail + EconomiaNegociacao + atualiza LimiteBanco atomicamente; regressão FINIMP intacta

---

## Fase 5 — CET e Tributação (Goldens)

- [ ] **Task 5.1** Golden case Lei 4131 USD com SBLC, sem NDF
  - [ ] JSON `tests/Sgcf.GoldenDataset/data/cotacao_lei4131_usd_sblc.json`
  - [ ] Cenário: USD 5M, taxa 6% a.a., prazo 360 dias, IOF 0.38%, SBLC 100%, PTAX 5,00
  - [ ] `expectedOutput.cetAaPercentual` validado via planilha

- [ ] **Task 5.2** Golden case Lei 4131 USD com NDF obrigatório
  - [ ] Cenário: USD 2M, taxa 5%, prazo 180d, NDF 2.5% a.a., SBLC 50%
  - [ ] Asserção: CET com NDF > CET sem NDF (mesmas demais variáveis)

- [ ] **Task 5.3** Property-based test de `CalculadoraIrrfEstimado`
  - [ ] IRRF >= 0 sempre
  - [ ] IRRF == 0 quando TaxaIrrfAaPercentual == null
  - [ ] Linearidade em TaxaIrrfAaPercentual

- [ ] **Checkpoint E** — Goldens Lei 4131 passam; property tests passam; sem regressão FINIMP

---

## Fase 6 — API, Bruno e Documentação

- [ ] **Task 6.1** Bruno collection — fluxo Lei 4131
  - [ ] Pasta nova (ex.: `12-CotacoesLei4131/` ou `10-Cotacoes/Lei4131/`)
  - [ ] Requests: criar cotação → adicionar banco com limite SBLC → registrar proposta com IRRF → aceitar → converter com `lei4131Detail`
  - [ ] Variáveis de ambiente atualizadas

- [ ] **Task 6.2** `docs/api/cotacoes.md` — apêndice Lei 4131
  - [ ] Campo `taxaIrrfAaPercentual` documentado
  - [ ] Payload de conversão `lei4131Detail`
  - [ ] Semântica de IRRF estimado (informativo, não entra no CET)
  - [ ] Reuse de garantia SBLC via `LimiteBanco`

- [ ] **Task 6.3** `docs/specs/cotacoes/SPEC.md` — apêndice
  - [ ] §11.2 atualizada (remover Lei 4131 do "out of scope")
  - [ ] Nova seção descrevendo modalidade Lei 4131 e referência cruzada
  - [ ] §18 Histórico atualizado: v1.1

- [ ] **Task 6.4** `CHANGELOG.md` v0.8.0
  - [ ] Bloco ADDITIVE — Cotações — Lei 4131/62
  - [ ] Bloco INTERNAL — Migration S6_PropostaIrrf

- [ ] **Checkpoint Final** — `dotnet test` verde; build limpo; Bruno OK; docs revisadas; PR pronto

---

## Perguntas pendentes (responder antes de iniciar)

- [ ] **Q1 (crítica):** IRRF como informativo (AD-3) ou incorporado ao CET?
- [ ] **Q2:** Capturar `CustoSblcAaPercentual` agora ou diferir?
- [ ] **Q3:** Modelar `PaisCredor` na proposta para auto-preencher alíquota IRRF?
- [ ] **Q4:** Estimativa atual de parcelas em `CalcularQuantidadeParcelas` é OK para Lei 4131 com prazo 1–3 anos?
- [ ] **Q5:** Confirmar que `LimiteBanco` Lei 4131 é separado do FINIMP (unique constraint já garante).
- [ ] **Q6:** Aceitar limitação do cross-rate (USD como proxy para EUR/JPY) no MVP?
