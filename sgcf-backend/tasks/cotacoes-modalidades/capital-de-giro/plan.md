# Plano de Implementação — Cotações de Capital de Giro

**Status:** Pendente de aprovação humana
**Autor:** Planning agent (read-only mode)
**Data:** 2026-05-18
**Dependências externas:**
- Conclusão (ou aceite de divergência) do plano FGI — `tasks/cotacoes-modalidades/fgi/plan.md` — devido a sobreposição quando `CapitalDeGiroDetail.TemFgi = true`.
- SPEC §13 decisão sobre PTAX para modalidades em BRL (Capital de Giro dispensa PTAX D-1).

---

## 1. Contexto

A SPEC atual (`docs/specs/cotacoes/SPEC.md`) declara o MVP como exclusivo de FINIMP (§1, §11.2). A modalidade Capital de Giro — crédito direto no balcão da Caixa Econômica Federal (produtos PROGER, FCO, BNDES Automático) — já está parcialmente modelada em `Contratos`:

- Domínio: `src/Sgcf.Domain/Contratos/CapitalDeGiroDetail.cs` (campos `NumeroOperacao`, `TipoProduto`, `TemFgi`).
- Conversão direta de contrato: `src/Sgcf.Application/Contratos/Commands/CreateContratoCommand.cs` linhas 326–338.
- Cronograma externo: `src/Sgcf.Application/Contratos/Commands/ImportarCronogramaCommand.cs` (linha 38 rejeita modalidades ≠ CapitalDeGiro).
- Bloqueio explícito de geração automática: `src/Sgcf.Application/Contratos/Commands/GerarCronogramaCommand.cs` linha 88.

O módulo de Cotações (`src/Sgcf.Application/Cotacoes/`) atualmente suporta apenas FINIMP no caminho de conversão (`ConverterEmContratoCommand.cs` linha 100) e exige PTAX D-1 USD/BRL incondicionalmente (`CriarCotacaoCommand.cs` linha 55). Para Capital de Giro precisamos:

1. Aceitar a modalidade `CapitalDeGiro` em `CriarCotacaoCommand` sem exigir PTAX (operação 100% BRL).
2. Capturar dados específicos do produto Caixa nas Propostas / Cotação (tipo de produto, FGI).
3. Estender `ConverterEmContratoCommand` para criar `CapitalDeGiroDetail` (e opcionalmente `FgiDetail`) e respeitar a regra "cronograma virá depois via importação manual" — não chamar `GerarCronograma` na conversão.
4. Adaptar `CalculadoraCet` para modalidades BRL sem PTAX e sem NDF.
5. Permitir cadastro de `LimiteBanco` específico para modalidade CapitalDeGiro.

A peculiaridade central é o **cronograma externo**: a Caixa entrega o cronograma definitivo em PDF/planilha após contratação. No momento da Cotação não há cronograma real; o CET é estimado a partir de premissas (taxa nominal a.a., prazo, IOF crédito, estrutura informada pelo gerente Caixa). O CET "de verdade" só pode ser recalculado depois que o cronograma for importado pós-conversão. Portanto, `EconomiaNegociacao` calculada no momento da conversão usa um CET-de-contrato **estimado** com a taxa final negociada — convergindo com a regra atual que já produz `cetContrato` via `CalculadoraCet` (linha 118 de `ConverterEmContratoCommand.cs`). A recálculo pós-importação fica fora do MVP (registrado em §6.Q3).

---

## 2. Decisões Arquiteturais

