# Plano de Implementação — Cotações de Lei 4131/62

**Status:** Pendente de aprovação humana
**Autor:** Planning agent (read-only mode)
**Data:** 2026-05-18
**Dependências externas:** Conclusão e merge do MVP de Cotações (FINIMP) — `docs/specs/cotacoes/SPEC.md` v1.0 já implementado até `ConverterEmContratoCommand`. Disponibilidade da entidade `Lei4131Detail` (já existente em `src/Sgcf.Domain/Contratos/Lei4131Detail.cs`).

---

## 1. Contexto

O módulo de Cotações entrou em produção cobrindo apenas a modalidade FINIMP (SPEC §1, §11.2). A modalidade **Lei 4131/62** é estruturalmente próxima de FINIMP (empréstimo em moeda estrangeira, NDF opcional, garantia bancária via SBLC), mas tem três diferenças que afetam o domínio: (a) não exige ROF de importação — exige RDE-ROF de empréstimo externo registrado no BACEN; (b) o credor é direto (banco ou fundo no exterior, não a tradeline do banco brasileiro), o que altera o regime tributário (IRRF sobre juros remetidos, com alíquota dependente de acordo de bitributação); (c) garantia bancária típica é a SBLC, já modelada como tipo do enum `TipoGarantia` e como campos planos em `Lei4131Detail`. O objetivo deste plano é estender o módulo de Cotações para suportar Lei 4131 reutilizando o máximo da estrutura atual, mantendo a `Proposta` plana (SPEC §3.3) e adicionando dados específicos de Lei 4131 apenas onde necessários — preferencialmente na **conversão em contrato**, não na captação da proposta.

---

## 2. Decisões Arquiteturais

| #     | Decisão                                                                                                                                                                                                          | Rationale                                                                                                                                                                                                                                            |
| ----- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AD-1  | **Não adicionar campos específicos de Lei 4131 ao agregado `Cotacao` nem à `Proposta`.** Os dados (`SblcNumero`, `SblcBancoEmissor`, `SblcValorUsd`, `TemMarketFlex`, `BreakFundingFeePercentual`) são capturados no momento da conversão em contrato, alimentando `Lei4131Detail`. | A `Proposta` é plana por design (SPEC §3.3) e cobre FINIMP, Lei 4131 e modalidades análogas. SBLC e break funding fee são detalhes de fechamento contratual; durante a cotação o operador compara taxa/CET/garantia, e a SBLC só existe formalmente após o aceite. |
| AD-2  | **Reutilizar o pré-preenchimento de garantia já implementado em `LimiteBanco.GarantiasExigidas`.** Quando o `LimiteBanco` de Lei 4131 tem `GarantiaExigidaLimite` do `Tipo = Sblc`, o `AdicionarBancoNaCotacaoCommand` popula `Proposta.GarantiaExigida` (string), `ValorGarantiaExigidaBrl` e mantém `GarantiaEhCdbCativo = false`. | Já há infraestrutura pronta (`tasks/garantias-em-limites/`); reusar evita duplicação. SBLC entra como string descritiva + valor estimado em BRL.                                                                                                                  |
| AD-3  | **CET de Lei 4131 reusa `CalculadoraCet` sem alteração estrutural no MVP.** O IRRF não entra no CET MVP — fica como dado informativo opcional na resposta da `CompararPropostasQuery` (campo `IrrfEstimadoBrl`) calculado em camada de Application. | Adicionar IRRF ao CET muda a métrica regulada e impacta os Goldens de FINIMP. IRRF é um custo do credor (retido na fonte sobre juros remetidos), e em diversos contratos é "gross-up" para o tomador — ou seja, recae sobre o tomador. Tratar gross-up no CET exige decisão de PO; **deferir para Onda 2**, mas exibir informativo para o operador desde já. |
| AD-4  | **SBLC é capturada no comando `ConverterEmContratoCommand`** (input opcional do tipo `Lei4131ConversaoDetail`). A Cotação não conhece o número da SBLC.                                                          | Mantém Cotação focada em comparação; SBLC só é emitida após o aceite, então tentar capturá-la antes seria modelagem premature.                                                                                                                       |
| AD-5  | **Adicionar parâmetro opcional `taxaIrrfAaPercentual` na `Proposta`** (default `null`, sem impacto em invariantes) — usado **apenas** pelo cálculo informativo de IRRF estimado.                                  | Permite o operador registrar a alíquota IRRF que o banco aplicará (varia por país via acordo de bitributação — comum 15%, reduzida para 12,5% ou 10% em alguns acordos). Aditivo, default null = comportamento atual preservado.                                |
| AD-6  | **Validador de modalidade no `CriarCotacaoCommand`**: aceitar `Lei4131` além de `Finimp` (hoje aceita ambos via enum, mas testar). Sem mudanças no contrato existente.                                            | O enum `ModalidadeContrato.Lei4131` já existe; validar apenas que o caminho de criação aceita.                                                                                                                                                       |
| AD-7  | **`ConverterEmContratoCommand` ganha branch `if (cotacao.Modalidade == ModalidadeContrato.Lei4131)`** que cria `Lei4131Detail` via factory existente.                                                             | Espelha o branch FINIMP já presente na linha 100 do command. Mudança mínima e cirúrgica.                                                                                                                                                              |
| AD-8  | **Não criar limite em moeda estrangeira no MVP.** `LimiteBanco` permanece em BRL para Lei 4131 também (mesmo padrão FINIMP).                                                                                      | Consistência com a estrutura atual. Conversão de exposição via PTAX D-1.                                                                                                                                                                              |
| AD-9  | **NDF é opcional em Lei 4131** (mesma semântica de `Proposta.ExigeNdf`). Algumas operações usam hedge separado, outras carregam exposição cambial — o `ExigeNdf` flag da Proposta captura ambos os casos.        | Já modelado; sem mudança.                                                                                                                                                                                                                             |
| AD-10 | **Sem CHANGELOG MAJOR.** Mudança é puramente aditiva no domínio de Cotações; release como **v0.8.0 ADDITIVE**.                                                                                                    | Compat retroativa: payloads FINIMP existentes continuam funcionando inalterados.                                                                                                                                                                      |

