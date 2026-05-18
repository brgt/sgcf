# Todo — Cotações de Capital de Giro

Lista de tarefas em ordem de execução. Marque cada item conforme concluir.
Plano completo em `plan.md` (mesmo diretório).

---

## Bloqueio externo

- [ ] **PRÉ-REQUISITO Q1:** Alinhamento com plano FGI (`tasks/cotacoes-modalidades/fgi/plan.md`) — confirmar que Task 3.3 deste plano cobre `CapitalDeGiro + TemFgi` e o plano FGI cobre `ModalidadeContrato.Fgi` puro
- [ ] **PRÉ-REQUISITO Q2/Q3/Q4/Q5/Q6:** Decisões registradas em `plan.md` §6 respondidas pelo PO

---

## Fase 1 — Domínio

- [ ] **Task 1.2** Criar `TiposProdutoCaixa` (whitelist)
  - [ ] Constantes: Proger, Fco, BndesAutomatico, ConstrucCard, Outros
  - [ ] Método `IsValid(string)` case-insensitive + trim
  - [ ] Lista `Todos`
  - [ ] Testes unitários cobrindo válidos, inválidos, case-insensitive

- [ ] **Task 1.1** Estender `Proposta` com campos opcionais Capital de Giro
  - [ ] `TipoProdutoCaixa` (string?), `TemFgiPrevisto` (bool), `NumeroOperacaoCaixa` (string?)
  - [ ] `Cotacao.AdicionarProposta` recebe os 3 campos como opcionais
  - [ ] Validação: TipoProdutoCaixa obrigatório quando modalidade = CapitalDeGiro
  - [ ] Validação: TipoProdutoCaixa deve estar na whitelist (Task 1.2)
  - [ ] Validação: TemFgiPrevisto só permitido em CapitalDeGiro
  - [ ] Testes em `PropostaCapitalDeGiroTests.cs`
  - [ ] Regressão FINIMP intocada

- [ ] **Checkpoint A** — Build verde + testes de domínio + revisão humana das invariantes

---

## Fase 2 — Persistência

- [ ] **Task 2.1** Migration `S6_PropostaCamposCapitalDeGiro`
  - [ ] Colunas: `tipo_produto_caixa` (varchar(40) nullable), `tem_fgi_previsto` (bool not null default false), `numero_operacao_caixa` (varchar(60) nullable)
  - [ ] `dotnet ef migrations add` limpo
  - [ ] `dotnet ef database update` aplica sem afetar propostas FINIMP existentes
  - [ ] `dotnet ef migrations remove` reverte

- [ ] **Task 2.2** EF Configuration de `Proposta`
  - [ ] `PropostaConfiguration` mapeia os 3 campos novos com max length corretos
  - [ ] Round-trip persistência validado via teste de integração

- [ ] **Checkpoint B** — Migration aplica/reverte limpo + round-trip OK

---

## Fase 3 — Application / API

- [ ] **Task 3.1** `CriarCotacaoCommand` torna PTAX condicional
  - [ ] PTAX consultada apenas para modalidades em moeda estrangeira (Finimp, Lei4131)
  - [ ] Para CapitalDeGiro: `PtaxUsadaUsdBrl = 1.0m`
  - [ ] Validar invariante §3.2 regra 7 (Cotacao.Criar) — possível S6b se rejeitar
  - [ ] Teste unitário: mock fxRepo não chamado para CapitalDeGiro
  - [ ] Teste E2E: cotação CapitalDeGiro sem PTAX cadastrada → 201
  - [ ] Regressão FINIMP: sem PTAX → 400

- [ ] **Task 3.2** `RegistrarPropostaCommand` aceita campos Capital de Giro
  - [ ] Record + handler aceitam 3 campos opcionais
  - [ ] Validator: TipoProdutoCaixa obrigatório quando cotação CapitalDeGiro
  - [ ] Validator: rejeita ExigeNdf=true em CapitalDeGiro
  - [ ] Validator: rejeita moeda ≠ BRL em CapitalDeGiro
  - [ ] `ObterPtaxEfetivaAsync` retorna 1.0 para BRL — confirmar
  - [ ] Teste E2E: registrar proposta PROGER → 201 com CET calculado
  - [ ] Teste E2E: ExigeNdf=true em CapitalDeGiro → 400