| #     | Decisão                                                                                                                                                                                                                              | Rationale                                                                                                                                                                                                |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AD-1  | **Reaproveitar 100% do agregado `Cotacao` / `Proposta`** — nenhum novo aggregate root para Capital de Giro                                                                                                                              | Cotação é genérica por design (SPEC §3.1); especificidades vão para campos opcionais e branch no handler de conversão (mesmo padrão de FINIMP via `FinimpDetail`)                                        |
| AD-2  | **PTAX D-1 deixa de ser pré-requisito obrigatório**; vira condicional para modalidades em moeda estrangeira (Finimp, Lei4131)                                                                                                        | Capital de Giro é 100% BRL; exigir PTAX bloqueia operação válida. Implementação: `CriarCotacaoCommandHandler` consulta PTAX **apenas** quando modalidade ∈ {Finimp, Lei4131}; persiste 1m e `null` quando BRL |
| AD-3  | **`TipoProduto` modelado como string livre** com validação contra **whitelist** configurável (default: PROGER, FCO, BNDES Automático, ConstrucCard, Outros)                                                                            | Whitelist hardcoded em `enum` exigiria migration a cada novo produto Caixa; string + whitelist permite evoluir; validação simples no handler                                                              |
| AD-4  | **`Proposta` ganha campos opcionais** `TipoProdutoCaixa` (string?), `TemFgiPrevisto` (bool, default false), `NumeroOperacaoCaixa` (string?, capturado posteriormente) — **sem mudança estrutural no agregado**                            | Mesma estratégia da SPEC §3.5 para garantias planas; preserva compatibilidade com FINIMP                                                                                                                   |
| AD-5  | **No momento da Cotação, Proposta carrega `EstruturaAmortizacao` + `PeriodicidadeJuros` + `PrazoDias` apenas como premissas** para estimativa de CET                                                                                  | Confirmação: o cronograma real virá da Caixa via `ImportarCronogramaCommand`. As premissas alimentam o `CalculadoraCet` estimado e permanecem como snapshot na `EconomiaNegociacao`                       |
| AD-6  | **`ConverterEmContratoCommand` NÃO dispara `GerarCronograma` para Capital de Giro**; cria o `Contrato` + `CapitalDeGiroDetail` + opcional `FgiDetail`; cronograma é responsabilidade do passo subsequente (`ImportarCronogramaCommand`)         | `GerarCronogramaCommand.cs` linha 88 já rejeita CapitalDeGiro; o fluxo correto é `Converter → Importar`. Documentar no CHANGELOG e na resposta da API                                                       |
| AD-7  | **Quando `TemFgi = true` na conversão, criar `FgiDetail` adicionalmente** com taxa FGI e percentual coberto vindos do `CapitalDeGiroDetailRequest`                                                                                       | Espelha o que `CreateContratoCommand.cs` já faz para `ModalidadeContrato.Fgi` (linha 340), mas dentro do branch CapitalDeGiro. **Interseção crítica com plano FGI** — alinhar antes de implementar              |
| AD-8  | **`LimiteBanco` para `Modalidade = CapitalDeGiro` segue cadastro normal** (modalidade já existe no enum `ModalidadeContrato`)                                                                                                            | Limite operacional Caixa pode ser por produto ou agregado; MVP usa agregado (mesmo padrão FINIMP); refinar por produto futuramente                                                                          |
| AD-9  | **`CalculadoraCet` ganha caminho BRL sem PTAX, sem NDF, sem cross-rate** — converge naturalmente porque já trata `Moeda.Brl` em `ConverterParaBrl` (linha 88)                                                                              | A função já é segura para BRL (retorna o próprio valor); precisamos apenas garantir que `ObterPtaxEfetivaAsync` em `RegistrarPropostaCommandHandler` retorne 1.0 sem consulta ao `ICotacaoFxRepository`     |
| AD-10 | **Migration `S6_PropostaCamposCapitalDeGiro`** adiciona colunas `tipo_produto_caixa`, `tem_fgi_previsto`, `numero_operacao_caixa` em `proposta` — todas nullable, default seguro                                                            | Aditivo; backwards-compatible com propostas FINIMP existentes (todas ficam `null`)                                                                                                                          |
| AD-11 | **CET estimado vira CET de contrato sem recálculo a partir do cronograma real no MVP**; recálculo automático pós-importação fica registrado como evolução em §6.Q3                                                                       | Mantém consistência com fluxo FINIMP atual (CET de contrato usa taxa final negociada via override em `CalculadoraCet`); evita aumento de escopo no MVP                                                       |

---

## 3. Grafo de Dependências