---

## 3. Grafo de Dependências

```
src/Sgcf.Domain/Contratos/Lei4131Detail.cs (já existe)
src/Sgcf.Domain/Contratos/ModalidadeContrato.cs (Lei4131 já no enum)
src/Sgcf.Domain/Cotacoes/Proposta.cs (já existe — plana)
    │
    ├─► Task 1.1: Proposta ganha TaxaIrrfAaPercentual (aditivo, nullable)
    │       │
    │       └─► Task 2.1: Migration S6_PropostaIrrf (ADD COLUMN nullable)
    │               │
    │               └─► Task 2.2: PropostaConfiguration mapeia coluna
    │
    ├─► Task 3.1: RegistrarPropostaCommand / AtualizarPropostaCommand aceitam taxaIrrfAaPercentual
    │       │
    │       └─► Task 3.2: PropostaDto + CompararPropostasQuery expõem IrrfEstimadoBrl (calculado)
    │
    ├─► Task 3.3: AdicionarBancoNaCotacaoCommand — Tipo=Sblc no LimiteBanco produz string descritiva correta
    │       (reusa Task 4.1 de garantias-em-limites)
    │
    ├─► Task 4.1: ConverterEmContratoCommand recebe Lei4131ConversaoDetail (opcional)
    │       │
    │       └─► Task 4.2: branch Lei4131 cria Lei4131Detail e chama AddLei4131Detail
    │
    ├─► Task 5.1: CalculadoraCet — golden case Lei 4131 (sem alteração de código)
    ├─► Task 5.2: CalculadoraIrrfEstimado (helper puro Application)
    │
    └─► Task 6.x: Bruno + docs/api/cotacoes.md + SPEC.md (apêndice Lei 4131) + CHANGELOG v0.8.0
```

---

## 4. Fases e Tarefas

### Fase 1 — Domínio (Aditivo em `Proposta`)

#### Task 1.1 — Adicionar `TaxaIrrfAaPercentual` (opcional) em `Proposta`

**Descrição:** Adicionar propriedade nullable `TaxaIrrfAaPercentual` (decimal, em fração 0..1) à entidade `Proposta`. Valor opcional, sem invariantes obrigatórios. Usado apenas para cálculo informativo de IRRF estimado em remessas (não entra no CET MVP — AD-3).

