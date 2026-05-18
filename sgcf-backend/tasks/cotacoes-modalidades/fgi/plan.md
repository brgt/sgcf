# Plano de Implementação — Cotações de FGI

**Status:** Pendente de aprovação humana
**Autor:** Planning agent (read-only mode)
**Data:** 2026-05-18
**Dependências externas:** Confirmação da regra de cobrança da tarifa FGI (anual sobre saldo devedor) com o time financeiro; eventual sequenciamento com o plano de Capital de Giro (ver §1.4).

---

## 1. Contexto

### 1.1. Estado atual

O módulo de Cotações (`src/Sgcf.Application/Cotacoes/`) cobre apenas FINIMP no MVP (SPEC §1, §11.2). O domínio de `Contrato` (`src/Sgcf.Domain/Contratos/`) já possui suporte completo a FGI como **modalidade** (`ModalidadeContrato.Fgi`, `FgiDetail`) e como **garantia** (`TipoGarantia.Fgi`, `GarantiaFgiDetail`). A geração de cronograma já adiciona automaticamente o evento `TarifaFgi` quando a modalidade é FGI (`src/Sgcf.Application/Contratos/Commands/GerarCronogramaCommand.cs:116`, método `AdicionarTarifaFgiAsync` linha 160).

O comando `ConverterEmContratoCommand` (`src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs:100`) trata somente o ramo FINIMP. As demais modalidades (Lei4131, REFINIMP, NCE, CapitalDeGiro, Fgi) caem no caminho default e nenhum detail é criado.

### 1.2. Disambiguação FGI-modalidade × FGI-garantia (crítico)

O domínio do SGCF possui **duas representações distintas** do FGI:

| Aspecto | FGI como **modalidade** | FGI como **garantia** |
|---------|-------------------------|-----------------------|
| Enum | `ModalidadeContrato.Fgi` | `TipoGarantia.Fgi` |
| Detail aggregate | `Sgcf.Domain.Contratos.FgiDetail` (1:1 com `Contrato`) | `Sgcf.Domain.Contratos.GarantiaFgiDetail` (1:1 com `Garantia`) |
| Campos principais | `NumeroOperacaoFgi`, `TaxaFgiAa` (anual, sobre saldo devedor), `PercentualCoberto` | `TipoFgi` (PEAC, NOVO_EMPREENDEDOR), `PercentualCobertura`, `TaxaFgiAa`, `BancoIntermediario`, `CodigoOperacaoBndes` |
| Significado | Linha de crédito FGI direta (Caixa/BV operam linhas garantidas pelo FGI Programa BNDES) | Garantia FGI acoplada a outra modalidade (ex.: `CapitalDeGiroDetail.TemFgi = true` ou `Garantia` tipada como FGI em qualquer contrato) |
| Cronograma | Gera evento `TarifaFgi` automaticamente (`GerarCronogramaCommand.cs:116`) | Não gera evento próprio; apenas registra a cobertura |

**Escopo deste plano:** exclusivamente **FGI como modalidade**. O suporte a FGI como garantia já existe via `GarantiaExigidaLimite` (v0.6.0, ver `tasks/garantias-em-limites/plan.md`) — basta cadastrar `TipoGarantia.Fgi` na coleção `GarantiasExigidas` do `LimiteBanco` da modalidade-hospedeira (ex.: Capital de Giro). Nenhuma alteração estrutural adicional é necessária para o caso "FGI anexo".

### 1.3. Mecânica da tarifa FGI

`FgiDetail.TaxaFgiAa` é uma `Percentual` (armazenada como fração 0..1). A tarifa é **anual e incide sobre o saldo devedor** do contrato — `GerarCronogramaCommand.AdicionarTarifaFgiAsync` (linhas 160-195) projeta um evento `TipoEventoCronograma.TarifaFgi` único, no vencimento, calculado como:

```
valorTarifa = principal × taxaFgiAa × prazoDias / baseCalculo  (default Dias360)
```