```
ModalidadeContrato.CapitalDeGiro (já existe)
TipoProduto whitelist (NEW — Sgcf.Domain.Cotacoes.TiposProdutoCaixa)
    │
    └─► Sgcf.Domain.Cotacoes.Proposta (estende — 3 campos opcionais)
            │
            ├─► Migration S6_PropostaCamposCapitalDeGiro
            │       └─► PropostaConfiguration (edita)
            │
            ├─► Sgcf.Application.Cotacoes.Commands.CriarCotacaoCommand
            │       └─► PTAX condicional (modalidade BRL ⇒ pula consulta)
            │
            ├─► Sgcf.Application.Cotacoes.Commands.RegistrarPropostaCommand
            │       └─► aceita tipoProdutoCaixa, temFgiPrevisto, numeroOperacaoCaixa
            │       └─► ObterPtaxEfetivaAsync (BRL ⇒ 1.0, já implementado)
            │
            ├─► Sgcf.Application.Cotacoes.Commands.ConverterEmContratoCommand
            │       └─► branch CapitalDeGiro: cria CapitalDeGiroDetail
            │       └─► branch (CapitalDeGiro AND TemFgi): cria FgiDetail
            │       └─► NÃO dispara GerarCronograma
            │
            └─► Sgcf.Application.Cotacoes.PropostaDto / CotacaoDto (estende)

LimiteBanco (sem mudança estrutural — modalidade CapitalDeGiro já suportada)

API:
    POST /api/v1/cotacoes (já existe) — aceita modalidade=CapitalDeGiro
    POST /api/v1/cotacoes/{id}/propostas (já existe) — aceita campos novos opcionais
    POST /api/v1/cotacoes/{id}/converter-em-contrato (estende request com CapitalDeGiroDetail)
    POST /api/v1/contratos/{id}/cronograma/importar (já existe — passo subsequente)

Documentação:
    docs/specs/cotacoes/SPEC.md (estende §1, §11.2, §5)
    docs/api/cotacoes.md (estende — seção Capital de Giro)
    docs/api/collections/sgcf-api/.../Cotacoes/ (Bruno)
    docs/changelog/CHANGELOG.md (v0.7.0 ADDITIVE — Cotações — Capital de Giro)

Golden dataset:
    tests/Sgcf.GoldenDataset/data/cotacao-balcao-caixa-proger.json (NEW)
```

---

## 4. Fases e Tarefas

### Fase 1 — Domínio

#### Task 1.1 — Adicionar campos opcionais Capital de Giro em `Proposta`

**Descrição:** Estender `src/Sgcf.Domain/Cotacoes/Proposta.cs` com 3 campos opcionais: `TipoProdutoCaixa` (string?, ≤40 chars), `TemFgiPrevisto` (bool, default false), `NumeroOperacaoCaixa` (string?, ≤60 chars). Atualizar factory `Cotacao.AdicionarProposta` com parâmetros opcionais.

**Critérios de aceite:**
- [ ] 3 campos novos com setters privados em `Proposta`
- [ ] `AdicionarProposta` em `Cotacao.cs` aceita os campos como parâmetros opcionais (default = null/false) — preserva todas chamadas FINIMP existentes
- [ ] Construtor privado para EF preservado
- [ ] Validação: quando `Cotacao.Modalidade == CapitalDeGiro`, `TipoProdutoCaixa` é **obrigatório** na hora da proposta (lança `InvalidOperationException` se nulo)
- [ ] Validação: `TipoProdutoCaixa` deve estar na whitelist (Task 1.2)
- [ ] `TemFgiPrevisto = true` só permitido quando `Modalidade == CapitalDeGiro`

**Verificação:**
- [ ] Testes em `tests/Sgcf.Domain.Tests/Cotacoes/PropostaTests.cs` cobrem: caminho FINIMP intocado, caminho CapitalDeGiro com tipo válido, rejeição de tipo inválido, rejeição de TipoProdutoCaixa nulo, rejeição de TemFgiPrevisto fora de CapitalDeGiro
- [ ] `dotnet test --filter "FullyQualifiedName~Proposta"` continua verde

**Dependências:** Task 1.2

**Arquivos prováveis:**
- `src/Sgcf.Domain/Cotacoes/Proposta.cs`
- `src/Sgcf.Domain/Cotacoes/Cotacao.cs`
- `tests/Sgcf.Domain.Tests/Cotacoes/PropostaCapitalDeGiroTests.cs`

**Escopo:** M

---

#### Task 1.2 — Criar `TiposProdutoCaixa` (constantes whitelist)

**Descrição:** Estrutura estática com whitelist de tipos de produto Caixa aceitos. Não é enum porque queremos string serializada e evolução sem migration.

**Critérios de aceite:**
- [ ] `src/Sgcf.Domain/Cotacoes/TiposProdutoCaixa.cs` com constantes `Proger`, `Fco`, `BndesAutomatico`, `ConstrucCard`, `Outros`
- [ ] Método `IsValid(string)` (case-insensitive, trim)
- [ ] Lista `Todos` exposta como `IReadOnlyList<string>`

**Verificação:**
- [ ] Teste unitário cobre casos válidos, inválidos, case-insensitive, trim

**Dependências:** nenhuma

**Arquivos prováveis:**
- `src/Sgcf.Domain/Cotacoes/TiposProdutoCaixa.cs`
- `tests/Sgcf.Domain.Tests/Cotacoes/TiposProdutoCaixaTests.cs`

**Escopo:** XS

---

#### Checkpoint A — Domínio