**Critérios de aceite:**
- [ ] Propriedade `TaxaIrrfAaPercentual` (nullable, fração 0..1) e backing field `TaxaIrrfAaPercentualDecimal` adicionados.
- [ ] Construtor `internal Proposta(...)` ganha parâmetro `decimal? taxaIrrfAaPercentual = null` ao final (preserva compat).
- [ ] `ValidarInvariantes` verifica: se `taxaIrrfAaPercentual.HasValue`, então valor deve estar em `[0, 1]`. Não obrigatório.
- [ ] Cache de CET **não** é invalidado por mudança nesse campo (não impacta CET no MVP — ver AD-3).

**Verificação:**
- [ ] Teste `PropostaTests.cs`: criar proposta sem IRRF (passa); com IRRF 0.15 (passa); com IRRF -0.1 (rejeita); com IRRF 1.5 (rejeita).
- [ ] Regressão: testes existentes de `Proposta` continuam verdes (campo default null).

**Dependências:** nenhuma

**Arquivos prováveis:**
- `src/Sgcf.Domain/Cotacoes/Proposta.cs`
- `tests/Sgcf.Domain.Tests/Cotacoes/PropostaTests.cs`

**Escopo:** S

---

#### Checkpoint A — Domínio

- [ ] `dotnet build` limpo.
- [ ] `dotnet test tests/Sgcf.Domain.Tests` verde.
- [ ] Revisão humana: confirmar AD-3 (IRRF fora do CET) e AD-5 (IRRF na Proposta).

---

### Fase 2 — Persistência

#### Task 2.1 — Migration `S6_PropostaIrrf`

**Descrição:** Adicionar coluna `taxa_irrf_aa_percentual` (numeric nullable) à tabela `proposta`. Aditiva, sem default — registros existentes ficam null.

**Critérios de aceite:**
- [ ] `dotnet ef migrations add S6_PropostaIrrf --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api` gera migration limpa.
- [ ] Up: `AddColumn` nullable; Down: `DropColumn`.
- [ ] `dotnet ef database update` aplica sem afetar dados.

**Verificação:**
- [ ] `dotnet ef migrations remove` reverte limpo (em banco recém-aplicado).
- [ ] Snapshot do modelo atualizado consistente.

**Dependências:** Task 1.1

**Arquivos prováveis:**
- `src/Sgcf.Infrastructure/Migrations/2026MMDD_S6_PropostaIrrf.cs`
- `src/Sgcf.Infrastructure/Migrations/SgcfDbContextModelSnapshot.cs`

**Escopo:** XS

---

#### Task 2.2 — `PropostaConfiguration` mapeia coluna

**Descrição:** Atualizar EF Configuration de `Proposta` para mapear `TaxaIrrfAaPercentualDecimal → taxa_irrf_aa_percentual` (precision 18,8 — consistente com outros campos percentuais).

**Critérios de aceite:**
- [ ] `Property(p => p.TaxaIrrfAaPercentualDecimal).HasColumnName("taxa_irrf_aa_percentual").HasPrecision(18, 8).IsRequired(false)`.
- [ ] Round-trip (write → read) preserva valor incluindo null.

**Verificação:**
- [ ] Teste de integração existente de `Proposta` regenerado para incluir IRRF em 1 cenário.

**Dependências:** Task 2.1

**Arquivos prováveis:**
- `src/Sgcf.Infrastructure/Persistence/Configurations/PropostaConfiguration.cs`

**Escopo:** XS

---

#### Checkpoint B — Persistência

- [ ] Migration aplica e reverte sem erro.
- [ ] Round-trip de IRRF persistente funciona.

---

### Fase 3 — Application (Captura e Pré-preenchimento)

#### Task 3.1 — `RegistrarPropostaCommand` e `AtualizarPropostaCommand` aceitam `TaxaIrrfAaPercentual`

**Descrição:** Adicionar parâmetro opcional `decimal? TaxaIrrfAaPercentual` (em percentual humano, ex. 15 para 15%) aos dois commands. O handler converte para fração (`/100`) antes de passar ao domínio.

