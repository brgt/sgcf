# TODO — Cotações de FGI

Espelho operacional de `tasks/cotacoes-modalidades/fgi/plan.md`. Marque conforme avançar.

## Fase 1 — Domínio

- [ ] **Task 1.1** Estender `Proposta` com campos FGI planos (`NumeroOperacaoFgi`, `TaxaFgiAaDecimal`, `PercentualCobertoDecimal`)
    - [ ] Campos com setters privados em `Proposta.cs`
    - [ ] Construtor `internal` aceita os 3 parâmetros opcionais
    - [ ] `Cotacao.AdicionarProposta` propaga os campos
    - [ ] Invariantes condicionais (`Modalidade == Fgi` exige FGI; demais rejeitam FGI)
    - [ ] Cache CET invalidado em mutações FGI
    - [ ] Conversão pct→fração espelha `FgiDetail.Criar`
    - [ ] Testes `PropostaFgiTests.cs` verdes
    - [ ] Testes existentes continuam verdes

- [ ] **Task 1.2** Estender `CalculadoraCet` com tarifa FGI no fluxo
    - [ ] Bloco em `MontarFluxoBrl` adiciona evento FGI em `t = prazoDias`
    - [ ] Fórmula idêntica a `GerarCronogramaCommand.AdicionarTarifaFgiAsync`
    - [ ] Função permanece pura (sem I/O, sem `IClock`)
    - [ ] Testes `CalculadoraCetFgiTests.cs` verdes
    - [ ] Property-based (FsCheck) confirma monotonicidade e linearidade
    - [ ] Regressão FINIMP sem FGI mantém CET atual

- [ ] **Checkpoint A — Domínio**
    - [ ] `dotnet build` limpo
    - [ ] `dotnet test tests/Sgcf.Domain.Tests` verde
    - [ ] Revisão humana de AD-4 e AD-5 (fórmula do CET)

## Fase 2 — Persistência

- [ ] **Task 2.1** Migration `S6_PropostaFgi`
    - [ ] `dotnet ef migrations add S6_PropostaFgi` gera migration limpa
    - [ ] 3 colunas nullable em `proposta`
    - [ ] Validação cross-modalidade fica em aplicação (AD-10), não em CHECK
    - [ ] `dotnet ef database update` aplica em banco com dados
    - [ ] `dotnet ef migrations remove` reverte sem erro

- [ ] **Task 2.2** Atualizar `PropostaConfiguration`
    - [ ] Mapeamento das 3 novas colunas
    - [ ] Precisão dos `numeric` consistente com `FgiDetail`
    - [ ] Teste de integração persiste e recarrega proposta FGI

- [ ] **Checkpoint B — Persistência**
    - [ ] Migration aplica e reverte em banco com dados
    - [ ] Round-trip de persistência funciona

## Fase 3 — Application + API

- [ ] **Task 3.1** `CriarCotacaoCommand` dispensa PTAX D-1 para FGI
    - [ ] Ramo `if (modalidade == Fgi)` antes de buscar PTAX
    - [ ] Helper compartilhado com NCE/Capital de Giro (extrair ou reutilizar)
    - [ ] Teste `CriarCotacaoFgiTests.cs` cria cotação sem PTAX cadastrada
    - [ ] Cotação FINIMP continua exigindo PTAX (regressão)

- [ ] **Task 3.2** `RegistrarPropostaCommand` aceita campos FGI
    - [ ] Record ganha `NumeroOperacaoFgi`, `TaxaFgiAaPct`, `PercentualCobertoPct` opcionais
    - [ ] Validador condicional (`Modalidade == Fgi` exige; demais rejeitam)
    - [ ] Handler converte pct→fração
    - [ ] Payload antigo FINIMP continua válido (backwards-compatible)
    - [ ] Testes unitários e E2E verdes

- [ ] **Task 3.3** `ConverterEmContratoCommand` cria `FgiDetail`
    - [ ] Ramo `Modalidade == Fgi` chama `FgiDetail.Criar` com campos da proposta aceita
    - [ ] `ContratoDto.From` recebe o `FgiDetail`
    - [ ] Campos FINIMP (`RofNumero`, etc.) ficam null para FGI
    - [ ] `LimiteBanco` FGI tem `ValorUtilizadoBRL` atualizado
    - [ ] Cronograma gerado contém evento `TarifaFgi`

- [ ] **Task 3.4** API e DTOs
    - [ ] `PropostaDto` expõe campos FGI
    - [ ] `GET /api/v1/cotacoes/{id}` retorna campos FGI quando aplicável
    - [ ] Swagger atualizado
    - [ ] E2E `CotacaoFgiE2ETests.cs` cobre fluxo completo

- [ ] **Checkpoint C — CRUD ponta a ponta**
    - [ ] Fluxo completo FGI via API funciona
    - [ ] Suite E2E verde
    - [ ] Revisão humana do CET FGI

## Fase 4 — Golden Dataset

- [ ] **Task 4.1** Cenário FGI bullet 12m
    - [ ] `cotacao-fgi-bullet-12m.json` em `tests/Sgcf.GoldenDataset/data/cotacoes/`
    - [ ] CET esperado com 6 casas decimais
    - [ ] Comentário explicando a derivação do fluxo
    - [ ] Sign-off do time financeiro (CLAUDE.md exige)
    - [ ] `dotnet test tests/Sgcf.GoldenDataset` verde

## Fase 5 — Documentação

- [ ] **Task 5.1** Atualizar `docs/specs/cotacoes/SPEC.md`
    - [ ] §11.2 remove FGI de out-of-scope
    - [ ] §3.3 ganha regra FGI
    - [ ] §5.1 ganha sub-seção 5.1.1 (CET FGI)
    - [ ] Glossário §2 distingue FGI-modalidade × FGI-garantia

- [ ] **Task 5.2** Atualizar `docs/api/cotacoes.md`
    - [ ] Schema Proposta ganha 3 campos
    - [ ] Exemplo POST cotação FGI
    - [ ] Tabela de regras condicionais

- [ ] **Task 5.3** Bruno collection
    - [ ] `POST cotacao FGI`
    - [ ] `POST proposta FGI`
    - [ ] `POST converter em contrato FGI`

- [ ] **Task 5.4** CHANGELOG
    - [ ] Seção `[0.x.0] — 2026-MM-DD`
    - [ ] Bloco `ADDITIVE — Cotações — Suporte a modalidade FGI`

## Checkpoint Final

- [ ] `dotnet test` (suite completa) verde
- [ ] Build limpo, sem warnings novos
- [ ] Golden Dataset assinado pelo time financeiro
- [ ] Documentação revisada
- [ ] Bruno collection valida fluxo manual completo
- [ ] PR pronto para review

## Perguntas em Aberto (decidir antes do `/build`)

- [ ] Q1 — FGI em estruturas Price/SAC: restringir MVP a Bullet?
- [ ] Q2 — `PercentualCoberto` máximo regulatório?
- [ ] Q3 — Capturar `TipoFgi` (subprograma) na proposta?
- [ ] Q4 — Imutabilidade da `TaxaFgiAa` após aceitação?
- [ ] Q5 — Capturar `BancoIntermediario` na proposta?
- [ ] Q6 — Quem extrai o helper "BRL dispensa PTAX" — FGI ou Capital de Giro?
- [ ] Q7 — Numeração da versão (consolidação com task #16)
