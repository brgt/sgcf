# Plano de Implementação — Cotações de NCE (Nota de Crédito à Exportação)

**Status:** Pendente de aprovação humana
**Autor:** Planning agent (read-only mode)
**Data:** 2026-05-18
**Dependências externas:** Nenhuma (módulo de Cotações FINIMP já mergeado em `main`; `NceDetail` já existe em Contratos)

---

## 1. Contexto

O módulo de Cotações (`docs/specs/cotacoes/SPEC.md`) entrega no MVP apenas a modalidade FINIMP. A SPEC §11.2 lista explicitamente NCE como "fora do MVP, para sprints subsequentes, reutilizando estrutura". Este plano operacionaliza essa extensão.

NCE (Nota de Crédito à Exportação) é uma operação de crédito **doméstica em BRL**, lastreada em recebíveis de exportação futura. Características que a distinguem de FINIMP (registradas no header de `src/Sgcf.Domain/Contratos/NceDetail.cs:6-13` e na validação NCE em `src/Sgcf.Application/Contratos/Commands/CreateContratoCommand.cs:137-142` e `src/Sgcf.Application/Contratos/Commands/GerarCronogramaCommand.cs:75-86`):

- **Moeda:** sempre BRL. Não há conversão cambial, portanto PTAX é irrelevante para o cálculo do CET.
- **Tributação:** **isenta de IRRF** e **sem IOF câmbio** (a operação não é de câmbio). Mantém-se a possibilidade de IOF crédito ordinário (alíquota de operação de crédito interna), mas o campo `Proposta.IofPercentual` já modela isso de forma genérica.
- **NDF:** nunca aplicável. NCE em BRL não tem hedge cambial associado.
- **Periodicidade de juros:** comumente paga em frequência configurável (mensal, bimestral, trimestral, semestral, anual ou bullet no vencimento). FINIMP do MVP é apenas Bullet. `Proposta.PeriodicidadeJuros` já existe e aceita o enum `Periodicidade` completo — basta validar coerência.
- **Estrutura:** geralmente Bullet (juros periódicos durante o prazo + principal no vencimento) ou Price.
- **Garantias típicas:** Aval dos sócios + duplicatas de exportação ou cessão fiduciária de recebíveis. CDB cativo é raro mas possível. A modelagem atual de `Proposta.GarantiaExigida`/`ValorGarantiaExigidaBrl`/`GarantiaEhCdbCativo` já cobre.

`NceDetail` já existe (`src/Sgcf.Domain/Contratos/NceDetail.cs`) com três campos: `NceNumero` (string?), `DataEmissao` (LocalDate?), `BancoMandatario` (string?). O fluxo de criação de Contrato via `CreateContratoCommand` já popula NceDetail em `src/Sgcf.Application/Contratos/Commands/CreateContratoCommand.cs:310-324`. O que falta é **conectar a conversão Cotação → Contrato** a esse path e **adaptar a entrada da cotação para não exigir PTAX nem aceitar NDF**.

Bloqueio chave para o MVP de Cotações expandir para NCE está em quatro pontos:

1. **`CriarCotacaoCommandHandler`** (`src/Sgcf.Application/Cotacoes/Commands/CriarCotacaoCommand.cs:54-67`) sempre busca PTAX D-1 e a injeta em `Cotacao.Criar(..., ptaxUsadaUsdBrl: ptax, ...)`. Para NCE, PTAX é semanticamente irrelevante; exigi-la como pré-cadastro causa fricção operacional indevida.
2. **`RegistrarPropostaCommand`** (`src/Sgcf.Application/Cotacoes/Commands/RegistrarPropostaCommand.cs`) aceita propostas em qualquer moeda mas não rejeita explicitamente NDF=true para NCE. Sem essa validação, é possível registrar proposta semanticamente errada (NCE com NDF). A validação de moeda BRL para NCE também não existe.
3. **`ConverterEmContratoCommand`** (`src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs:98-113`) cria `FinimpDetail` no branch `if (cotacao.Modalidade == ModalidadeContrato.Finimp)` mas não tem branch equivalente para NCE. Campos NCE específicos (`NceNumero`, `DataEmissao`, `BancoMandatario`) não trafegam no command. Hoje, converter cotação NCE produz Contrato órfão (sem `NceDetail`).
4. **`CalculadoraCet`** (`src/Sgcf.Domain/Cotacoes/CalculadoraCet.cs:33-82`) trata moeda BRL corretamente em `ConverterParaBrl` (linha 88-91: `if (valor.Moeda == Moeda.Brl) return valor;`) mas o handler de `RegistrarPropostaCommand` chama `ObterPtaxEfetivaAsync` que retorna `1m` para BRL (linha 136-139). Aparentemente já funciona para BRL, mas o flow depende do PTAX da cotação existir. Validar end-to-end.