**Critérios de aceite:**
- [ ] Records dos commands ganham campo nullable ao final (preserva compat).
- [ ] Validators aceitam `[0, 100]` quando `HasValue`.
- [ ] Handlers passam para o construtor/mutator da `Proposta`.
- [ ] PropostaDto.From inclui o campo.

**Verificação:**
- [ ] Teste E2E em `tests/Sgcf.Api.IntegrationTests/Cotacoes/`: registrar proposta de Lei 4131 com `taxaIrrfAaPercentual: 15` → 201; payload retorna `taxaIrrfAaPercentual: 0.15`.
- [ ] Regressão FINIMP: registrar proposta sem o campo → continua verde.

**Dependências:** Tasks 1.1, 2.2

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/RegistrarPropostaCommand.cs`
- `src/Sgcf.Application/Cotacoes/Commands/AtualizarPropostaCommand.cs`
- `src/Sgcf.Application/Cotacoes/PropostaDto.cs`

**Escopo:** S

---

#### Task 3.2 — `CalculadoraIrrfEstimado` (helper puro) + exposição em `CompararPropostasQuery`

**Descrição:** Criar helper estático puro em `Sgcf.Application/Cotacoes/` que calcula o IRRF estimado em BRL sobre os juros projetados de uma proposta de Lei 4131. Resultado exposto em `ComparativoDto` como campo informativo `IrrfEstimadoBrl` (default 0 quando `TaxaIrrfAaPercentual == null` ou modalidade != Lei 4131).

**Fórmula (a confirmar com PO antes de implementar):**

```
JurosProjetadosBrl = ValorOferecidoBrl × (TaxaAaPercentual + SpreadAaPercentual) × (PrazoDias / 360)
IrrfEstimadoBrl    = JurosProjetadosBrl × TaxaIrrfAaPercentual
```

**Critérios de aceite:**
- [ ] Helper sem I/O, sem estado, sem `IClock`. Função pura.
- [ ] `Math.Round(..., 2, MidpointRounding.AwayFromZero)` no resultado final em BRL.
- [ ] Quando `TaxaIrrfAaPercentual == null` → retorna 0.
- [ ] `CompararPropostasQuery` retorna `IrrfEstimadoBrl` por proposta no DTO.

**Verificação:**
- [ ] Teste unitário: USD 1M, taxa 5% a.a., prazo 180 dias, IRRF 15%, PTAX 5,00 → JurosBrl = 125.000, IRRF = 18.750.
- [ ] Teste de regressão: proposta FINIMP sem IRRF → `IrrfEstimadoBrl == 0`.

**Dependências:** Task 3.1

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/CalculadoraIrrfEstimado.cs` (NEW)
- `src/Sgcf.Application/Cotacoes/ComparativoDto.cs`
- `src/Sgcf.Application/Cotacoes/Queries/CompararPropostasQuery.cs`

**Escopo:** M

---

#### Task 3.3 — Validar pré-preenchimento de garantia SBLC em `AdicionarBancoNaCotacaoCommand`

**Descrição:** Sem mudança de código — apenas verificação. Quando o `LimiteBanco` de Lei 4131 tem `GarantiaExigidaLimite` do `Tipo = Sblc` (já suportado pelo enum `TipoGarantia`), o handler atual de `AdicionarBancoNaCotacaoCommand` deve formatar string descritiva correta (ex.: `"SBLC 100% (obrigatório)"`) via `FormatadorGarantiaExigida`.

**Critérios de aceite:**
- [ ] Teste E2E exercita: criar `LimiteBanco` modalidade `Lei4131` com `GarantiaExigida` `Sblc 100%` → adicionar banco em cotação Lei 4131 → `Proposta.GarantiaExigida` reflete a string formatada; `GarantiaEhCdbCativo = false`.
- [ ] Se o `FormatadorGarantiaExigida` ainda não suporta `Sblc` adequadamente, abrir tarefa de patch (estimar XS).

**Verificação:**
- [ ] Teste E2E novo em `tests/Sgcf.Api.IntegrationTests/Cotacoes/` cobrindo o fluxo completo.

**Dependências:** Conclusão de `tasks/garantias-em-limites/` (já em andamento)

**Escopo:** S

---

#### Checkpoint C — Application