- [ ] **Task 3.3** `ConverterEmContratoCommand` cria `CapitalDeGiroDetail` (+ opcional `FgiDetail`)
  - [ ] Parâmetros opcionais no command: NumeroOperacaoCaixa, TipoProdutoCaixaFinal, TaxaFgiAaPct, PercentualCobertoFgiPct, NumeroOperacaoFgi
  - [ ] Branch CapitalDeGiro: cria `CapitalDeGiroDetail`
  - [ ] Sub-branch TemFgi: cria `FgiDetail`
  - [ ] Validação: TemFgi=true sem TaxaFgiAaPct → 400
  - [ ] NÃO dispara GerarCronograma (já bloqueado upstream)
  - [ ] `EconomiaNegociacao` criada com CET estimado (regra atual)
  - [ ] `ContratoDto` retorna ambos os details quando aplicável
  - [ ] Teste E2E: conversão CapitalDeGiro puro → 200 sem cronograma
  - [ ] Teste E2E: conversão CapitalDeGiro + FGI → 200 com ambos details
  - [ ] Fluxo encadeado: converter + importar cronograma manual

- [ ] **Task 3.4** DTOs e responses estendidos
  - [ ] `PropostaDto.From(...)` mapeia 3 campos novos
  - [ ] OpenAPI/Bruno refletem mudanças

- [ ] **Checkpoint C** — Fluxo completo via Bruno + suite E2E verde + revisão humana

---

## Fase 4 — Cronograma externo

- [ ] **Task 4.1** Documentar fluxo "Converter → Importar cronograma"
  - [ ] `ContratoDto` (ou wrapper) inclui `proximoPassoSugerido` quando CapitalDeGiro
  - [ ] `docs/api/cotacoes.md` descreve sequência obrigatória
  - [ ] Bruno: folder encadeado "Fluxo Capital de Giro — completo"

- [ ] **Checkpoint D** — Encadeamento validado manualmente

---

## Fase 5 — Documentação e Golden Dataset

- [ ] **Task 5.1** `docs/specs/cotacoes/SPEC.md` atualizado
  - [ ] §1 modalidades MVP inclui CapitalDeGiro
  - [ ] §11.2 remove CapitalDeGiro do fora-de-escopo
  - [ ] Nova §5.4: CET estimado vs CET real
  - [ ] §3.2 invariante 7 revisada (PTAX condicional)
  - [ ] §13 atualizada com decisões deste plano

- [ ] **Task 5.2** `docs/api/cotacoes.md` atualizado
  - [ ] Seção Capital de Giro
  - [ ] Exemplos POST com modalidade=CapitalDeGiro
  - [ ] Exemplos PROGER e PROGER+FGI
  - [ ] Linka fluxo importação de cronograma

- [ ] **Task 5.3** Bruno collection
  - [ ] Folder `Cotacoes/Modalidade-CapitalDeGiro/` numerado 01–07

- [ ] **Task 5.4** Golden dataset
  - [ ] `cotacao-balcao-caixa-proger.json` (cenário PROGER R$ 500k / 24m / 9% a.a. / Price)
  - [ ] Opcional: cenário PROGER + FGI
  - [ ] `dotnet test tests/Sgcf.GoldenDataset` verde

- [ ] **Task 5.5** CHANGELOG v0.7.0
  - [ ] ADDITIVE — Cotações — Capital de Giro
  - [ ] INTERNAL — Migration S6

- [ ] **Checkpoint Final** — `dotnet test` verde + build limpo + Bruno + docs revisadas + PR pronto

---

## Perguntas pendentes (responder antes de iniciar)

- [ ] Q1: FGI dentro de CapitalDeGiro vs FGI standalone — alinhamento com plano FGI
- [ ] Q2: Taxa FGI conhecida na Cotação ou só na conversão? CET incorpora FGI no MVP?
- [ ] Q3: Recálculo de CET pós-importação de cronograma — implementar ou diferir?
- [ ] Q4: `LimiteBanco` por TipoProduto ou agregado por modalidade?
- [ ] Q5: IOF crédito BRL — tratamento atual do `CalculadoraCet` (linha 157) é suficiente?
- [ ] Q6: `NumeroOperacaoCaixa` apenas em `CapitalDeGiroDetail` ou também em `Proposta`?