Casos de uso reais que o plano deve viabilizar:

- Operador captura proposta NCE de R$ 5 M, prazo 360 dias, taxa CDI+3% a.a., **juros trimestrais** e principal bullet, garantia Aval + duplicatas — sem precisar cadastrar PTAX.
- Comparação entre duas propostas NCE de bancos distintos com periodicidades de juros diferentes (mensal vs. trimestral) usando CET equalizado.
- Conversão de proposta NCE aceita em Contrato com `NceDetail` populado e atualização correta de `LimiteBanco` (modalidade NCE).

---

## 2. Decisões Arquiteturais

| #     | Decisão | Rationale |
|-------|---------|-----------|
| AD-1  | **PTAX é opcional na cotação** quando `Modalidade ∈ { Nce, CapitalDeGiro, Fgi }`. Para modalidades USD-dependentes (Finimp, Lei4131), permanece obrigatória. | Forçar PTAX para operações BRL-puras é semanticamente errado e cria fricção operacional. O domínio (`Cotacao.PtaxUsadaUsdBrl`) atualmente é `decimal` não-anulável; ver AD-2. |
| AD-2  | **Modelar `Cotacao.PtaxUsadaUsdBrl` como `decimal?` (nullable)**. Para NCE, valor é `null`. Para FINIMP/Lei4131, mantém-se obrigatório via validação no handler de criação. | Alternativa rejeitada: usar PTAX=1m sentinel. Sentinels confundem leitura, mascaram bugs de cálculo e induzem o `CalculadoraCet` a tratar BRL como se fosse USD com câmbio 1:1. Modelar a opcionalidade no tipo é honesto e auditável. Migration de schema necessária. |
| AD-3  | **`RegistrarPropostaCommand` rejeita combinações inválidas por modalidade**: NCE com `MoedaOriginal != BRL` → 400; NCE com `ExigeNdf = true` → 400. | Defesa em profundidade: validação adicional no handler/validator antes do agregado. Mensagens citam SPEC §11.2 e características da modalidade. |
| AD-4  | **`CalculadoraCet` não recebe novo branch específico para NCE**. O caminho BRL existente já funciona (PTAX irrelevante; `ConverterParaBrl` retorna `valor` quando `Moeda.Brl`). | Evitar dois caminhos é mais simples e reduz risco de regressão. Mas: se PTAX virar nullable, a interface `CalcularCet(Proposta, decimal ptaxUsdBrl, ...)` precisa aceitar `decimal? ptaxUsdBrl` ou ter overload. Decisão: **overload** — preserva chamada FINIMP intacta e adiciona variante para BRL puro. |
| AD-5  | **Periodicidade de juros configurável na proposta NCE** é exposta sem mudança de modelo. `Proposta.PeriodicidadeJuros` já é `Periodicidade` (enum completo). Validar no handler: NCE aceita `Mensal`, `Bimestral`, `Trimestral`, `Semestral`, `Anual`, `Bullet`. | A estrutura existe; o que falta é a UX/regra explícita de que NCE não é só Bullet. Documentar. |
| AD-6  | **`ConverterEmContratoCommand` ganha campos opcionais NCE**: `NceNumero`, `DataEmissao`, `BancoMandatario`. Quando `cotacao.Modalidade == Nce`, handler cria `NceDetail` via `NceDetail.Criar(...)`. | Espelha o padrão atual de FINIMP (cf. linhas 98-113 do command). Mantém o command como o único ponto de criação de `*Detail` na conversão. |
| AD-7  | **`IContratoRepository.AddNceDetail` já existe** (usado em `CreateContratoCommand.cs:323`). Reutilizar; não criar novo repo method. | Verificado em `src/Sgcf.Application/Contratos/IContratoRepository.cs` (assumido — confirmar na Task 3.3). |
| AD-8  | **Validação cruzada de CET de NCE**: garantir que o cálculo não tenta inferir IRRF/IOF câmbio (que não se aplicam). Como `CalculadoraCet` hoje não modela IRRF nem IOF câmbio diretamente (apenas `IofPercentual` sobre principal — IOF crédito), nada a ajustar; documentar em SPEC. | Risco: futuros refactors do `CalculadoraCet` podem introduzir IRRF — proteger com property test (CET de NCE não muda quando alíquota IRRF varia, se vier a existir). |
| AD-9  | **Não criar nova migration de domínio para NCE** — `NceDetail` já está mapeado (Onda anterior). Migration de Cotação é necessária apenas para tornar `ptax_usada_usd_brl` nullable. | Migration única, aditiva, sem perda de dados (NULL não cria conflito com decimals existentes). |
| AD-10 | **API**: reutilizar `POST /api/v1/cotacoes` com `modalidade = "Nce"`. Endpoint `POST /api/v1/cotacoes/{id}/converter-em-contrato` ganha campos opcionais NCE no payload. | Sem mudanças no roteamento; apenas no contrato JSON. |
| AD-11 | **LimiteBanco para NCE** é cadastro independente já suportado: `LimiteBanco` tem `Modalidade: ModalidadeContrato` (enum inclui `Nce`). Nenhuma mudança de schema. Bruno collection precisa request específico de exemplo. | Verificado em `src/Sgcf.Domain/Cotacoes/LimiteBanco.cs` (assumido — confirmar na Task 1.3). |