- [ ] CompararPropostasQuery retorna IRRF estimado quando aplicável.
- [ ] Pré-preenchimento SBLC funciona via reuse.
- [ ] Bruno collection — operador valida manualmente Cotação Lei 4131 ponta a ponta até "Comparada".

---

### Fase 4 — Conversão em Contrato (Lei 4131)

#### Task 4.1 — `ConverterEmContratoCommand` aceita `Lei4131ConversaoDetail`

**Descrição:** Adicionar parâmetro opcional `Lei4131ConversaoDetail? Lei4131Detail` ao command. Quando a cotação tem `Modalidade == Lei4131`, este input é **obrigatório** (validator) e seus campos correspondem ao factory de `Lei4131Detail.Criar`: `SblcNumero?`, `SblcBancoEmissor?`, `SblcValorUsd?`, `TemMarketFlex` (bool, default false), `BreakFundingFeePercentual?`.

**Critérios de aceite:**
- [ ] Novo record `Lei4131ConversaoDetail(string? SblcNumero, string? SblcBancoEmissor, decimal? SblcValorUsd, bool TemMarketFlex, decimal? BreakFundingFeePercentual)`.
- [ ] Command record ganha campo opcional ao final.
- [ ] Validator: quando `cotacao.Modalidade == Lei4131`, `Lei4131Detail` é obrigatório (espelha pattern de `CreateContratoCommand` linhas 116-119).
- [ ] Compat: FINIMP segue funcionando sem mudanças no payload.

**Verificação:**
- [ ] Teste E2E: converter cotação Lei 4131 com payload contendo `lei4131Detail` → 201 e contrato criado com `Lei4131Detail` populado no banco.
- [ ] Teste E2E negativo: converter cotação Lei 4131 sem `lei4131Detail` → 400 com mensagem clara.
- [ ] Teste E2E regressão FINIMP: cotação FINIMP sem mudanças → continua verde.

**Dependências:** Task 1.1 (não bloqueia; apenas garante coerência de campos opcionais como BreakFunding)

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs`

**Escopo:** M

---

#### Task 4.2 — Branch Lei 4131 em `ConverterEmContratoCommandHandler`

**Descrição:** Adicionar bloco `if (cotacao.Modalidade == ModalidadeContrato.Lei4131)` (após o branch FINIMP atual na linha 100) que cria `Lei4131Detail` via `Lei4131Detail.Criar(...)` e chama `contratoRepo.AddLei4131Detail(...)`.

**Critérios de aceite:**
- [ ] Mesmo padrão do branch FINIMP — sem refactor cross-cutting.
- [ ] `Lei4131Detail` retornado é passado para `ContratoDto.From` (atualmente passa apenas `finimpDetail`; precisa estender assinatura — verificar `ContratoDto.From` existente em `CreateContratoCommand` linha 356 que já tem assinatura `From(contrato, finimpDetail, lei4131Detail, ...)`).
- [ ] Snapshot JSON do contrato (`snapshotContrato`) inclui campos de `Lei4131Detail` (`SblcNumero`, `SblcValorUsd`, `TemMarketFlex`).
- [ ] `LimiteBanco` de Lei 4131 é atualizado normalmente (mesmo trecho existente, linhas 178-187).

**Verificação:**
- [ ] Golden case: cotação Lei 4131 USD 5M com SBLC USD 5M, taxa 6% a.a., prazo 360 dias, sem NDF → contrato criado com `Lei4131Detail.SblcNumero` correto, `EconomiaNegociacao` persistida, `LimiteBanco.ValorUtilizadoBrl` incrementado.
- [ ] Trilha de auditoria registra transição `Aceita → Convertida` (sem mudança — herdada).

**Dependências:** Task 4.1

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs` (edita)

**Escopo:** M

---

#### Checkpoint D — Conversão Lei 4131

- [ ] Conversão de cotação Lei 4131 cria `Contrato` + `Lei4131Detail` + `EconomiaNegociacao` atomicamente.
- [ ] Regressão FINIMP intacta.
- [ ] Auditoria registra transição.

---

### Fase 5 — CET e Tributação (Goldens)

#### Task 5.1 — Golden case Lei 4131 USD com SBLC, sem NDF