- [ ] `dotnet build` limpo
- [ ] `dotnet test tests/Sgcf.Domain.Tests` verde
- [ ] Revisão humana de AD-3 (whitelist em string vs enum) e da regra "TipoProdutoCaixa obrigatório quando modalidade = CapitalDeGiro"

---

### Fase 2 — Persistência

#### Task 2.1 — Migration `S6_PropostaCamposCapitalDeGiro`

**Descrição:** Adicionar 3 colunas em `proposta`: `tipo_produto_caixa` (varchar(40) nullable), `tem_fgi_previsto` (boolean not null default false), `numero_operacao_caixa` (varchar(60) nullable). Aditivo.

**Critérios de aceite:**
- [ ] Migration `dotnet ef migrations add S6_PropostaCamposCapitalDeGiro` gera scripts up/down limpos
- [ ] `dotnet ef database update` aplica em banco com propostas existentes sem afetar dados (todas ficam `null`/`false`)
- [ ] `dotnet ef migrations remove` reverte

**Verificação:**
- [ ] Reaplicar migration em banco com 5+ propostas FINIMP — verificar que continuam funcionando

**Dependências:** Task 1.1

**Arquivos prováveis:**
- `src/Sgcf.Infrastructure/Migrations/2026xxxx_S6_PropostaCamposCapitalDeGiro.cs`
- `src/Sgcf.Infrastructure/Migrations/SgcfDbContextModelSnapshot.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/PropostaConfiguration.cs`

**Escopo:** S

---

#### Task 2.2 — EF Configuration de `Proposta` atualizada

**Descrição:** Configurar mapeamento dos 3 campos novos em `PropostaConfiguration`.

**Critérios de aceite:**
- [ ] `TipoProdutoCaixa` → coluna nullable com max length 40
- [ ] `TemFgiPrevisto` → coluna NOT NULL default false
- [ ] `NumeroOperacaoCaixa` → coluna nullable com max length 60
- [ ] Round-trip persiste/recupera os 3 campos corretamente

**Verificação:**
- [ ] Teste de integração em `tests/Sgcf.Api.IntegrationTests/Cotacoes/PropostaCapitalDeGiroPersistenceTests.cs` salva e relê uma proposta com os 3 campos preenchidos

**Dependências:** Task 2.1

**Escopo:** S

---

#### Checkpoint B — Persistência

- [ ] Migration aplica e reverte limpo em banco vazio e com dados
- [ ] Round-trip de persistência funciona

---

### Fase 3 — Application / API

#### Task 3.1 — `CriarCotacaoCommand` torna PTAX condicional

**Descrição:** Em `src/Sgcf.Application/Cotacoes/Commands/CriarCotacaoCommand.cs`, encapsular a consulta de PTAX D-1 em um método auxiliar que só dispara para modalidades em moeda estrangeira (`Finimp`, `Lei4131`). Para `CapitalDeGiro`, gravar `PtaxUsadaUsdBrl = 1.0m` e `DataPtaxReferencia = DataAbertura` (ou null se o agregado aceitar — verificar `Cotacao.Criar`).

**Critérios de aceite:**
- [ ] Branch BRL: PTAX não consultada; `Cotacao.Criar` recebe `ptaxUsadaUsdBrl = 1.0m` e `dataPtaxReferencia = dataAbertura.PlusDays(-1)` para satisfazer invariante §3.2 regra 7 (data útil anterior)
- [ ] Branch USD/EUR/CNY/JPY: comportamento atual preservado
- [ ] Validator do command não muda — modalidade já é validada contra enum
- [ ] Mensagens de erro PTAX inexistente continuam corretas para modalidades em moeda estrangeira

**Verificação:**
- [ ] Testes unitários no handler: cenário CapitalDeGiro não chama `fxRepo` (mock); cenário Finimp chama
- [ ] Teste E2E em `tests/Sgcf.Api.IntegrationTests/Cotacoes/CriarCotacaoCapitalDeGiroTests.cs`: POST com modalidade=CapitalDeGiro e sem PTAX cadastrada → 201
- [ ] Regressão FINIMP: criar cotação Finimp sem PTAX cadastrada → 400 com mensagem atual

