# Plano Mestre — Cotações além de FINIMP (REFINIMP, Lei 4131, NCE, Capital de Giro, FGI)

**Status:** Pendente de aprovação humana
**Autor:** Planning agent (consolidação de 5 planos paralelos)
**Data:** 2026-05-18
**Dependências externas:** Nenhuma além das modificações de fundação descritas em §3 (Onda 0).

---

## 1. Contexto

O MVP do módulo de Cotações (v0.5.0–v0.6.0) cobre apenas FINIMP. Cinco modalidades aguardam habilitação: REFINIMP, Lei 4131/62, NCE, Capital de Giro e FGI. As entidades `*Detail` já existem em `Sgcf.Domain.Contratos`; o `CreateContratoCommand` (módulo Contratos) já lida com todas as 6 modalidades. O gap está exclusivamente no **bridge Cotações → Contratos** (`ConverterEmContratoCommand` ramifica apenas em `Finimp`) e em **suposições embutidas** (PTAX D-1 obrigatória; CET assumindo moeda estrangeira; conversão hard-coded em FINIMP).

Cada modalidade tem um plano dedicado nesta pasta:

| Modalidade | Plano | Checkpoints | Tasks | Escopo dominante |
|---|---|---|---|---|
| REFINIMP | `refinimp/plan.md` | 6 (A–E + Final) | 13 | Domínio + Application |
| Lei 4131 | `lei4131/plan.md` | 6 (A–E + Final) | 14 | Tributação + Application |
| NCE | `nce/plan.md` | 5 (A–D + Final) | 14 | CET adapter + Application |
| Capital de Giro | `capital-de-giro/plan.md` | 5 (A–D + Final) | 13 | Cronograma externo |
| FGI | `fgi/plan.md` | 4 (A–C + Final) | 13 | CET + tarifa anual |
| **Total** | — | **26** | **67** | — |

---

## 2. Decisões Arquiteturais Cross-Modalidade

| # | Decisão | Rationale |
|---|---------|-----------|
| MD-1 | **Foundation primeiro, modalidades depois.** Criar uma "Onda 0" que entrega: (a) `PtaxUsadaUsdBrl` opcional em `Cotacao`; (b) `CalculadoraCet` com branch BRL; (c) `ConverterEmContratoCommand` como dispatcher por modalidade. | Três das cinco modalidades (NCE, Capital de Giro, FGI) dependem dessas mudanças. Fazer cada modalidade modificar essas três peças individualmente causa retrabalho, conflitos de merge, e risco de regressão FINIMP. |
| MD-2 | **Não renumerar `ModalidadeContrato` enum.** Valores existentes (Finimp=1..Fgi=6) são imutáveis para compatibilidade com migrations e dados em produção. | Já sinalizado no SPEC §11. |
| MD-3 | **`ConverterEmContratoCommand` vira dispatcher (strategy por modalidade).** Cada modalidade implementa `IModalidadeConverter` (ou método estático nomeado) que cria seu `*Detail`. O command continua sendo único; quem ramifica é uma interface. | Evita `switch (modalidade)` que cresce; isola lógica de cada modalidade; facilita testes unitários por modalidade. |
| MD-4 | **CET por modalidade via método especializado em `CalculadoraCet`.** Não usar um único método "genérico" — cada modalidade tem fórmula com inputs e outputs diferentes (IRRF, IOF, NDF, tarifa FGI, IOF crédito). | Forçar generalização prematura aumenta complexidade e mascara bugs sutis. Cinco métodos nomeados são mais simples que um método com 15 parâmetros opcionais. |
| MD-5 | **`Proposta` não recebe campos novos por modalidade.** Campos extras de modalidade (RofNumero, SblcNumero, NceNumero, NumeroOperacao, TaxaFgiAa, etc.) ficam no `ConverterEmContratoCommand` (inputs do command). | A `Proposta` deve permanecer pequena e estável. Capturar dados específicos só no momento de virar contrato evita explosão de campos opcionais. |
| MD-6 | **`LimiteBanco` é por modalidade (já estabelecido).** Cada modalidade exige cadastro separado de limite por banco. Sem mudança no agregado. | Mantém o invariante atual. |
| MD-7 | **`EconomiaNegociacao` snapshot imutável (SPEC §12.3).** Os snapshots JSON capturam o estado no momento da conversão. Mudanças no schema da `Proposta` ou nas modalidades não retro-aplicam aos snapshots. | Já estabelecido. Mas exige inspeção dos campos novos para garantir serialização correta. |
| MD-8 | **FGI tem dupla natureza preservada.** FGI-**garantia** já implementado em v0.6.0 (`GarantiaExigidaLimite.Tipo=Fgi`). FGI-**modalidade** é tratado pelo plano `fgi/`. `CapitalDeGiroDetail.TemFgi` (FGI dentro de Capital de Giro) é tratado pelo plano `capital-de-giro/`. | Os três casos são distintos e o glossário precisa ser explícito na SPEC. |
| MD-9 | **`PtaxUsadaUsdBrl` opcional via `decimal?`, não sentinel.** Modalidades BRL passam `null`; FINIMP, REFINIMP e Lei 4131 continuam obrigando. Validação migra para `CriarCotacaoCommandValidator` condicionada à modalidade. | Sentinel (PTAX=1) gera bugs sutis em comparações e em snapshots. Nullable é semanticamente correto. |
| MD-10 | **Golden dataset cresce uma entrada por modalidade.** Cada plano de modalidade adiciona pelo menos 1 cenário ao `tests/Sgcf.GoldenDataset/data/cotacoes/`. | Regressão futura de qualquer modalidade fica protegida. |