---

## 3. Grafo de Dependências

```
Domínio:
  Cotacao.PtaxUsadaUsdBrl (decimal → decimal?)
      │
      └── Migration S6_NcePtaxOpcional (ALTER COLUMN nullable)
              │
              └── CotacaoConfiguration (IsRequired(false))
                      │
                      ├── CalculadoraCet overload (decimal? ptax)
                      │       └── Tests: CET de NCE BRL sem PTAX
                      │
                      └── CriarCotacaoCommand (PTAX condicional por modalidade)
                              │
                              ├── RegistrarPropostaCommand
                              │       ├── Validação: NCE ⇒ MoedaOriginal=BRL
                              │       ├── Validação: NCE ⇒ !ExigeNdf
                              │       └── ObterPtaxEfetivaAsync (BRL → 1m já OK)
                              │
                              └── ConverterEmContratoCommand
                                      ├── Campos NCE: NceNumero, DataEmissao, BancoMandatario
                                      ├── Branch: if Nce → criar NceDetail
                                      └── AddNceDetail (reutiliza repo method)

API:
  CotacoesController (POST /cotacoes, POST /converter-em-contrato): payload estendido
  LimitesBancoController: já aceita modalidade=Nce (sem mudança)

Bruno collection:
  06-Cotacoes/: novo "Criar cotação NCE", "Registrar proposta NCE", "Converter em contrato NCE"
  11-LimitesBanco/: novo "Criar limite NCE" como exemplo

Golden dataset:
  tests/Sgcf.GoldenDataset/data/: novo cenário NCE-BRL-trimestral.json

Documentação:
  docs/specs/cotacoes/SPEC.md: §11.2 atualizada; nova §19 NCE
  docs/api/cotacoes.md: exemplos NCE
  docs/changelog/CHANGELOG.md: v0.7.0 ADDITIVE
```

---

## 4. Fases e Tarefas

### Fase 1 — Domínio + Persistência

#### Task 1.1 — Tornar `Cotacao.PtaxUsadaUsdBrl` nullable

**Descrição:** Alterar tipo de `Cotacao.PtaxUsadaUsdBrl` de `decimal` para `decimal?`. Ajustar factory `Cotacao.Criar(...)` para aceitar `decimal? ptaxUsadaUsdBrl`. Invariante: PTAX é obrigatória se `Modalidade ∈ { Finimp, Lei4131, Refinimp }`; opcional caso contrário.

**Critérios de aceite:**
- [ ] `Cotacao.PtaxUsadaUsdBrl: decimal?` em `src/Sgcf.Domain/Cotacoes/Cotacao.cs`
- [ ] `Cotacao.Criar` aceita `decimal? ptaxUsadaUsdBrl`
- [ ] Invariante de criação rejeita PTAX nula para modalidades cambiais (`InvalidOperationException` com mensagem orientativa)
- [ ] Todos os pontos de uso em domínio compilam (Conversão, Proposta, EconomiaNegociacao)

**Verificação:**
- [ ] `tests/Sgcf.Domain.Tests/Cotacoes/CotacaoTests.cs`: novos testes — criar NCE sem PTAX (ok), criar FINIMP sem PTAX (rejeita)
- [ ] Suite de domínio existente continua verde

**Dependências:** nenhuma

**Arquivos prováveis:**
- `src/Sgcf.Domain/Cotacoes/Cotacao.cs`
- `tests/Sgcf.Domain.Tests/Cotacoes/CotacaoTests.cs`

**Escopo:** M

---

#### Task 1.2 — Overload de `CalculadoraCet.CalcularCet` para PTAX opcional

**Descrição:** Adicionar overload `CalcularCet(Proposta proposta, decimal? ptaxUsdBrl, LocalDate dataDesembolso, decimal? taxaAaPercentualOverride = null)` que delega: se moeda da proposta é BRL e PTAX é nula, usa `1m` internamente; se moeda é não-BRL e PTAX é nula, lança `ArgumentException`.