**Descrição:** Adicionar 1 cenário JSON em `tests/Sgcf.GoldenDataset/data/` validando CET de cotação Lei 4131 típica (sem NDF, com SBLC como garantia descritiva — não afeta CET pois SBLC não é CDB cativo). Sem mudança no `CalculadoraCet`.

**Critérios de aceite:**
- [ ] Cenário: USD 5M, taxa 6% a.a., prazo 360 dias, IOF 0.38%, SBLC 100% (valor descritivo, sem rendimento), PTAX 5,00.
- [ ] `expectedOutput.cetAaPercentual` calculado a mão (e validado via planilha financeira).
- [ ] Teste em `Sgcf.GoldenDataset` carrega JSON e executa `CalculadoraCet.CalcularCet(...)`.

**Verificação:**
- [ ] `dotnet test tests/Sgcf.GoldenDataset/Sgcf.GoldenDataset.csproj --filter Lei4131` verde.

**Dependências:** nenhuma (não requer mudança de código)

**Arquivos prováveis:**
- `tests/Sgcf.GoldenDataset/data/cotacao_lei4131_usd_sblc.json` (NEW)
- `tests/Sgcf.GoldenDataset/Cotacoes/CalculadoraCetGoldenTests.cs` (edita ou parametriza)

**Escopo:** S

---

#### Task 5.2 — Golden case Lei 4131 USD com NDF obrigatório

**Descrição:** Cenário adicional para confirmar que NDF entra no CET corretamente mesmo em Lei 4131 (mesma lógica de FINIMP, código não muda).

**Critérios de aceite:**
- [ ] Cenário: USD 2M, taxa 5%, prazo 180 dias, NDF 2.5% a.a., SBLC 50%.
- [ ] Invariante validado: CET com NDF > CET sem NDF (mesmas outras variáveis) — property-based test reusável.

**Verificação:**
- [ ] Teste verde + assertion adicional de invariante.

**Dependências:** Task 5.1

**Escopo:** XS

---

#### Task 5.3 — Property-based test de IRRF estimado

**Descrição:** FsCheck property test para `CalculadoraIrrfEstimado`:
- IRRF >= 0 sempre.
- IRRF == 0 quando `TaxaIrrfAaPercentual == null`.
- IRRF cresce linearmente com `TaxaIrrfAaPercentual` (mantendo demais inputs).

**Critérios de aceite:**
- [ ] 3 propriedades validadas.
- [ ] Geradores limitados a valores realistas (taxa 0..100%, prazo 1..3650 dias, IRRF 0..50%).

**Verificação:**
- [ ] `dotnet test` verde com FsCheck rodando 100+ casos.

**Dependências:** Task 3.2

**Escopo:** S

---

#### Checkpoint E — Cálculos validados

- [ ] Goldens Lei 4131 passam.
- [ ] Property tests de IRRF passam.
- [ ] Invariantes de CalculadoraCet preservados (sem regressão FINIMP).

---

### Fase 6 — API, Bruno e Documentação

#### Task 6.1 — Bruno collection — Lei 4131

**Descrição:** Adicionar fluxo completo Lei 4131 em `docs/api/collections/sgcf-api/`:
- `POST /api/v1/cotacoes` modalidade `Lei4131`.
- `POST /api/v1/cotacoes/{id}/bancos` — banco com limite Lei 4131 + garantia SBLC.
- `POST /api/v1/cotacoes/{id}/propostas` com `taxaIrrfAaPercentual: 15`.
- `POST /api/v1/cotacoes/{id}/propostas/{pid}/aceitar`.
- `POST /api/v1/cotacoes/{id}/converter-em-contrato` com `lei4131Detail` populado.

**Critérios de aceite:**
- [ ] 1 pasta nova `12-CotacoesLei4131/` (ou dentro de `10-Cotacoes/Lei4131/`).
- [ ] Cada request com payload de exemplo realista.
- [ ] Variáveis de ambiente atualizadas (`{{cotacaoLei4131Id}}`, etc.).

**Verificação:**
- [ ] Operador roda manualmente o fluxo no ambiente local.

**Dependências:** Tasks 3.1, 4.1

**Escopo:** S

---

#### Task 6.2 — `docs/api/cotacoes.md` — apêndice Lei 4131