Esse modelo simplificado funciona para FGI bullet (caso típico Caixa/BV). Para estruturas Price/SAC seria necessário projetar uma tarifa por janela anual sobre saldo médio — fora do escopo MVP.

`PercentualCoberto` é **informativo**: indica a fração do principal coberta pelo FGI em caso de inadimplência. Não entra no CET (não há fluxo de caixa associado) — apenas é exibido na proposta.

### 1.4. Interseção com Capital de Giro

Um contrato `CapitalDeGiro` pode ter `TemFgi = true` (`src/Sgcf.Application/Contratos/Commands/CreateContratoCommand.cs:326-338`). Nesse caso o FGI funciona **como garantia**, não como modalidade — o `LimiteBanco` da modalidade CapitalDeGiro terá `GarantiaExigidaLimite` com `TipoGarantia.Fgi`. **Não confundir com este plano**: aqui modelamos cotações cuja `Modalidade == Fgi`.

### 1.5. Moeda

FGI é cobrado e desembolsado em BRL. Reaproveita-se a decisão já tomada para NCE/CapitalDeGiro: cotação BRL dispensa PTAX D-1 (cross-rate retorna 1, ver `RegistrarPropostaCommand.ObterPtaxEfetivaAsync` linha 136). Cabe revalidar no fluxo `CriarCotacaoCommand` para não exigir cadastro de PTAX quando `Modalidade == Fgi`.

---

## 2. Decisões Arquiteturais

| # | Decisão | Rationale |
|---|---------|-----------|
| AD-1 | Reaproveitar `Sgcf.Domain.Contratos.FgiDetail` sem modificar a entidade | Já estável e usada em `CreateContratoCommand`; alterar quebraria contratos existentes |
| AD-2 | Estender `RegistrarPropostaCommand` com campos opcionais FGI (`NumeroOperacaoFgi`, `TaxaFgiAaPct`, `PercentualCobertoPct`) condicionais à modalidade da Cotação | Mantém compatibilidade com FINIMP; rejeita combinações inválidas no validador |
| AD-3 | Persistir os campos FGI em **colunas planas da Proposta** (não criar `PropostaFgiDetail` separado) | FGI tem poucos campos (3 numéricos + número da operação); coluna plana é proporcional. REFINIMP/Lei4131 podem demandar tabela própria, mas FGI não justifica |
| AD-4 | Estender `CalculadoraCet` com novo ramo: incorporar tarifa FGI anual sobre saldo devedor médio ao fluxo de caixa | A tarifa FGI muda o custo efetivo significativamente (0,3% a 1,5% a.a.); ignorar levaria CET subestimado e perda de fidelidade na comparação contra outras modalidades |
| AD-5 | Modelar tarifa FGI no CET como **fluxo único em t = prazoDias** (bullet) com valor = `principal × taxaFgi × prazo / 360` | Espelha exatamente `GerarCronogramaCommand.AdicionarTarifaFgiAsync` — garante que CET da cotação ≈ CET do contrato fechado |
| AD-6 | `PercentualCoberto` **não entra no CET** | É informativo; representa cobertura para a contraparte (banco/fundo), não custo do tomador |
| AD-7 | `CriarCotacaoCommand` dispensa PTAX D-1 quando `Modalidade == Fgi` | Operação 100% BRL; consistente com NCE/CapitalDeGiro |
| AD-8 | `ConverterEmContratoCommand` ganha ramo `Modalidade == Fgi` que cria `FgiDetail` via `FgiDetail.Criar(...)` | Espelha o ramo FINIMP existente (linhas 99-113) |
| AD-9 | `LimiteBanco` para FGI é cadastro autônomo (`Modalidade = Fgi`) | Mesma estrutura usada para FINIMP; não há modelagem nova |
| AD-10 | Não validar coerência entre `TaxaFgiAa` da proposta e `TaxaFgiAa` do banco no MVP | Bancos negociam taxa caso a caso; rigidez prejudicaria o operador. Pode virar alerta informativo |
| AD-11 | Migration aditiva (`S6_PropostaFgi`) adiciona apenas colunas nullable em `proposta` | Sem quebra de dados; rollback simples |

