# Todo — Cotações de NCE

Lista de tarefas em ordem de execução. Marque cada item conforme concluir.
Plano completo em `plan.md` (mesmo diretório).

---

## Bloqueios externos

- [ ] Nenhum bloqueio externo. Pré-condições: módulo de Cotações FINIMP mergeado em `main` (já está); `NceDetail` mapeado em `Contratos` (já está).

---

## Fase 1 — Domínio + Persistência

- [ ] **Task 1.1** Tornar `Cotacao.PtaxUsadaUsdBrl` nullable
  - [ ] Alterar tipo `decimal → decimal?` em `src/Sgcf.Domain/Cotacoes/Cotacao.cs`
  - [ ] Factory `Cotacao.Criar` aceita `decimal? ptaxUsadaUsdBrl`
  - [ ] Invariante: PTAX obrigatória se modalidade ∈ { Finimp, Lei4131, Refinimp }
  - [ ] Testes unitários: NCE sem PTAX (ok), FINIMP sem PTAX (rejeita)
  - [ ] Suite de domínio existente continua verde

- [ ] **Task 1.2** Overload de `CalculadoraCet.CalcularCet` para PTAX opcional
  - [ ] Adicionar overload `CalcularCet(Proposta, decimal? ptax, LocalDate, decimal? override)`
  - [ ] Overload original preservado intacto
  - [ ] Lança `ArgumentException` se proposta não-BRL e PTAX null
  - [ ] Property test: CET BRL é o mesmo com PTAX=null vs PTAX=1m

- [ ] **Task 1.3** Migration `S6_NcePtaxOpcional`
  - [ ] `dotnet ef migrations add S6_NcePtaxOpcional`
  - [ ] Up: `ALTER COLUMN ptax_usada_usd_brl DROP NOT NULL`
  - [ ] Down: documenta restrição (NCE existente bloqueia rollback)
  - [ ] `CotacaoConfiguration` ajustado para `IsRequired(false)`

- [ ] **Checkpoint A** — Build limpo + testes de domínio verdes + migration aplica/reverte + revisão humana

---

## Fase 2 — Application

- [ ] **Task 2.1** `CriarCotacaoCommand` aceita NCE sem PTAX
  - [ ] Branch condicional: PTAX só é buscada para modalidades cambiais (Finimp, Lei4131, Refinimp)
  - [ ] Para NCE/CapitalDeGiro/Fgi: cotação criada com `PtaxUsadaUsdBrl=null`
  - [ ] Testes: criar NCE sem PTAX cadastrada → sucesso; criar FINIMP sem PTAX → falha

- [ ] **Task 2.2** `RegistrarPropostaCommand` valida regras NCE
  - [ ] Guard no handler: NCE ⇒ `MoedaOriginal == BRL`
  - [ ] Guard no handler: NCE ⇒ `ExigeNdf == false`
  - [ ] Mensagens de erro orientativas
  - [ ] Testes E2E: payloads inválidos → 400; payload válido → 201

- [ ] **Task 2.3** `ConverterEmContratoCommand` cria `NceDetail`
  - [ ] Adicionar `NceNumero`, `DataEmissao`, `BancoMandatario` ao command record (opcionais)
  - [ ] Branch `if Modalidade == Nce` → cria `NceDetail` via `NceDetail.Criar`
  - [ ] Chamar `contratoRepo.AddNceDetail(detail)`
  - [ ] `ContratoDto.From` retorna `NceDetail` quando aplicável
  - [ ] Refatorar cálculo de `valorPrincipalBrl` para tolerar `PtaxUsadaUsdBrl` null (sempre cai no branch BRL para NCE)
  - [ ] Testes E2E: converter NCE com/sem campos → contrato persistido corretamente

- [ ] **Task 2.4** Verificar `IContratoRepository.AddNceDetail` exposto
  - [ ] Confirmar declaração na interface
  - [ ] Elevar se estiver apenas no concrete

- [ ] **Checkpoint B** — Application.Tests + IntegrationTests verdes + Bruno manual NCE rodado + CET bate com planilha

---

## Fase 3 — API + Bruno collection

- [ ] **Task 3.1** Endpoint `POST /converter-em-contrato` aceita campos NCE
  - [ ] Mapear payload JSON com `nceNumero`, `dataEmissao`, `bancoMandatario`
  - [ ] Manter compatibilidade FINIMP
  - [ ] Swagger renderiza
  - [ ] Teste E2E round-trip JSON

- [ ] **Task 3.2** Bruno collection — requests NCE
  - [ ] `criar-cotacao-nce.bru` (cenário sem PTAX)
  - [ ] `registrar-proposta-nce.bru` (BRL, juros trimestrais, Aval)
  - [ ] `comparar-propostas-nce.bru`
  - [ ] `converter-em-contrato-nce.bru` (com campos NCE)
  - [ ] `11-LimitesBanco/criar-limite-nce.bru`

- [ ] **Checkpoint C** — Fluxo manual ponta a ponta NCE no Bruno verde + sem regressão FINIMP

---

## Fase 4 — Golden Dataset + Tests

- [ ] **Task 4.1** Cenário golden NCE BRL com juros trimestrais
  - [ ] `tests/Sgcf.GoldenDataset/data/cotacoes/nce-brl-juros-trimestrais.json`
  - [ ] Valores `expectedOutput` validados via planilha por PO
  - [ ] Teste `[Theory]` carrega e compara campo a campo
  - [ ] Tolerância ≤ 0,01% absoluto sobre CET

- [ ] **Task 4.2** Property tests específicos de NCE
  - [ ] CET NCE BRL é invariante a PTAX
  - [ ] CET NCE BRL == CET FINIMP BRL com mesmos parâmetros + PTAX=1
  - [ ] Periodicidade Bullet+Trimestral: CET ≈ taxa+spread (juros simples)

- [ ] **Checkpoint D** — Golden verde + properties em ≥1000 amostras + cobertura ≥80%

---

## Fase 5 — Documentação

- [ ] **Task 5.1** `docs/specs/cotacoes/SPEC.md`
  - [ ] §11.2 atualizada (NCE saiu de out-of-scope)
  - [ ] Nova §19 "Extensão NCE"
  - [ ] Tabela de invariantes ajustada

- [ ] **Task 5.2** `docs/api/cotacoes.md`
  - [ ] Exemplo payload `POST /cotacoes` NCE
  - [ ] Exemplo payload `POST /converter-em-contrato` NCE
  - [ ] Tabela de campos opcionais por modalidade

- [ ] **Task 5.3** `CHANGELOG.md` v0.7.0
  - [ ] Bloco `ADDITIVE — Cotações — Modalidade NCE`
  - [ ] Bloco `BREAKING-INTERNO — Cotacao.PtaxUsadaUsdBrl nullable`
  - [ ] Migration S6 documentada

- [ ] **Checkpoint Final** — `dotnet test` verde, docs revisadas, PR pronto

---

## Perguntas pendentes (responder antes de iniciar)

- [ ] Q1: Base de anualização NCE — 360 (mantém) ou 252 (BACEN para BRL)?
- [ ] Q2: AD-1 inclui Refinimp em "sem PTAX"? (default: não — Refinimp é cambial)
- [ ] Q3: `Proposta.IofPercentual` cobre IOF crédito (0,38% + 0,0041% a.d.) ou precisa estender?
- [ ] Q4: Periodicidades aceitas para NCE — todas do enum ou subset?
- [ ] Q5: Validade temporal do snapshot de mercado para NCE — 0 (sem validade)?
- [ ] Q6: `RefreshCotacaoMercadoCommand` para NCE — no-op com aviso ou erro?