**Descrição:** Adicionar seção "Lei 4131" descrevendo:
- Campos adicionais no payload de proposta (`taxaIrrfAaPercentual`).
- Payload de conversão (`lei4131Detail`).
- IRRF estimado no comparativo (semântica + cálculo informativo).
- Reuse de garantia SBLC via `LimiteBanco.GarantiasExigidas`.

**Critérios de aceite:**
- [ ] Seção autossuficiente; exemplo curl com payload completo.
- [ ] Tabela de campos Lei 4131-specific.
- [ ] Aviso explícito: "IRRF é informativo no MVP; não entra no CET."

**Escopo:** S

---

#### Task 6.3 — `docs/specs/cotacoes/SPEC.md` — apêndice Lei 4131

**Descrição:** Atualizar SPEC removendo Lei 4131 da §11.2 (out of scope) e adicionando seção (ex.: §19) "Lei 4131" com:
- Diferenças vs FINIMP.
- Tratamento de IRRF (informativo).
- SBLC captura no momento de conversão.
- AD-1..AD-10 (resumido).

**Critérios de aceite:**
- [ ] §11.2 atualizada.
- [ ] Nova seção com referência cruzada a esta `plan.md`.
- [ ] Histórico (§18) atualizado: `v1.1 — Adição da modalidade Lei 4131`.

**Escopo:** S

---

#### Task 6.4 — `CHANGELOG` v0.8.0

**Descrição:** Bloco `ADDITIVE — Cotações — Modalidade Lei 4131/62` com:
- Resumo das capacidades novas.
- Campos novos em Proposta e payload de conversão.
- Não breaking change.

**Critérios de aceite:**
- [ ] Seção `[0.8.0] — 2026-MM-DD`.
- [ ] Bloco `INTERNAL — Migration S6_PropostaIrrf`.

**Escopo:** XS

---

#### Checkpoint Final

- [ ] `dotnet test` verde (todos suites).
- [ ] Build limpo sem warnings novos.
- [ ] Bruno valida fluxo manual ponta a ponta.
- [ ] Documentação revisada.
- [ ] PR pronto para review.

---

## 5. Riscos e Mitigações

| Risco                                                                                                                                                              | Probabilidade | Impacto | Mitigação                                                                                                                                                                                                                |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **IRRF deveria entrar no CET** (decisão de PO) — tratá-lo apenas como informativo pode levar a CET subestimado em comparações com bancos que praticam gross-up    | Alta          | Alto    | Documentar claramente AD-3; expor IRRF estimado em comparativo desde o MVP para o operador "ver"; abrir Onda 2 com decisão de incorporar IRRF ao CET. Confirmar com PO antes de iniciar Fase 3.                          |
| **Variação de alíquota IRRF por país do credor** (acordos de bitributação: Japão 12,5%, Espanha 10%, paraísos fiscais 25%, default 15%)                            | Alta          | Médio   | `TaxaIrrfAaPercentual` é input do operador (não tabela hard-coded). Documentar exemplos de alíquotas por país no `docs/api/cotacoes.md` para orientação operacional. Não modelar país do credor no MVP.                  |
| **Regulação BACEN** — RDE-ROF obrigatório só é emitido pós-contratação; capturar dados de registro antes pode gerar dados fantasmas                                 | Média         | Baixo   | AD-1 deferiu captura para `ConverterEmContratoCommand`. Apenas `SblcNumero` (opcional) é capturado. Sem campos de RDE-ROF no MVP.                                                                                       |
| **Custo da SBLC não entra no CET** — o tomador paga uma taxa anual ao banco emissor da SBLC (típico 0,5%–1,5% a.a.); ignorar subestima CET                          | Média         | Médio   | Onda 2: adicionar `CustoSblcAaPercentual` na proposta e incorporar ao CET. No MVP, documentar limitação. Quando custo SBLC for material, operador pode incluí-lo no `SpreadAaPercentual` da proposta como workaround.    |
| **Conflito de migration** com `S5_GarantiasExigidasLimite` ainda em revisão                                                                                         | Baixa         | Baixo   | `S6_PropostaIrrf` é aditiva em tabela diferente (`proposta`, não `limite_banco`). Sem conflito esperado, mas confirmar ordem de merge.                                                                                  |
| **Quebra de assinatura de `PropostaDto.From`** ao adicionar IRRF                                                                                                    | Baixa         | Baixo   | Campo nullable; default `null` mantém comportamento. Testes de mapeamento Bibliografia cobrem.                                                                                                                          |
| **Cotação multi-moeda Lei 4131 (EUR/JPY)** — cross-rate sem PTAX explícita usa USD como proxy (SPEC §5.1, comentário em CalculadoraCet linha 93-100)                | Baixa         | Médio   | Limitação herdada do MVP FINIMP; documentar no `docs/api/cotacoes.md` apêndice Lei 4131. Onda 2 introduz cross-rate explícito.                                                                                          |
| **CalculadoraIrrfEstimado divergir de cálculo do banco** por usar projeção simplificada de juros (não cronograma completo)                                          | Média         | Baixo   | Documentar como "estimativa". Aceitar diferença até 5% em testes de sanity. Onda 2 pode evoluir para usar cronograma completo via `CronogramaStrategy`.                                                                  |