---

## 3. Onda 0 — Foundation (pré-requisito de todas as modalidades exceto REFINIMP)

Mudanças base que devem ser executadas e mergeadas **antes** das modalidades dependentes. REFINIMP é o caso de menor risco que não exige nenhuma mudança aqui — pode ser executado em paralelo com a Onda 0.

### F0.1 — Tornar `PtaxUsadaUsdBrl` opcional em `Cotacao`

**Descrição:** Permitir que o agregado `Cotacao` carregue `PtaxUsadaUsdBrl: decimal?` em vez de `decimal`. Atualizar `CriarCotacaoCommand` para deixar de exigir PTAX D-1 quando modalidade é BRL pura (NCE, Capital de Giro, FGI). Atualizar `ConverterEmContratoCommand` para tratar `valorPrincipalBrl` quando `PtaxUsadaUsdBrl` é null (usar valor direto se moeda original já é BRL).

**Critérios de aceite:**
- `Cotacao.PtaxUsadaUsdBrl` tipo `decimal?`
- `CriarCotacaoCommand`: aceita modalidades BRL sem PTAX; exige PTAX para Finimp/Lei4131/Refinimp
- `EconomiaNegociacao` snapshot: PTAX null serializa como `null` JSON (não string vazia)
- Migration S6 adita: coluna `ptax_usada_usd_brl` vira nullable
- Suite Domain + Application + IntegrationTests (FINIMP) verde sem regressão
- Golden dataset existente (3 cenários FINIMP) passa sem ajuste

**Verificação:** `dotnet test sgcf-backend.sln --filter "Category!=Slow"` + `dotnet ef database update`

**Escopo:** M

### F0.2 — `CalculadoraCet` ganha método dedicado por modalidade

**Descrição:** Refatorar `CalculadoraCet` para expor 5 métodos públicos: `CalcularCetFinimp`, `CalcularCetRefinimp` (delega para Finimp), `CalcularCetLei4131`, `CalcularCetNce`, `CalcularCetCapitalDeGiro`, `CalcularCetFgi`. Método legado `CalcularCet` vira fachada que dispatcheia por modalidade. Cada método tem assinatura específica (inputs distintos).

**Critérios de aceite:**
- Cada método é pure function (sem I/O, sem clock)
- Cobertura unitária por método (mín. 2 cenários — typical + edge)
- Golden dataset FINIMP existente passa sem ajuste
- Documentar fórmula no XML doc de cada método (CET = f(taxa, prazo, IOF, ...))

**Verificação:** `dotnet test tests/Sgcf.Domain.Tests/Sgcf.Domain.Tests.csproj --filter "FullyQualifiedName~CalculadoraCet"`

**Dependências:** F0.1 (PTAX opcional impacta o método NCE/BalcãoCaixa/FGI)

**Escopo:** M

### F0.3 — `ConverterEmContratoCommand` como dispatcher

**Descrição:** Substituir o `if (cotacao.Modalidade == ModalidadeContrato.Finimp)` (linha 100) por um dispatcher que invoca o conversor da modalidade. Criar interface `IConversorModalidade` (ou método estático em classe parcial) com método `CriarDetail(cotacao, propostaAceita, contrato, cmd, clock) → entity`. Implementar `ConversorFinimp` que extrai a lógica existente.