**Dependências:** Checkpoint B

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/CriarCotacaoCommand.cs`
- `tests/Sgcf.Application.Tests/Cotacoes/CriarCotacaoCommandHandlerTests.cs`
- `tests/Sgcf.Api.IntegrationTests/Cotacoes/CriarCotacaoCapitalDeGiroTests.cs`

**Escopo:** M

---

#### Task 3.2 — `RegistrarPropostaCommand` aceita campos Capital de Giro

**Descrição:** Estender `RegistrarPropostaCommand` (record) e seu handler com 3 parâmetros opcionais: `TipoProdutoCaixa`, `TemFgiPrevisto`, `NumeroOperacaoCaixa`. Propagar para `Cotacao.AdicionarProposta` (Task 1.1).

**Critérios de aceite:**
- [ ] Record `RegistrarPropostaCommand` ganha 3 campos opcionais ao final (default null/false) — preserva ordem dos campos atuais
- [ ] Validator: quando `MoedaOriginal == BRL` e cotação é `CapitalDeGiro`, exige `TipoProdutoCaixa` não vazio
- [ ] Validator: rejeita `ExigeNdf = true` quando cotação é `CapitalDeGiro` (NDF não se aplica a BRL doméstico)
- [ ] Validator: rejeita moeda ≠ BRL quando cotação é `CapitalDeGiro`
- [ ] Handler propaga os 3 campos novos para o agregado
- [ ] `ObterPtaxEfetivaAsync` retorna 1.0 para BRL (já implementado — linha 137 de RegistrarPropostaCommand.cs) — verificar
- [ ] CET calculado corretamente para BRL (já implementado em `CalculadoraCet.ConverterParaBrl` linha 88) — adicionar teste explícito

**Verificação:**
- [ ] Teste unitário valida cada nova regra (NDF rejeitado, moeda ≠ BRL rejeitada, TipoProdutoCaixa obrigatório)
- [ ] Teste E2E em `tests/Sgcf.Api.IntegrationTests/Cotacoes/RegistrarPropostaCapitalDeGiroTests.cs`: registrar proposta PROGER em cotação BRL → 201 com CET calculado
- [ ] Teste E2E: registrar proposta com NDF=true em cotação CapitalDeGiro → 400
- [ ] Regressão FINIMP: smoke test atual continua verde

**Dependências:** Task 3.1

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/RegistrarPropostaCommand.cs`
- `src/Sgcf.Application/Cotacoes/PropostaDto.cs` (estende DTO de resposta)
- `tests/Sgcf.Api.IntegrationTests/Cotacoes/RegistrarPropostaCapitalDeGiroTests.cs`

**Escopo:** M

---

#### Task 3.3 — `ConverterEmContratoCommand` cria `CapitalDeGiroDetail` + opcional `FgiDetail`

**Descrição:** Estender o handler em `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs` (linha 100) com branch para `CapitalDeGiro`. Adicionar parâmetros opcionais no command: `NumeroOperacaoCaixa`, `TipoProdutoCaixaFinal`, `TaxaFgiAaPct`, `PercentualCobertoFgiPct`, `NumeroOperacaoFgi`. Quando `Modalidade = CapitalDeGiro`, criar `CapitalDeGiroDetail` (usando dados da proposta + override do command); se `propostaAceita.TemFgiPrevisto || command.TaxaFgiAaPct.HasValue`, criar também `FgiDetail`.

**Critérios de aceite:**
- [ ] Branch CapitalDeGiro: cria `CapitalDeGiroDetail.Criar(contrato.Id, numeroOperacao, tipoProduto, temFgi, clock)` e chama `contratoRepo.AddCapitalDeGiroDetail(...)`
- [ ] Sub-branch FGI: quando `temFgi == true`, cria `FgiDetail.Criar(contrato.Id, numeroOperacaoFgi, taxaFgiAaPct, percentualCobertoPct, clock)` e chama `contratoRepo.AddFgiDetail(...)`
- [ ] Validação: se `temFgi == true` mas `TaxaFgiAaPct` é null → 400 com mensagem clara
- [ ] **Não dispara `GerarCronograma`** (já bloqueado upstream pela linha 88 de GerarCronogramaCommand.cs); resposta inclui hint textual em `Observacoes` indicando que cronograma deve ser importado em seguida via `ImportarCronogramaCommand`
- [ ] `EconomiaNegociacao` é criada normalmente usando CET-de-contrato estimado via `CalculadoraCet` (mesmo fluxo FINIMP, linhas 118 e 156)
- [ ] `LimiteBanco` é atualizado normalmente para modalidade CapitalDeGiro
- [ ] `ContratoDto.From(...)` retornado inclui `CapitalDeGiroDetail` e `FgiDetail` se aplicáveis