**Critérios de aceite:**
- [ ] Overload novo em `src/Sgcf.Domain/Cotacoes/CalculadoraCet.cs`
- [ ] Overload original (com `decimal ptaxUsdBrl`) preservado, sem mudança de comportamento
- [ ] Lança `ArgumentException` com mensagem clara quando proposta é não-BRL e PTAX é nula

**Verificação:**
- [ ] `tests/Sgcf.Domain.Tests/Cotacoes/CalculadoraCetTests.cs`: cenário NCE BRL com PTAX null → CET correto; cenário NCE com USD + PTAX null → rejeita
- [ ] Property test: CET de proposta BRL é o mesmo com PTAX=null e PTAX=1m

**Dependências:** Task 1.1

**Arquivos prováveis:**
- `src/Sgcf.Domain/Cotacoes/CalculadoraCet.cs`
- `tests/Sgcf.Domain.Tests/Cotacoes/CalculadoraCetTests.cs`

**Escopo:** S

---

#### Task 1.3 — Migration `S6_NcePtaxOpcional`

**Descrição:** Migration EF Core que altera `sgcf.cotacao.ptax_usada_usd_brl` para `NULL` permitido. Atualiza `CotacaoConfiguration` para `IsRequired(false)` ou `.HasColumnType("numeric")` sem `NOT NULL`.

**Critérios de aceite:**
- [ ] Migration gerada via `dotnet ef migrations add S6_NcePtaxOpcional --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api`
- [ ] Up: `ALTER COLUMN ptax_usada_usd_brl DROP NOT NULL`
- [ ] Down: `ALTER COLUMN ptax_usada_usd_brl SET NOT NULL` (precisa que não existam linhas com NULL; rollback bloqueia se NCE já cadastrada — documentar)
- [ ] `CotacaoConfiguration` atualizada

**Verificação:**
- [ ] `dotnet ef database update` aplica em banco de dev sem perda de dados
- [ ] `dotnet ef migrations remove` reverte limpo em banco vazio

**Dependências:** Task 1.1

**Arquivos prováveis:**
- `src/Sgcf.Infrastructure/Migrations/2026xxxx_S6_NcePtaxOpcional.cs`
- `src/Sgcf.Infrastructure/Migrations/2026xxxx_S6_NcePtaxOpcional.Designer.cs`
- `src/Sgcf.Infrastructure/Migrations/SgcfDbContextModelSnapshot.cs`
- `src/Sgcf.Infrastructure/Persistence/Configurations/CotacaoConfiguration.cs`

**Escopo:** S

---

#### Checkpoint A — Domínio + Persistência

- [ ] `dotnet build` limpo
- [ ] Testes de Domain.Tests passam
- [ ] Migration aplica e reverte (banco vazio); aplica sem perda em banco com dados FINIMP
- [ ] Revisão humana das invariantes (AD-1, AD-2, AD-4) antes de avançar para Application

---

### Fase 2 — Application

#### Task 2.1 — `CriarCotacaoCommand` aceita NCE sem PTAX

**Descrição:** Refatorar `CriarCotacaoCommandHandler.Handle` para tornar a busca de PTAX condicional. Pseudocódigo: `if (modalidade is Nce or CapitalDeGiro or Fgi) { ptax = null; dataPtaxReferencia = dataAbertura; } else { /* fluxo atual */ }`. Passar valores para `Cotacao.Criar`.

**Critérios de aceite:**
- [ ] Para modalidades cambiais (Finimp, Lei4131, Refinimp): comportamento inalterado, PTAX obrigatória
- [ ] Para NCE/CapitalDeGiro/Fgi: PTAX não é buscada; cotação criada com `PtaxUsadaUsdBrl=null` e `DataPtaxReferencia=DataAbertura` (ou outro placeholder validado)
- [ ] Validator `CriarCotacaoCommandValidator` inalterado (não há campo PTAX no command de entrada)

**Verificação:**
- [ ] Teste unitário do handler: criar NCE sem PTAX cadastrada → sucesso; criar FINIMP sem PTAX cadastrada → `InvalidOperationException`
- [ ] Suite atual continua verde