---

## 3. Grafo de Dependências

```
ModalidadeContrato.Fgi (existe)
FgiDetail (existe — src/Sgcf.Domain/Contratos/FgiDetail.cs)
    │
    ├─► Sgcf.Domain.Cotacoes.Proposta (estende com campos FGI planos)
    │       │
    │       └─► Sgcf.Domain.Cotacoes.CalculadoraCet (estende com tarifa FGI)
    │
    ├─► Migration S6_PropostaFgi (colunas nullable em proposta)
    │       └─► PropostaConfiguration (atualiza)
    │               └─► CotacaoRepository (sem mudança — já carrega proposta)
    │
    ├─► Application: CriarCotacaoCommand (dispensa PTAX quando Fgi)
    ├─► Application: RegistrarPropostaCommand (aceita campos FGI; validador condicional)
    ├─► Application: ConverterEmContratoCommand (ramo Fgi cria FgiDetail)
    │
    ├─► API: PropostasController / CotacoesController (DTOs estendidos)
    │
    ├─► Bruno collection: novos requests POST cotação FGI + proposta FGI
    │
    ├─► Golden dataset: cenário FGI bullet 12m (PercentualCoberto 80%, TaxaFgiAa 0,5%)
    │
    └─► Documentação
            ├─ docs/specs/cotacoes/SPEC.md (modalidade Fgi adicionada)
            ├─ docs/api/cotacoes.md (campos novos)
            └─ docs/changelog/CHANGELOG.md (v0.x.0 ADDITIVE)
```

---

## 4. Fases e Tarefas

### Fase 1: Domínio

#### Task 1.1 — Estender `Proposta` com campos FGI planos

**Descrição:** Adicionar três campos opcionais ao agregado `Proposta`: `NumeroOperacaoFgi` (string?), `TaxaFgiAaDecimal` (decimal? — fração 0..1), `PercentualCobertoDecimal` (decimal? — fração 0..1). Expor `Percentual?` tipado nos getters públicos (consistente com `FgiDetail`).

**Critérios de aceite:**
- [ ] Campos adicionados a `src/Sgcf.Domain/Cotacoes/Proposta.cs` com setters privados
- [ ] Construtor `internal` da `Proposta` ganha os 3 parâmetros opcionais
- [ ] Método público `Cotacao.AdicionarProposta(...)` ganha os 3 parâmetros opcionais
- [ ] Invariantes adicionais: quando `Cotacao.Modalidade == Fgi`, exigir `TaxaFgiAaDecimal` informada (> 0) e `PercentualCobertoDecimal` em (0, 1]; quando `Modalidade != Fgi`, todos os 3 devem ser nulos (regra defensiva — rejeita campos errantes)
- [ ] Cache CET invalidado quando algum campo FGI muda
- [ ] Conversão "humano (pct) → fração" idêntica a `FgiDetail.Criar` para evitar drift

**Verificação:**
- [ ] `tests/Sgcf.Domain.Tests/Cotacoes/PropostaFgiTests.cs` cobre: criação válida com FGI, criação inválida sem TaxaFgi, criação inválida com FGI em modalidade não-FGI, PercentualCoberto fora do intervalo
- [ ] Testes existentes de `Proposta`/`Cotacao` continuam verdes

**Dependências:** nenhuma
**Escopo:** M
**Arquivos prováveis:**
- `src/Sgcf.Domain/Cotacoes/Proposta.cs`
- `src/Sgcf.Domain/Cotacoes/Cotacao.cs`
- `tests/Sgcf.Domain.Tests/Cotacoes/PropostaFgiTests.cs`

---

#### Task 1.2 — Estender `CalculadoraCet` para incorporar tarifa FGI