**Verificação:**
- [ ] Teste E2E `ConverterEmContratoCapitalDeGiroTests`: cotação CapitalDeGiro aceita → converter com payload PROGER → 200 com `CapitalDeGiroDetail` no DTO, sem cronograma persistido
- [ ] Teste E2E: cotação CapitalDeGiro aceita com `TemFgiPrevisto=true` → converter com `TaxaFgiAaPct=0.5` → 200 com ambos os details no DTO
- [ ] Teste E2E: `TemFgi=true` sem `TaxaFgiAaPct` → 400
- [ ] Fluxo encadeado: converter + importar cronograma manual → cronograma persistido no contrato gerado
- [ ] Regressão FINIMP: smoke test de conversão FINIMP continua verde

**Dependências:** Task 3.2

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs`
- `src/Sgcf.Application/Contratos/IContratoRepository.cs` (verificar se já expõe `AddCapitalDeGiroDetail` / `AddFgiDetail` — provavelmente sim, dado CreateContratoCommand os usa)
- `tests/Sgcf.Api.IntegrationTests/Cotacoes/ConverterEmContratoCapitalDeGiroTests.cs`

**Escopo:** L

---

#### Task 3.4 — DTOs e responses estendidos

**Descrição:** `PropostaDto` ganha os 3 campos novos. `ContratoDto` já comporta `CapitalDeGiroDetail`/`FgiDetail` (verificado em CreateContratoCommand.cs linha 356).

**Critérios de aceite:**
- [ ] `PropostaDto.From(...)` mapeia os 3 campos
- [ ] OpenAPI gerado reflete os novos campos opcionais

**Verificação:**
- [ ] Teste de contrato em `tests/Sgcf.Api.IntegrationTests/Cotacoes/PropostaDtoContractTests.cs` confirma campos no payload

**Dependências:** Tasks 1.1, 3.2

**Escopo:** S

---

#### Checkpoint C — Application/API ponta a ponta

- [ ] Fluxo completo Capital de Giro via Bruno: criar cotação BRL → adicionar banco → registrar proposta PROGER → aceitar → converter → importar cronograma → contrato com cronograma real
- [ ] Suite E2E `CotacoesCapitalDeGiro` verde
- [ ] Regressão FINIMP intocada
- [ ] Revisão humana antes de prosseguir para documentação e golden dataset

---

### Fase 4 — Cronograma externo (workflow doc-only)

#### Task 4.1 — Documentar fluxo "Converter → Importar cronograma"

**Descrição:** A operação de importação já está implementada (`ImportarCronogramaCommand.cs`). Esta task documenta a sequência obrigatória para Capital de Giro e adiciona resposta enriquecida em `ConverterEmContratoCommand` quando modalidade for CapitalDeGiro.

**Critérios de aceite:**
- [ ] `ContratoDto` (ou um wrapper específico de conversão) inclui campo informativo `proximoPassoSugerido: "ImportarCronograma"` quando modalidade = CapitalDeGiro
- [ ] Documentação em `docs/api/cotacoes.md` descreve sequência: conversão cria contrato sem cronograma; chamada subsequente a `POST /api/v1/contratos/{id}/cronograma/importar` finaliza
- [ ] Bruno collection inclui um *folder* "Fluxo Capital de Giro — completo" com encadeamento dos requests

**Verificação:**
- [ ] Revisão de documentação por PO
- [ ] Bruno: rodar folder end-to-end manualmente em ambiente dev

**Dependências:** Task 3.3

**Escopo:** S

---

#### Checkpoint D — Cronograma externo

- [ ] Fluxo encadeado validado manualmente via Bruno
- [ ] Documentação aprovada

---

### Fase 5 — Documentação e Golden Dataset

#### Task 5.1 — Atualizar `docs/specs/cotacoes/SPEC.md`

**Critérios de aceite:**
- [ ] §1: modalidades MVP passa de "FINIMP somente" para "FINIMP + Capital de Giro"
- [ ] §11.2: remover Capital de Giro da lista "fora do escopo"
- [ ] §5.1: nota sobre caminho BRL no cálculo de CET (sem PTAX, sem NDF, sem cross-rate)
- [ ] §3.2 invariante 7 revisada: PTAX D-1 obrigatória **apenas** para modalidades em moeda estrangeira
- [ ] Nova seção §5.4 "CET estimado vs CET real (Capital de Giro)" explicando que cronograma externo torna o CET de contrato uma estimativa no momento da conversão
- [ ] §13 atualizada com decisões deste plano

**Escopo:** S

---

#### Task 5.2 — Atualizar `docs/api/cotacoes.md`

**Critérios de aceite:**
- [ ] Seção "Modalidade: Capital de Giro" descreve campos opcionais
- [ ] Exemplo de POST /cotacoes com modalidade=CapitalDeGiro
- [ ] Exemplo de POST /propostas com payload PROGER
- [ ] Exemplo de POST /converter-em-contrato com CapitalDeGiroDetail (com e sem FGI)
- [ ] Seção "Fluxo encadeado pós-conversão" linka para `docs/api/contratos.md` cronograma manual

**Escopo:** S

---

#### Task 5.3 — Bruno collection

**Critérios de aceite:**
- [ ] Folder novo `Cotacoes/Modalidade-CapitalDeGiro/` com requests numerados:
  - 01. Criar Cotação Capital de Giro
  - 02. Adicionar Banco (Caixa) — limite Capital de Giro
  - 03. Registrar Proposta PROGER
  - 04. Registrar Proposta PROGER com FGI
  - 05. Aceitar Proposta
  - 06. Converter em Contrato (com FGI quando aplicável)
  - 07. Importar Cronograma manual (linka pasta de contratos)

**Escopo:** S

---

#### Task 5.4 — Golden dataset

**Critérios de aceite:**
- [ ] `tests/Sgcf.GoldenDataset/data/cotacao-balcao-caixa-proger.json` com cenário PROGER típico (R$ 500k, 24 meses, taxa 9% a.a., IOF crédito padrão, Price mensal)
- [ ] `input` cobre `CriarCotacao` + `RegistrarProposta` + `AceitarProposta` + `ConverterEmContrato`
- [ ] `expectedOutput`: CET calculado, valor total estimado em BRL, ausência de cronograma após conversão
- [ ] Cenário adicional (opcional): PROGER + FGI 0,5% / 80% cobertura

**Verificação:**
- [ ] `dotnet test tests/Sgcf.GoldenDataset` verde

**Escopo:** M

---

#### Task 5.5 — CHANGELOG v0.7.0

**Critérios de aceite:**
- [ ] Seção `[0.7.0] — 2026-MM-DD` adicionada
- [ ] Bloco `ADDITIVE — Cotações — Capital de Giro` descreve nova modalidade, campos opcionais, PTAX condicional, branch de conversão
- [ ] Bloco `INTERNAL — Migration S6` documenta colunas novas
- [ ] Bloco `BREAKING-NOTE` (se for o caso): nenhuma; tudo aditivo

**Escopo:** XS

---

#### Checkpoint Final

- [ ] `dotnet test` verde (Domain + Application + Integration + GoldenDataset)
- [ ] Build limpo, sem warnings novos
- [ ] Bruno collection validada manualmente (fluxo completo + cenário com FGI)
- [ ] Documentação revisada por PO
- [ ] PR pronto para review

---

## 5. Riscos e Mitigações

| #  | Risco                                                                                                                                                | Probabilidade | Impacto | Mitigação                                                                                                                                                                                                                            |
| -- | ---------------------------------------------------------------------------------------------------------------------------------------------------- | ------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| R1 | **Sobreposição com plano FGI:** Task 3.3 cria `FgiDetail` dentro do branch CapitalDeGiro; o plano FGI pode estabelecer outra modalidade pura para FGI standalone | Alta          | Alto    | **Coordenar antes de iniciar Task 3.3.** Definir contrato: o plano FGI cobre `ModalidadeContrato.Fgi` (linha 340 de CreateContratoCommand.cs); este plano cobre `CapitalDeGiro AND TemFgi`. São paths disjuntos. Documentar a decisão antes da implementação |
| R2 | **CET estimado pode divergir significativamente do CET real** (que dependerá do cronograma importado, eventualmente meses depois)                          | Alta          | Médio   | Documentar limitação na SPEC §5.4; manter `EconomiaNegociacao` imutável conforme regra atual; criar issue de evolução para recálculo opcional pós-importação                                                                              |
| R3 | **Assumption USD remanescente no `CalculadoraCet`:** comentário na linha 95 menciona "moedas não-BRL convertidas via USD". Para BRL puro, fluxo é seguro mas vale auditar | Média         | Médio   | Adicionar teste explícito em `CalculadoraCetTests` cobrindo BRL puro (ptaxUsdBrl=1.0, sem cross-rate); confirmar que CET converge para a taxa nominal + spread sem distorção                                                          |
| R4 | **Whitelist hardcoded de TipoProdutoCaixa** envelhece                                                                                                | Média         | Baixo   | AD-3: string + whitelist permite adicionar produto sem migration; admin pode reabrir `Outros` para casos pontuais; revisar lista por sprint                                                                                            |
| R5 | **`Cotacao.Criar` exige `DataPtaxReferencia`** que pode ser inválido para BRL (não é dia útil anterior?)                                                | Média         | Médio   | Verificar invariante §3.2 regra 7 antes de implementar Task 3.1; se o agregado rejeitar, considerar nullable `DataPtaxReferencia` em migration adicional (S6b)                                                                            |
| R6 | **`ICotacaoFxRepository` mock em testes** pode passar PTAX 1.0 indevidamente — falsos positivos                                                       | Baixa         | Médio   | Teste de regressão FINIMP explícito no Checkpoint C; afirmação no mock que repo **não** é chamado para CapitalDeGiro                                                                                                                       |
| R7 | **Bruno collection desatualizada** confunde operador                                                                                                  | Média         | Baixo   | Task 5.3 inclui fluxo completo end-to-end; operador valida no Checkpoint Final                                                                                                                                                          |

---

## 6. Perguntas em Aberto

1. **Q1 — FGI dentro de Capital de Giro vs FGI standalone:** O plano FGI separado define `ModalidadeContrato.Fgi` como modalidade pura. Confirmar que o branch deste plano (Task 3.3) cobre apenas o caso `CapitalDeGiro + TemFgi`, e que ambos os planos são complementares (não duplicados). Sugestão: encontro síncrono entre os dois agentes/PO antes de iniciar Task 3.3.

2. **Q2 — Taxa FGI no momento da Cotação:** A taxa FGI (`TaxaFgiAaPct`) é conhecida no momento da Proposta ou apenas na conversão? Se conhecida antes, deveria entrar no `CET estimado` (custo adicional). Sugestão default: capturar na conversão (proposta carrega apenas `TemFgiPrevisto` como bool); CET não incorpora FGI no MVP. Documentar como limitação.

3. **Q3 — Recálculo de CET pós-importação de cronograma:** Quando o cronograma real chegar, o CET do contrato pode ser recalculado. Implementar agora ou diferir? Sugestão: **diferir** — `EconomiaNegociacao` é imutável (SPEC §12.3) e refletir mudança quebraria essa regra. Criar issue separada para "CET realizado vs CET estimado" como métrica complementar.

4. **Q4 — `LimiteBanco` por TipoProduto?** Caixa pode ter limites distintos por produto (PROGER vs FCO). Modelar agora (UQ `banco_id, modalidade, tipo_produto, data_vigencia`) ou agregar tudo em um único limite CapitalDeGiro por banco? Sugestão default: agregado por modalidade no MVP; refinar via campo `Observacoes` se necessário.

5. **Q5 — IOF crédito em BRL:** IOF de operação doméstica BRL difere de IOF câmbio. `RegistrarPropostaCommand` aceita `IofPct` — confirma que o cálculo atual em `CalculadoraCet` (linha 157) trata corretamente IOF crédito (sobre principal, em t=0)? Validar com golden dataset (Task 5.4).

6. **Q6 — Campo `NumeroOperacaoCaixa` na Proposta vs no Contrato:** O número operacional Caixa só sai após contratação. Deveria estar **apenas** no `CapitalDeGiroDetail` (já está) e não na Proposta? Sugestão: remover de `Proposta` (deixar só no detail); ajustar Task 1.1.

---

## 7. Paralelização

- **Sequencial obrigatório:** Task 1.2 → 1.1 → 2.1 → 2.2 → 3.1 → 3.2 → 3.3 → 3.4
- **Paralelo possível após Checkpoint B:** Task 5.4 (golden dataset) pode iniciar enquanto Task 3.1/3.2/3.3 são implementadas (input do golden é estável após Domínio + Persistência)
- **Paralelo após Checkpoint C:** Tasks 5.1, 5.2, 5.3, 5.5 (documentação) executam em paralelo
- **Bloqueio externo:** Task 3.3 depende de alinhamento com plano FGI (Q1)

---

## 8. Sumário Quantitativo

- **5 fases**, **13 tasks**, **5 checkpoints** (A, B, C, D, Final)
- **Escopo total:** 1 L + 4 M + 6 S + 2 XS
- **Caminho crítico:** Task 1.2 → 1.1 → 2.1 → 2.2 → 3.1 → 3.2 → 3.3 (7 tasks)
- **Migrations novas:** 1 (S6_PropostaCamposCapitalDeGiro)
- **Endpoints novos:** 0 (reutiliza POST /cotacoes, POST /propostas, POST /converter-em-contrato, POST /contratos/{id}/cronograma/importar)
- **Pré-requisito externo:** alinhamento com plano FGI (`tasks/cotacoes-modalidades/fgi/plan.md`)
- **Arquivos editados (estimativa):** ~12 fontes + ~8 testes + ~5 docs