**Critérios de aceite:**
- Comportamento atual de FINIMP preservado (testes regressivos verdes)
- Conversores das outras modalidades como `NotImplementedException` (cada plano de modalidade implementa o seu)
- `ConverterEmContratoCommand` reduzido em ~30 linhas
- DI: registrar `IConversorModalidade` por modalidade no startup

**Verificação:** `dotnet test tests/Sgcf.Api.IntegrationTests/Sgcf.Api.IntegrationTests.csproj` (golden flow FINIMP completo)

**Dependências:** F0.1

**Escopo:** M

### Checkpoint F0

- [ ] F0.1, F0.2, F0.3 mergeados
- [ ] Suite completa verde (Domain + Application + IntegrationTests + GoldenDataset)
- [ ] Migration S6_PtaxNullable aplicada sem afetar dados
- [ ] Revisão humana das decisões cross-modalidade (MD-1..MD-10)

---

## 4. Sequenciamento Recomendado (Ondas)

```
┌─ Onda 0 (Foundation) ─────────────────────────────────────┐
│  F0.1 PtaxNullable     ─┬─→  F0.2 CalculadoraCet branches │
│                          └─→  F0.3 Converter dispatcher    │
└────────────────────────────────────────────────────────────┘
                  │
                  ├─────────────────────────────────┐
                  ▼                                  ▼
┌─ Onda 1 (paralelo com Onda 0) ┐    ┌─ Onda 2 (depois de F0) ─┐
│  REFINIMP                      │    │  NCE                     │
│  (não depende de F0)           │    └──────────────────────────┘
└────────────────────────────────┘                  │
                                                     ▼
                                       ┌─ Onda 3 (paralelo) ────┐
                                       │  FGI       Capital de Giro │
                                       │  ───       ────────────  │
                                       │  (independentes entre si)│
                                       └──────────────────────────┘
                                                     │
                                                     ▼
                                       ┌─ Onda 4 (depois de Onda 3 ou paralelo se equipe) ┐
                                       │  Lei 4131                                          │
                                       │  (mais regulatório; pode esperar)                 │
                                       └────────────────────────────────────────────────────┘
```

### Justificativa do sequenciamento

| Onda | Modalidades | Por quê | Pode paralelizar? |
|---|---|---|---|
| 0 | Foundation | Cinco planos individuais convergem para 3 mudanças base. Centralizar evita conflitos e regressão. | Internamente: F0.1 → (F0.2 + F0.3 em paralelo). |
| 1 | REFINIMP | Não depende de F0 (mesma moeda do mãe, mesmo CET, NDF aplicável). Menor risco e maior reuso. | Paralelo com Onda 0. |
| 2 | NCE | Modalidade BRL mais simples — apenas 1 detail entity, sem garantia FGI nem cronograma externo. Bom shake-down do branch BRL. | Sequencial após Onda 0. |
| 3 | FGI + Capital de Giro | Ambos BRL, ambos com complexidades distintas (tarifa anual vs cronograma externo) que não colidem. | Sim — equipes separadas viáveis. |
| 4 | Lei 4131 | Maior complexidade regulatória (IRRF, SBLC, RDE-ROF, acordos bitributação). Vale esperar lições das Ondas 1–3. | Paralelo com Onda 3 se equipe permite. |

---

## 5. Riscos Compartilhados

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Regressão FINIMP causada pelas mudanças da Onda 0 | Média | Alto | Golden dataset existente + IntegrationTests FINIMP servem de regression suite. Onda 0 é critério de bloqueio para Ondas 2–4. |
| Sobreposição FGI-modalidade × FGI-garantia × CapitalDeGiro.TemFgi causa confusão de implementação | Alta | Médio | MD-8 estabelece glossário em SPEC; planos `fgi/` e `capital-de-giro/` explicitam fronteiras. |
| `EconomiaNegociacao` snapshot quebra para modalidades BRL (PTAX null em JSON) | Média | Alto | Teste de round-trip JSON na F0.1; abaixo de release-gate. |
| CalculadoraCet com 5 branches divergentes acumula bug por modalidade | Média | Médio | Golden dataset cresce uma entrada por modalidade (MD-10). Cada branch tem teste unitário direto, não só via integração. |
| Lei 4131 IRRF varia por país do credor (10%/12,5%/15%/25%) | Alta | Médio | MVP: operador informa alíquota; tabela hard-coded fora de escopo. Plano lei4131 elenca como Q1. |
| Capital de Giro CET estimado divergente do CET real após importação do cronograma | Média | Alto (analítico) | `EconomiaNegociacao` imutável (SPEC §12.3); recálculo pós-importação é evolução, não MVP. |
| REFINIMP regra 70% BB descoberta tarde no fluxo (após cotação criada) | Baixa | Médio | Pré-cálculo informativo no comparativo (plan refinimp/ AD-5). |
| Tarifa FGI anual sobre saldo: fórmula duplicada em CalculadoraCet e GerarCronograma | Média | Médio | Plano fgi/ AD-8: espelhar linha a linha + teste comparativo cotação↔contrato. |