**Descrição:** Adicionar à função `CalcularCet` (em `src/Sgcf.Domain/Cotacoes/CalculadoraCet.cs`) um fluxo extra que representa a tarifa FGI quando `Cotacao.Modalidade == Fgi` (a `Proposta` carrega `TaxaFgiAaDecimal`). O fluxo é único, datado em `dataDesembolso.PlusDays(prazoDias)`, no valor `principalBrl × taxaFgi × prazoDias / 360`. Mantém pureza da função (sem I/O).

**Critérios de aceite:**
- [ ] Helper `MontarFluxoBrl` ganha um bloco que, se `proposta.TaxaFgiAaDecimal.HasValue`, adiciona ao fluxo um custo positivo (saída para o tomador) em `t = prazoDias`
- [ ] Fórmula idêntica a `GerarCronogramaCommand.AdicionarTarifaFgiAsync` (linha 180) — garantia de consistência cotação ↔ contrato
- [ ] Função permanece pura (sem `IClock`, sem I/O); recebe `Proposta` somente
- [ ] Property: CET com tarifa FGI > CET sem tarifa FGI, ceteris paribus
- [ ] Property: CET escala linearmente com `taxaFgi` para `taxaFgi` pequena

**Verificação:**
- [ ] `tests/Sgcf.Domain.Tests/Cotacoes/CalculadoraCetFgiTests.cs` cobre: CET FGI bullet 12m em BRL, comparativo "com FGI vs sem FGI", regressão FINIMP (mesmas entradas sem FGI produzem mesmo CET de hoje)
- [ ] Property-based tests (FsCheck) confirmam monotonicidade e linearidade local

**Dependências:** Task 1.1
**Escopo:** M
**Arquivos prováveis:**
- `src/Sgcf.Domain/Cotacoes/CalculadoraCet.cs`
- `tests/Sgcf.Domain.Tests/Cotacoes/CalculadoraCetFgiTests.cs`

---

#### Checkpoint A — Domínio

- [ ] `dotnet build` limpo, sem warnings novos
- [ ] `dotnet test tests/Sgcf.Domain.Tests` verde
- [ ] Revisão humana das fórmulas (AD-4, AD-5) e da definição de `PercentualCoberto` antes de prosseguir

---

### Fase 2: Persistência

#### Task 2.1 — Migration `S6_PropostaFgi`

**Descrição:** Adicionar 3 colunas nullable à tabela `proposta`: `numero_operacao_fgi` (text), `taxa_fgi_aa_decimal` (numeric), `percentual_coberto_decimal` (numeric).

**Critérios de aceite:**
- [ ] Migration EF gerada via `dotnet ef migrations add S6_PropostaFgi --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api`
- [ ] Migration aditiva, sem alterar colunas existentes
- [ ] Down reverte (drop columns)
- [ ] Constraint `CHECK ((modalidade_cotacao = 'Fgi') OR (taxa_fgi_aa_decimal IS NULL AND percentual_coberto_decimal IS NULL))` — assegura que campos FGI só aparecem em cotações FGI. **Atenção:** essa CHECK exige join — preferível **deixar para validação de aplicação** (AD-10 e regra de Task 1.1). Documentar no plano e remover do CHECK na migration final.

**Verificação:**
- [ ] `dotnet ef database update` aplica em banco com dados existentes (propostas FINIMP existentes ficam com NULLs)
- [ ] `dotnet ef migrations remove` reverte sem erro

**Dependências:** Task 1.1
**Escopo:** S
**Arquivos prováveis:**
- `src/Sgcf.Infrastructure/Migrations/2026xxxx_S6_PropostaFgi.cs` (+ `.Designer.cs`)
- `src/Sgcf.Infrastructure/Migrations/SgcfDbContextModelSnapshot.cs`

---

#### Task 2.2 — Atualizar `PropostaConfiguration`