---

## 6. Perguntas em Aberto

1. **IRRF no CET (crítico):** PO confirma manter IRRF como informativo no MVP (AD-3) ou prefere incorporar ao CET desde já? Se incorporar, exige reavaliação de todos os Goldens FINIMP existentes (sem IRRF, valor 0 — sem impacto numérico, mas exige decisão formal).
2. **Custo SBLC anual:** capturar `CustoSblcAaPercentual` na proposta agora (entra no CET) ou diferir para Onda 2? Probabilidade alta de ser pedido pelo operador no piloto.
3. **País do credor:** vale a pena modelar `PaisCredor` (ISO) na proposta para futuro auto-preenchimento de alíquota IRRF via tabela de acordos? No MVP, operador informa alíquota direta — mas isso pode evoluir.
4. **Quantidade de parcelas em Lei 4131:** Lei 4131 frequentemente tem amortização semestral/anual (não bullet). A `Proposta.EstruturaAmortizacao` já cobre Bullet/Price/SAC, e `CalculadoraCet` linha 137-139 estima parcelas mensais para non-bullet — isso é correto para Lei 4131 semestral? Validar com 1 golden adicional ou aceitar como limitação.
5. **`LimiteBanco` separado por modalidade:** confirmar que o operador deve cadastrar limite Lei 4131 **distinto** do limite FINIMP do mesmo banco (já é o caso pela UNIQUE constraint `(banco_id, modalidade, data_vigencia_inicio)` em §8.1). Confirmar visibilidade na tela de admin.
6. **Reabrir Lei 4131 sob `CalcularQuantidadeParcelas`:** confirmar se a estimativa atual (1 parcela mensal por 30 dias) é aceitável para Lei 4131 com prazo típico 1–3 anos. Caso contrário, adicionar input explícito de parcelas na Proposta (mudança fora do escopo deste plano).

---

## 7. Paralelização

- **Sequencial obrigatório:** Tasks 1.1 → 2.1 → 2.2 → 3.1 → 4.1 → 4.2 (caminho crítico do branch Lei 4131).
- **Paralelo após Checkpoint B:**
  - Task 3.2 (IRRF estimado) || Task 3.3 (validação SBLC pré-preenchimento) || Task 5.1 (golden case sem código novo).
- **Paralelo após Checkpoint D:**
  - Tasks 5.2, 5.3, 6.1, 6.2, 6.3, 6.4 (Goldens + Bruno + docs).
- **Bloqueio externo:** Task 3.3 espera fechamento de `tasks/garantias-em-limites/` (Fase 4 já em andamento).

---

## 8. Sumário Quantitativo

- **6 fases**, **14 tasks**, **6 checkpoints** (A, B, C, D, E, Final).
- **Escopo total:** ~4 M, ~7 S, ~3 XS.
- **Caminho crítico:** Task 1.1 → 2.1 → 2.2 → 3.1 → 4.1 → 4.2 (6 tasks).
- **Pré-requisitos externos:** (a) MVP FINIMP de Cotações em produção; (b) `tasks/garantias-em-limites/` Fase 4 mergeada.
- **CHANGELOG alvo:** v0.8.0 (ADDITIVE, não breaking).