---

## 6. Perguntas em Aberto Cross-Modalidade

Algumas decisões precisam ser tomadas antes ou durante a Onda 0 porque afetam múltiplos planos:

1. **Base de cálculo BRL** — NCE/FGI/BalcãoCaixa deveriam usar 252 dias úteis (padrão BACEN brasileiro) ou 360 (mesma de FINIMP)? Plano NCE Q1; impacta CalculadoraCet branch BRL.
2. **IRRF de Lei 4131 entra no CET?** AD-3 do plano lei4131/ propõe que não; pode subestimar o custo real ao comparar com bancos que praticam gross-up. Definir antes da Fase 3 desse plano.
3. **Pré-cálculo informativo da regra 70% BB** — calcular no comparativo (plano refinimp/) requer leitura do contrato mãe na proposta. OK como query dedicada?
4. **Tarifa FGI no comparativo** — exibir CET com e sem tarifa FGI ou apenas o composto? Plano fgi/ propõe composto + breakdown opcional.
5. **Cronograma estimado de Capital de Giro na Cotação** — capturar simulação Caixa ou apenas premissas (taxa, prazo, IOF)? Plano capital-de-giro/ propõe premissas; cronograma real chega na importação.
6. **Versionamento da SPEC** — extender `docs/specs/cotacoes/SPEC.md` (cresce muito) ou criar `SPEC-v2.md` por onda? Recomendação: estender e marcar seções com `[v0.7.0+]`, `[v0.8.0+]`, etc.

---

## 7. Sumário Quantitativo Consolidado

| Métrica | Valor |
|---|---|
| Planos individuais | 5 (REFINIMP, Lei 4131, NCE, Capital de Giro, FGI) |
| Onda 0 (foundation) | 3 tasks + 1 checkpoint |
| Tasks por modalidade | 13–14 |
| **Tasks totais (Onda 0 + 5 modalidades)** | **~70** |
| **Checkpoints totais** | **~30** (1 Onda 0 + 26 modalidades + 3 globais sugeridos) |
| Migrations esperadas | S6 (PtaxNullable) + 0–2 por modalidade |
| Releases sugeridos | v0.7.0 (REFINIMP), v0.8.0 (NCE), v0.9.0 (FGI+CapitalDeGiro), v1.0.0 (Lei4131) |
| Golden dataset entries novas | mín. 5 (1 por modalidade) |
| Riscos críticos compartilhados | 4 (regressão FINIMP, snapshot JSON, dualidade FGI, CalculadoraCet branches) |

---

## 8. Checkpoints Globais

Além dos checkpoints internos de cada plano, três checkpoints transversais:

- **Checkpoint G1 — Pós-Onda 0:** Foundation entregue. Bloqueia Ondas 2–4. Revisão humana das ADs MD-1..MD-10.
- **Checkpoint G2 — Pós-Onda 2 (NCE):** Primeira modalidade BRL completa. Validação de que o branch BRL não regrediu FINIMP. Bloqueia Onda 3.
- **Checkpoint G3 — Final (todas modalidades verdes):** Suite completa + 5 golden cases novos + docs SPEC, CHANGELOG, Bruno coleções. Release v1.0.0.

---

## 9. Próximos Passos

1. **Revisão humana** das ADs MD-1..MD-10 e das 6 perguntas em aberto (§6).
2. **Aprovação** da sequência por ondas (§4) ou ajuste conforme prioridade operacional.
3. **Decisão sobre versionamento da SPEC** (Q6).
4. **Início da Onda 0** assim que ADs forem confirmadas.