**Descrição:** Mapear as 3 novas colunas em `src/Sgcf.Infrastructure/Persistence/Configurations/PropostaConfiguration.cs`. Precisão dos `numeric` segue padrão de `Percentual` no projeto (verificar `FgiDetailConfiguration` como referência).

**Critérios de aceite:**
- [ ] Mapeamento completo das 3 colunas, com precisão consistente com `FgiDetail`
- [ ] `CotacaoRepository.GetByIdWithPropostasAsync` continua funcionando sem mudança (já faz `Include(Propostas)`)
- [ ] Teste de integração persistir-recarregar preserva valores

**Verificação:**
- [ ] `tests/Sgcf.Api.IntegrationTests/Cotacoes/PropostaFgiPersistenceTests.cs` salva e recarrega proposta FGI

**Dependências:** Task 2.1
**Escopo:** S

---

#### Checkpoint B — Persistência

- [ ] Migration aplica e reverte sem erro em banco com dados
- [ ] Round-trip de persistência funciona para FGI

---

### Fase 3: Application + API

#### Task 3.1 — `CriarCotacaoCommand` dispensa PTAX D-1 para FGI

**Descrição:** Quando `Modalidade == Fgi`, pular a busca de PTAX e usar `ptaxUsadaUsdBrl = 1m` + `dataPtaxReferencia = dataAbertura.PlusDays(-1)` (ou `dataAbertura`, conforme decisão de NCE/CapitalDeGiro — verificar consistência no momento da implementação).

**Critérios de aceite:**
- [ ] `CriarCotacaoCommandHandler.Handle` ramifica em `if (modalidade == ModalidadeContrato.Fgi)` antes de chamar `fxRepo.GetMaisRecenteAsync`
- [ ] Mesma decisão para BRL é registrada em **um único helper** se possível (NCE, CapitalDeGiro, Fgi compartilham essa regra)
- [ ] Teste unitário do handler valida que cotação FGI é criada sem PTAX cadastrada

**Verificação:**
- [ ] `tests/Sgcf.Application.Tests/Cotacoes/CriarCotacaoFgiTests.cs` cria cotação FGI sem PTAX no banco
- [ ] Cotação FINIMP continua exigindo PTAX (regressão)

**Dependências:** Task 1.1
**Escopo:** S

---

#### Task 3.2 — `RegistrarPropostaCommand` aceita campos FGI

**Descrição:** Estender o record `RegistrarPropostaCommand` com `NumeroOperacaoFgi: string?`, `TaxaFgiAaPct: decimal?`, `PercentualCobertoPct: decimal?`. Validador exige presença/ausência condicional à modalidade da cotação. Handler propaga os campos para `Cotacao.AdicionarProposta`.

**Critérios de aceite:**
- [ ] 3 parâmetros opcionais (default null) no record
- [ ] `RegistrarPropostaCommandValidator`: quando carregar `cotacao.Modalidade == Fgi`, exige `TaxaFgiAaPct > 0` e `PercentualCobertoPct ∈ (0, 100]`; quando não-FGI, exige os 3 nulos
- [ ] Handler converte `pct → fração` (dividir por 100) idêntico a `FgiDetail.Criar`
- [ ] Backwards-compatible (campos opcionais; payload antigo de FINIMP continua válido)

**Verificação:**
- [ ] `tests/Sgcf.Application.Tests/Cotacoes/RegistrarPropostaFgiTests.cs` cobre: registro válido FGI, ausência de TaxaFgi em FGI (400), presença de TaxaFgi em FINIMP (400)
- [ ] E2E em `tests/Sgcf.Api.IntegrationTests` registra proposta FGI e verifica CET coerente com Golden Dataset