**Dependências:** Tasks 1.1, 1.3

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/CriarCotacaoCommand.cs`
- `tests/Sgcf.Application.Tests/Cotacoes/CriarCotacaoCommandHandlerTests.cs` (ou similar)

**Escopo:** M

---

#### Task 2.2 — `RegistrarPropostaCommand` valida regras NCE

**Descrição:** Adicionar validações ao `RegistrarPropostaCommandValidator` que olham para a `Modalidade` da cotação parent (precisa carregar do repo ou ser passada como contexto). Alternativa: validar no handler antes de `cotacao.AdicionarProposta(...)`. Decisão: **validar no handler** (acesso ao agregado já carregado), em estilo "guard early".

**Critérios de aceite:**
- [ ] Quando `cotacao.Modalidade == Nce`:
  - `MoedaOriginal != "Brl"` → `ArgumentException` com mensagem: "Proposta NCE deve ser em BRL."
  - `ExigeNdf == true` → `ArgumentException` com mensagem: "Proposta NCE não aceita NDF — operação em BRL sem exposição cambial."
- [ ] Mensagens citam SPEC e modalidade
- [ ] `ObterPtaxEfetivaAsync` com `Moeda.Brl` continua retornando `1m` (sem mudança)

**Verificação:**
- [ ] Testes E2E: registrar proposta NCE em USD → 400 com mensagem clara; registrar proposta NCE com NDF → 400
- [ ] Teste positivo: proposta NCE BRL sem NDF → 201

**Dependências:** Tasks 1.1, 2.1

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/RegistrarPropostaCommand.cs`
- `tests/Sgcf.Api.IntegrationTests/Cotacoes/RegistrarPropostaNceTests.cs` (novo)

**Escopo:** S

---

#### Task 2.3 — `ConverterEmContratoCommand` cria `NceDetail`

**Descrição:** Estender `ConverterEmContratoCommand` record com 3 novos campos opcionais (`NceNumero: string?`, `DataEmissao: DateOnly?`, `BancoMandatario: string?`). No handler, adicionar branch espelhando o de FINIMP em `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs:98-113`:

```
NceDetail? nceDetail = null;
if (cotacao.Modalidade == ModalidadeContrato.Nce)
{
    LocalDate? dataEmissaoNce = cmd.DataEmissao.HasValue
        ? new LocalDate(cmd.DataEmissao.Value.Year, cmd.DataEmissao.Value.Month, cmd.DataEmissao.Value.Day)
        : (LocalDate?)null;
    nceDetail = NceDetail.Criar(contrato.Id, cmd.NceNumero, dataEmissaoNce, cmd.BancoMandatario, clock);
    contratoRepo.AddNceDetail(nceDetail);
}
```

Atualizar `ContratoDto.From` (se necessário) para incluir `NceDetail` no retorno.

**Critérios de aceite:**
- [ ] Command record aceita campos NCE opcionais; FINIMP fields permanecem intactos
- [ ] Handler cria `NceDetail` apenas quando modalidade é NCE
- [ ] `ContratoDto` retornado contém `nceDetail` populado quando aplicável
- [ ] Validator `ConverterEmContratoCommandValidator`: campos NCE são opcionais (sem `NotEmpty`)
- [ ] Atualização de `LimiteBanco` continua funcionando para modalidade NCE (já é genérico via `cotacao.Modalidade`)

**Verificação:**
- [ ] Teste E2E: cotação NCE aceita → POST converter-em-contrato com `nceNumero`, `dataEmissao`, `bancoMandatario` → 200; verifica contrato persistido com `NceDetail` correto
- [ ] Teste E2E: cotação NCE convertida sem campos NCE no payload → 200 com `NceDetail` com todos os campos null (cf. comportamento atual em `CreateContratoCommand.cs:313`)
- [ ] Suite FINIMP existente verde