**Dependências:** Tasks 1.1, 1.2, 2.2
**Escopo:** M
**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/RegistrarPropostaCommand.cs`
- `src/Sgcf.Application/Cotacoes/PropostaDto.cs` (expõe os 3 campos)

---

#### Task 3.3 — `ConverterEmContratoCommand` cria `FgiDetail`

**Descrição:** Em `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs:100`, adicionar ramo `if (cotacao.Modalidade == ModalidadeContrato.Fgi)` que cria `FgiDetail` via `FgiDetail.Criar(...)` reutilizando os campos persistidos na `propostaAceita`. Adicionar `AddFgiDetail` ao `IContratoRepository` se ainda não exposto (verificar — já é usado em `CreateContratoCommand.cs:351`).

**Critérios de aceite:**
- [ ] Ramo FGI cria `FgiDetail` com `NumeroOperacaoFgi`, `TaxaFgiAaPct` (convertido fração→pct para casar com `FgiDetail.Criar`), `PercentualCobertoPct` da proposta aceita
- [ ] `ContratoDto.From` recebe o `FgiDetail` (assinatura já existente em `CreateContratoCommand.cs:356`)
- [ ] Campos `RofNumero`, `ExportadorNome`, etc. (específicos FINIMP) ficam null para FGI
- [ ] `LimiteBanco` para modalidade FGI tem `ValorUtilizadoBRL` atualizado conforme regra atual

**Verificação:**
- [ ] `tests/Sgcf.Application.Tests/Cotacoes/ConverterEmContratoFgiTests.cs` converte cotação FGI aceita → contrato com `FgiDetail` populado
- [ ] Cronograma do contrato gerado contém evento `TarifaFgi` (smoke E2E)

**Dependências:** Tasks 1.1, 3.2
**Escopo:** M

---

#### Task 3.4 — API e DTOs

**Descrição:** Atualizar `PropostaDto`, `CotacaoDto` (se necessário) e os endpoints `POST /api/v1/cotacoes` e `POST /api/v1/cotacoes/{id}/propostas` para refletir os campos FGI. Atualizar Swagger/OpenAPI.

**Critérios de aceite:**
- [ ] `PropostaDto` expõe `numeroOperacaoFgi`, `taxaFgiAaPct`, `percentualCobertoPct`
- [ ] Resposta de `GET /api/v1/cotacoes/{id}` inclui campos quando modalidade FGI
- [ ] Swagger reflete campos novos

**Verificação:**
- [ ] Bruno collection executa fluxo manual completo FGI
- [ ] E2E `tests/Sgcf.Api.IntegrationTests/Cotacoes/CotacaoFgiE2ETests.cs` cobre: criar → adicionar banco → registrar proposta → aceitar → converter

**Dependências:** Tasks 3.1, 3.2, 3.3
**Escopo:** M

---

#### Checkpoint C — CRUD ponta a ponta

- [ ] Fluxo completo de cotação FGI via API funciona
- [ ] Suite E2E verde
- [ ] Revisão humana do CET FGI antes de prosseguir para Golden Dataset

---

### Fase 4: Golden Dataset

#### Task 4.1 — Cenário FGI bullet 12m

**Descrição:** Adicionar JSON em `tests/Sgcf.GoldenDataset/data/cotacoes/` com cenário "FGI Caixa R$ 500k, 12m, taxa 12% a.a., TarifaFgiAa 0,5%, PercentualCoberto 80%". CET esperado calculado a mão e validado com o time financeiro.

**Critérios de aceite:**
- [ ] Arquivo `cotacao-fgi-bullet-12m.json` em `tests/Sgcf.GoldenDataset/data/cotacoes/`
- [ ] `expectedOutput.cetAaPct` com 6 casas decimais
- [ ] Comentário no JSON explicando a derivação (fluxo: principal saída em t=0, IOF em t=0, juros+principal em t=360, tarifa FGI em t=360)

**Verificação:**
- [ ] `dotnet test tests/Sgcf.GoldenDataset/Sgcf.GoldenDataset.csproj` verde
- [ ] Time financeiro assina o esperado (sign-off obrigatório por CLAUDE.md)

**Dependências:** Task 1.2
**Escopo:** S

---

### Fase 5: Documentação

#### Task 5.1 — Atualizar `docs/specs/cotacoes/SPEC.md`

**Critérios de aceite:**
- [ ] §11.2 atualizado: remove FGI da lista de "out of scope"
- [ ] §3.3 (invariantes de Proposta) ganha regra "FGI: TaxaFgiAa e PercentualCoberto obrigatórios"
- [ ] §5.1 (CET) ganha sub-seção "5.1.1. CET FGI" descrevendo fluxo adicional da tarifa
- [ ] Glossário (§2) ganha entrada "FGI (modalidade)" distinta de "FGI (garantia)"

**Escopo:** S

---

#### Task 5.2 — Atualizar `docs/api/cotacoes.md`

**Critérios de aceite:**
- [ ] Schema de `Proposta` ganha 3 campos novos
- [ ] Exemplo POST cotação FGI
- [ ] Tabela de regras condicionais (campos FGI obrigatórios sse modalidade FGI)

**Escopo:** S

---

#### Task 5.3 — Bruno collection

**Critérios de aceite:**
- [ ] Requests novos em `docs/api/collections/sgcf-api/.../Cotacoes/`:
    - `POST cotacao FGI`
    - `POST proposta FGI`
    - `POST converter em contrato FGI`
- [ ] Variáveis de ambiente atualizadas se necessário

**Escopo:** S

---

#### Task 5.4 — CHANGELOG

**Critérios de aceite:**
- [ ] Seção nova `[0.7.0] — 2026-MM-DD` (ou versão acordada com o plano consolidado)
- [ ] Bloco `ADDITIVE — Cotações — Suporte a modalidade FGI` documenta:
    - Campos novos em `Proposta`
    - Tarifa FGI incorporada ao CET
    - Migration `S6_PropostaFgi`
    - Cenário Golden Dataset adicionado

**Escopo:** XS

---

#### Checkpoint Final

- [ ] `dotnet test` (suite completa) verde
- [ ] Build limpo, sem warnings novos
- [ ] Golden Dataset assinado pelo time financeiro
- [ ] Documentação revisada
- [ ] Bruno collection valida fluxo manual completo
- [ ] PR pronto para review

---

## 5. Riscos e Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Confusão FGI-modalidade × FGI-garantia leva a implementação no agregado errado | Alta | Alto | Glossário explícito (§1.2 deste plano + §2 do SPEC); revisão humana obrigatória ao final da Fase 1 (Checkpoint A); nomes de teste prefixados (`FgiModalidade*` vs `GarantiaFgi*`) |
| Fórmula de tarifa FGI na `CalculadoraCet` diverge da `GerarCronogramaCommand.AdicionarTarifaFgiAsync` | Média | Alto | Espelhar a fórmula linha-a-linha; teste de regressão compara CET-cotação contra CET-contrato com mesmos inputs; refatorar para helper compartilhado em fase futura |
| Estruturas Price/SAC com FGI não suportadas (tarifa anual sobre saldo médio) | Média | Médio | Documentar limitação no SPEC (FGI MVP suporta apenas bullet, consistente com `GerarCronogramaCommand` atual); rejeitar combinações inválidas em `RegistrarPropostaCommand` (`EstruturaAmortizacao != Bullet && Modalidade == Fgi` → 400) |
| Sobreposição com Capital de Giro quando contrato tem FGI como garantia | Média | Baixo | Documentação explícita; teste de regressão garante que cotação `CapitalDeGiro` com `GarantiaExigidaLimite` tipo `Fgi` continua usando o fluxo de garantia, não o fluxo deste plano |
| `PercentualCoberto` interpretado como redução de custo (entra no CET) por engano | Média | Médio | AD-6 explícito; teste de regressão garante que alterar `PercentualCoberto` mantendo `TaxaFgi` constante NÃO altera o CET |
| Cadastro de PTAX continua sendo exigido para FGI por descuido | Baixa | Baixo | Helper compartilhado com NCE/CapitalDeGiro (Task 3.1); teste E2E sem PTAX cadastrada |
| Migration aditiva conflita com migrations paralelas (REFINIMP, Lei4131, NCE, BalcãoCaixa) | Média | Baixo | Sequenciar numeração (`S6`...) após plano mestre consolidado; coordenar em um único PR de migração se possível |

---

## 6. Perguntas em Aberto

1. **Tarifa FGI anual sobre saldo devedor para estruturas Price/SAC:** o cronograma atual (`GerarCronogramaCommand.AdicionarTarifaFgiAsync`) calcula a tarifa como evento único `principal × taxa × prazo / 360`. Para FGI multi-parcelas (raro em Caixa/BV mas existe), a tarifa deveria ser cobrada anualmente sobre o saldo médio? **Default proposto:** restringir MVP a FGI bullet; rejeitar Price/SAC com FGI em validador.
2. **`PercentualCoberto` máximo:** existe limite regulatório (ex.: BNDES exige cobertura ≤ 80%)? Validar como invariante de domínio ou aceitar qualquer valor em (0, 100]? **Default:** aceitar (0, 100]; banco controla via cadastro.
3. **Linhas FGI por subprograma:** o FGI tem subprogramas (PEAC, NOVO_EMPREENDEDOR — já modelados em `GarantiaFgiDetail.TipoFgi`). Devemos espelhar `TipoFgi` na `Proposta`/`FgiDetail` de modalidade? **Default:** não no MVP; `NumeroOperacaoFgi` carrega contexto suficiente.
4. **Auditoria da tarifa FGI:** alterações de `TaxaFgiAa` em proposta aceita devem ser bloqueadas (snapshot imutável) ou apenas auditadas via `audit_log` existente? **Default:** mesma regra das demais alterações de proposta (auditadas, não bloqueadas até aceitação).
5. **Banco intermediário (Caixa/BV/BB):** o FGI roda via banco intermediário operacionalizando o fundo. Faz sentido capturar `BancoIntermediario` (como `GarantiaFgiDetail` faz) na proposta? **Default:** não no MVP; o `BancoId` da Proposta já cumpre o papel.
6. **Convergência com plano de Capital de Giro:** o plano de Capital de Giro (irmão deste, em `tasks/cotacoes-modalidades/capital-de-giro/`) precisa coordenar a regra "BRL dispensa PTAX". Quem implementa primeiro extrai o helper compartilhado? **Default:** quem chegar primeiro implementa o helper; o segundo apenas refatora se necessário.
7. **Versionamento do CHANGELOG:** este plano consome a próxima versão minor; o plano consolidado (task #16) define a numeração final.

---

## 7. Paralelização

- **Sequencial obrigatório:** Tasks 1.1 → 1.2 → 2.1 → 2.2 → 3.2 → 3.3 → 4.1
- **Paralelo possível:** Task 3.1 pode rodar em paralelo com 1.2 (depende apenas de 1.1)
- **Paralelo possível:** Tasks 5.1, 5.2, 5.3, 5.4 (documentação) após Checkpoint C
- **Pré-requisito externo:** Confirmação humana de AD-4 e AD-5 (regra do CET com tarifa FGI) antes de iniciar Task 1.2

---

## 8. Sumário Quantitativo

- **5 fases**, **13 tasks**, **4 checkpoints** (A, B, C, Final)
- **Caminho crítico:** 1.1 → 1.2 → 2.1 → 2.2 → 3.2 → 3.3 → 3.4 (7 tasks)
- **Escopo total:** ~5 M, ~7 S, ~1 XS (escopo dominante: domínio + CET + integração)
- **Relação com plano de Capital de Giro:** ambos compartilham a regra "modalidade BRL dispensa PTAX" (Task 3.1 deste plano); coordenar para extrair helper compartilhado. FGI **como garantia** em contratos Capital de Giro segue o caminho do plano `tasks/garantias-em-limites/` (v0.6.0) — fora do escopo deste plano.