**Dependências:** Tasks 1.1, 2.2

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs`
- `src/Sgcf.Application/Contratos/ContratoDto.cs` (possível ajuste)
- `tests/Sgcf.Api.IntegrationTests/Cotacoes/ConverterEmContratoNceTests.cs` (novo)

**Escopo:** M

---

#### Task 2.4 — Verificar `IContratoRepository.AddNceDetail` exposto

**Descrição:** Confirmar que o método `AddNceDetail` está exposto na interface `IContratoRepository` (já usado em `CreateContratoCommand`). Se não estiver na interface (apenas concrete repo), elevar.

**Critérios de aceite:**
- [ ] Interface `IContratoRepository` declara `void AddNceDetail(NceDetail detail)`
- [ ] Implementação `ContratoRepository.AddNceDetail` mantida

**Verificação:**
- [ ] `dotnet build` limpo
- [ ] Caller em `ConverterEmContratoCommand` compila sem cast

**Dependências:** nenhuma (paralelizável)

**Arquivos prováveis:**
- `src/Sgcf.Application/Contratos/IContratoRepository.cs`
- `src/Sgcf.Infrastructure/Persistence/Repositories/ContratoRepository.cs` (eventual ajuste)

**Escopo:** XS

---

#### Checkpoint B — Application

- [ ] Suite Application.Tests + IntegrationTests verde
- [ ] Bruno collection rodada manual: criar NCE → adicionar banco → registrar proposta → aceitar → converter → contrato com `NceDetail` no GET
- [ ] CET de cotação NCE bate com cálculo manual (planilha) para o cenário golden trimestral

---

### Fase 3 — API + Bruno collection

#### Task 3.1 — Endpoint `POST /api/v1/cotacoes/{id}/converter-em-contrato` aceita campos NCE

**Descrição:** Atualizar `CotacoesController` para mapear payload JSON com `nceNumero`, `dataEmissao`, `bancoMandatario` para o command. Manter compatibilidade com FINIMP (campos atuais).

**Critérios de aceite:**
- [ ] Request body do endpoint aceita campos novos como opcionais
- [ ] Resposta inclui `nceDetail` quando aplicável
- [ ] OpenAPI/Swagger reflete os novos campos

**Verificação:**
- [ ] Teste E2E (WebApplicationFactory) verifica round-trip JSON
- [ ] Swagger renderiza sem erro

**Dependências:** Task 2.3

**Arquivos prováveis:**
- `src/Sgcf.Api/Controllers/CotacoesController.cs`
- `tests/Sgcf.Api.IntegrationTests/Cotacoes/CotacoesControllerNceTests.cs`

**Escopo:** S

---

#### Task 3.2 — Bruno collection: requests NCE

**Descrição:** Criar 4 requests Bruno em `docs/api/collections/sgcf-api/06-Cotacoes/` (ou pasta equivalente já existente):

1. `criar-cotacao-nce.bru` — payload com `modalidade: "Nce"`, sem PTAX cadastrada (cenário positivo)
2. `registrar-proposta-nce.bru` — proposta BRL, taxa CDI+3%, periodicidade trimestral, sem NDF, garantia Aval
3. `comparar-propostas-nce.bru` — GET comparativo
4. `converter-em-contrato-nce.bru` — POST com `nceNumero`, `dataEmissao`, `bancoMandatario`

Também: 1 request em `11-LimitesBanco/criar-limite-nce.bru` para documentar o cadastro de linha NCE.

**Critérios de aceite:**
- [ ] Variáveis de ambiente (`{{bancoId}}`, `{{cotacaoId}}`) usam o padrão já vigente
- [ ] Cada request tem comentário explicando o passo no fluxo
- [ ] Sequência ordenada permite rodar de ponta a ponta manualmente

**Verificação:**
- [ ] Operador (humano) executa a sequência em ambiente dev e cota termina como Contrato NCE com `NceDetail` populado

**Dependências:** Task 3.1

**Arquivos prováveis:**
- `docs/api/collections/sgcf-api/06-Cotacoes/*.bru`
- `docs/api/collections/sgcf-api/11-LimitesBanco/criar-limite-nce.bru`

**Escopo:** S

---

#### Checkpoint C — API + Bruno

- [ ] Fluxo manual ponta a ponta NCE no Bruno → verde
- [ ] Swagger renderiza endpoints com payload atualizado
- [ ] Nenhuma regressão em testes FINIMP

---

### Fase 4 — Golden Dataset + Tests adicionais

#### Task 4.1 — Cenário golden NCE BRL com juros trimestrais

**Descrição:** Adicionar arquivo `tests/Sgcf.GoldenDataset/data/cotacoes/nce-brl-juros-trimestrais.json` com `input` (proposta NCE BRL 5M, taxa 12% a.a., spread 0%, IOF 0,38%, prazo 360d, juros trimestrais, garantia Aval — `valorGarantia=0`) e `expectedOutput` (CET calculado manualmente + valor total estimado em BRL).

**Critérios de aceite:**
- [ ] Arquivo JSON com formato consistente com cenários FINIMP existentes
- [ ] Valores `expectedOutput` validados via planilha por usuário humano (PO)
- [ ] Teste `[Theory]` em `tests/Sgcf.GoldenDataset/CotacaoGoldenTests.cs` (ou similar) carrega o arquivo e compara campo a campo

**Verificação:**
- [ ] `dotnet test tests/Sgcf.GoldenDataset --filter "FullyQualifiedName~NceBrl"` verde
- [ ] Tolerância de comparação ≤ 0,01% absoluto sobre CET

**Dependências:** Task 2.2 (CET de NCE estável)

**Arquivos prováveis:**
- `tests/Sgcf.GoldenDataset/data/cotacoes/nce-brl-juros-trimestrais.json`
- `tests/Sgcf.GoldenDataset/CotacaoGoldenTests.cs` (estende)

**Escopo:** M (depende fortemente de validação humana dos valores esperados)

---

#### Task 4.2 — Property tests específicos de NCE

**Descrição:** Adicionar properties em FsCheck que valem para NCE BRL:

1. CET de proposta NCE não muda quando passamos PTAX qualquer valor (já que moeda é BRL).
2. CET de proposta NCE com NDF=false e MoedaOriginal=BRL é estritamente igual ao CET de FINIMP com mesmos parâmetros e MoedaOriginal=BRL + PTAX=1.
3. Aumentar a `PeriodicidadeJuros` (de Mensal para Anual) com demais parâmetros constantes não pode aumentar o CET além de um delta justificável (juros simples não capitalizam — para Bullet+Bullet, CET ≈ taxa nominal+spread).

**Critérios de aceite:**
- [ ] 3 properties adicionadas em `tests/Sgcf.Domain.Tests/Cotacoes/CalculadoraCetPropertyTests.cs` (estende existente)

**Verificação:**
- [ ] `dotnet test --filter "FullyQualifiedName~Nce"` verde

**Dependências:** Task 1.2

**Escopo:** S

---

#### Checkpoint D — Tests

- [ ] Golden dataset verde
- [ ] Property tests passam em ≥1000 amostras
- [ ] Cobertura de NCE ≥ 80% no caminho Application + Domain

---

### Fase 5 — Documentação

#### Task 5.1 — Atualizar `docs/specs/cotacoes/SPEC.md`

**Critérios de aceite:**
- [ ] §11.2 atualizada: NCE saiu de "out of scope" para "incluído na v0.7.0"
- [ ] Nova §19 "Extensão NCE" descreve diferenças vs. FINIMP (BRL, sem PTAX, sem IRRF, sem IOF câmbio, periodicidade de juros configurável)
- [ ] Tabela de invariantes atualizada referindo a `PtaxUsadaUsdBrl?` (opcional)

**Escopo:** S

---

#### Task 5.2 — Atualizar `docs/api/cotacoes.md`

**Critérios de aceite:**
- [ ] Exemplo de payload `POST /cotacoes` para NCE
- [ ] Exemplo de payload `POST /converter-em-contrato` com campos NCE
- [ ] Tabela de campos opcionais por modalidade

**Escopo:** S

---

#### Task 5.3 — CHANGELOG v0.7.0

**Critérios de aceite:**
- [ ] Bloco `ADDITIVE — Cotações — Modalidade NCE` documenta nova capacidade
- [ ] Bloco `BREAKING-INTERNO — Cotacao.PtaxUsadaUsdBrl agora nullable` documenta refactor (consumers internos do dto precisam tratar null; nenhum impacto API público se Dto também for opcional)
- [ ] Migration S6 documentada

**Escopo:** XS

---

#### Checkpoint Final

- [ ] Toda suite verde: `dotnet test`
- [ ] Build limpo sem novos warnings
- [ ] Bruno collection valida fluxo manual ponta a ponta para NCE
- [ ] Documentação revisada (SPEC + API)
- [ ] CHANGELOG atualizado
- [ ] PR pronto para review

---

## 5. Riscos e Mitigações

| Risco | Impacto | Probabilidade | Mitigação |
|-------|---------|---------------|-----------|
| **Tornar `PtaxUsadaUsdBrl` nullable quebra serializações existentes** (DTOs, snapshot JSON em `EconomiaNegociacao`, audit log) | Alto — pode invalidar snapshots históricos | Média | Inspecionar todos os consumers do campo antes da Task 1.1. Snapshots existentes têm valor numérico; deserializer precisa aceitar null. Adicionar teste de round-trip JSON antigo. |
| **`CalculadoraCet` tem premissas USD escondidas** (ex: anualização 360 dias talvez deveria ser 252 para NCE BRL) | Médio | Média | Auditar `AnualizarTaxaDiaria` (linha 264-269): hoje sempre 360. Para NCE BRL doméstico, regulação local usa 252 dias úteis. Decidir: manter 360 e justificar (consistência com `BaseCalculo.Dias360`), ou parametrizar por modalidade. **Sugestão:** manter 360 no MVP de NCE; adicionar Q em §6 para validar com PO. |
| **Cotação `EconomiaNegociacao` quebra**: o cálculo de `valorPrincipalBrl` em `ConverterEmContratoCommand.cs:150-152` usa `cotacao.PtaxUsadaUsdBrl` — se nullable, branch ternário precisa de fallback. | Alto | Alta | Refatorar: `Money valorPrincipalBrl = propostaAceita.MoedaOriginal == Moeda.Brl ? valorPrincipal : new Money(valorPrincipal.Valor * cotacao.PtaxUsadaUsdBrl!.Value, Moeda.Brl);`. Para NCE, sempre cai no branch BRL. Adicionar guard explícito. |
| **`CalculadoraCet` injeta `1m` como PTAX para NCE no caminho atual e isso pode ter mascarado bug** | Baixo | Baixa | Property test (Task 4.2) confirma que CET NCE BRL é igual com PTAX qualquer. Se quebrar, investigar. |
| **Periodicidade trimestral no motor de amortização**: `Periodicidade.Trimestral` existe no enum mas pode não ter cobertura completa de testes para Bullet+Trimestral | Médio | Média | Verificar `Sgcf.Domain.Cronograma` para estratégia Bullet com periodicidade de juros trimestral. Se houver lacuna, criar issue separada e fixar antes da Task 4.1. |
| **Bruno collection desatualizada** após mudança no payload de `converter-em-contrato` quebra runs FINIMP existentes | Baixo | Média | Manter campos NCE como opcionais. Validar runs FINIMP no Checkpoint C. |
| **Migration S6 não-reversível em produção** se NCE já cadastrada | Médio | Baixa (NCE só após mergeo) | Documentar no header da migration. Considerar adicionar `WHERE` ao down ou aceitar que rollback exige limpar NCE manualmente. |
| **Modalidade `Refinimp` em cotação**: o handler de criação não busca PTAX para Refinimp hoje? Verificar. Se buscar, AD-2 precisa incluir Refinimp explicitamente. | Baixo | Baixa | Validar em `CriarCotacaoCommandHandler` antes de iniciar Task 2.1. |

---

## 6. Perguntas em Aberto

1. **Base de anualização para NCE**: manter 360 (alinhado a `BaseCalculo.Dias360` atual) ou usar 252 (dias úteis, padrão BACEN para BRL)? Default proposto: 360 no MVP, abrir issue para refinamento se PO discordar.
2. **PTAX para `Refinimp`/`CapitalDeGiro`/`Fgi`**: o AD-1 lista Nce/CapitalDeGiro/Fgi como modalidades sem PTAX. Refinimp é cambial (deriva de FINIMP) → PTAX obrigatória. Confirmar.
3. **NCE com IOF crédito**: a alíquota típica de IOF crédito é 0,38% + 0,0041% a.d. O campo `Proposta.IofPercentual` modela um % único sobre principal. Cobre? Ou precisa estender para `IofAdicionalAd`?
4. **Periodicidades aceitas para NCE**: enum `Periodicidade` contém Mensal, Bimestral, Trimestral, Semestral, Anual, Bullet. Aceitar todas? Há restrição prática (ex: bancos não oferecem semestral)?
5. **Validade temporal do snapshot de mercado**: SPEC §13 cita 4h/24h para cotações com NDF/sem NDF. Para NCE (sem PTAX), o conceito de "snapshot de mercado" perde sentido — deve ser 0 (sem validade)? Documentar.
6. **`RefreshCotacaoMercadoCommand` para NCE**: faz sentido permitir refresh em cotação NCE? Default: handler retorna no-op para NCE com aviso informativo.

---

## 7. Paralelização

- **Sequencial obrigatório:**
  - Task 1.1 → 1.2 → 1.3 → 2.1 (mudança no tipo do agregado bloqueia consumers)
  - Task 2.1 → 2.2 → 2.3 (handlers em sequência)
  - Task 2.3 → 3.1 → 3.2 (API depende do handler)
- **Paralelo possível após Checkpoint A:**
  - Task 2.4 (verificar interface) pode iniciar em paralelo com Task 2.1
- **Paralelo após Checkpoint B:**
  - Task 4.1 e Task 4.2 (testes) com Task 3.1 e Task 3.2 (API + Bruno)
- **Paralelo após Checkpoint C:**
  - Tasks 5.1, 5.2, 5.3 (documentação) em paralelo
- **Caminho crítico:** 1.1 → 1.2 → 1.3 → 2.1 → 2.2 → 2.3 → 3.1 → 4.1 → Checkpoint Final (9 tasks)

---

## 8. Sumário Quantitativo

- **5 fases**, **14 tasks**, **5 checkpoints** (A, B, C, D, Final)
- **Escopo total:** 4 M, 7 S, 1 XS, 2 dependentes de validação humana (Task 4.1)
- **Caminho crítico:** 9 tasks (estimativa de 2 a 3 sprints curtas)
- **Mudanças de schema:** 1 migration (S6, aditiva/permissiva)
- **Sem novos endpoints REST** — apenas extensões de payload em endpoints existentes
- **Sem novos agregados de domínio** — `NceDetail` reutilizado; `Cotacao` ajustado
- **Pré-requisitos externos:** nenhum
- **Compatibilidade:** mudança em `Cotacao.PtaxUsadaUsdBrl` é INTERNAL-BREAKING para consumers de domínio; ADDITIVE no contrato JSON da API (PTAX continua presente quando aplicável)
