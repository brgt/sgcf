# Plano de Implementação — Cotações de REFINIMP

**Status:** Pendente de aprovação humana
**Autor:** Planning agent (read-only mode)
**Data:** 2026-05-18
**Dependências externas:** Módulo Cotações FINIMP estável (MVP entregue em v0.5.0); regra BB 70% e máquina de status REFINIMP do módulo Contratos (`CreateContratoCommand.ProcessarRefinimpAsync`) já operacionais.

---

## 1. Contexto

O módulo de Cotações entrega hoje o fluxo proposta → comparação → aceitação → conversão apenas para `ModalidadeContrato.Finimp` (SPEC §11.2 — REFINIMP explicitamente fora do MVP). A entidade `RefinimpDetail` (`src/Sgcf.Domain/Contratos/RefinimpDetail.cs`) e toda a lógica de criação direta de contrato REFINIMP via `CreateContratoCommand.ProcessarRefinimpAsync` (validação de banco `AceitaRefinimp`, walk até ancestral não-REFINIMP, regra 70% BB, cálculo do percentual refinanciado e marcação do contrato mãe como `RefinanciadoParcial`/`RefinanciadoTotal`) já existem. Este plano estende o módulo de Cotações para que uma cotação possa nascer com modalidade REFINIMP, carregar a referência ao contrato mãe desde o rascunho, validar moeda/prazo contra o mãe, e converter a proposta aceita em um contrato REFINIMP completo — reutilizando o pipeline existente sem duplicar regras.

---

## 2. Decisões Arquiteturais

| #   | Decisão                                                                                                                                                                                          | Rationale                                                                                                                                                                                                                                                          |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| AD-1 | `ContratoMaeId` (Guid nullable) passa a ser propriedade do agregado `Cotacao`; obrigatório quando `Modalidade == Refinimp`, nulo nas demais.                                                     | A escolha do contrato mãe é a decisão de negócio que originou a cotação (define moeda, prazo restante, ancestral para a regra 70%). Capturar no rascunho permite validar limite, derivar moeda da proposta e gerar comparativo coerente — adiar deslocaria a regra para o final do fluxo. |
| AD-2 | Reutilizar `ModalidadeContrato` do domínio de Contratos (já contém `Refinimp = 2`); **não** criar enum próprio de Cotação.                                                                       | A SPEC §3.1 já documenta `Cotacao.Modalidade : ModalidadeContrato`. Coerência semântica entre Cotação e Contrato.                                                                                                                                                  |
| AD-3 | `LimiteBanco` para REFINIMP é uma linha **separada** (registro com `Modalidade = Refinimp`), não compartilha o limite de FINIMP.                                                                  | A enum já distingue Finimp/Refinimp; bancos costumam tratar refinanciamento com sublimite próprio. Reaproveita o repositório `GetByBancoModalidadeAsync` sem mudanças.                                                                                              |
| AD-4 | A validação `Banco.AceitaRefinimp` é aplicada em **`AdicionarBancoNaCotacaoCommand`** (não somente na conversão).                                                                                 | Falhar cedo: se o banco não aceita REFINIMP, não faz sentido permitir entrada na cotação. Espelha a posição da validação no `CreateContratoCommand` (linha 207–209).                                                                                                 |
| AD-5 | A regra 70% BB é validada **na conversão**, não na aceitação da proposta.                                                                                                                        | É a regra que define o `ValorPrincipal` final do contrato e exige `valorPrincipalAncestral` calculado em moeda do mãe. Manter consistência com `ProcessarRefinimpAsync` evita duplicação de lógica.                                                                  |
| AD-6 | `ConverterEmContratoCommand` é estendido (não bifurcado) — recebe `RefinimpDetail?` opcional e adiciona o branch `if (cotacao.Modalidade == Refinimp)` análogo ao `if (... == Finimp)` existente. | Single command, single SaveChanges, fluxo único de auditoria/economia/limite. Reduz risco de regressão no caminho FINIMP.                                                                                                                                          |
| AD-7 | O CET para REFINIMP usa a **mesma `CalculadoraCet`** sem modificação.                                                                                                                            | Estrutura de proposta (taxa + spread + IOF + NDF + garantia CDB) é idêntica; REFINIMP é diferenciação documental/contratual, não fórmula. Cenário golden valida.                                                                                                    |
| AD-8 | Moeda da Proposta REFINIMP **deve** coincidir com a moeda do contrato mãe; validação no `RegistrarPropostaCommand`.                                                                              | Espelha a invariante de `ProcessarRefinimpAsync` linhas 374–378. Pegar o erro na proposta evita cascata até conversão.                                                                                                                                              |
| AD-9 | Prazo da Proposta REFINIMP é livre (sem limite imposto pelo restante do mãe) no MVP de REFINIMP.                                                                                                 | A regra "prazo limitado pelo restante do mãe" não está codificada em `ProcessarRefinimpAsync`; introduzi-la aqui seria invenção. Documentar como questão aberta para validação com tesouraria.                                                                      |
| AD-10 | Sem nova migration EF. `Cotacao.ContratoMaeId` será adicionada via migration aditiva nullable.                                                                                                  | Coluna opcional não quebra dados FINIMP existentes; rollback é DROP COLUMN.                                                                                                                                                                                        |
| AD-11 | Marcação do contrato mãe (`MarcarRefinanciadoParcial/Total`) ocorre dentro do `ConverterEmContratoCommand`, reusando `IContratoRepository`.                                                       | Idêntico ao `ProcessarRefinimpAsync`. Mantém invariante: status do mãe só muda após conversão concluída (UoW único).                                                                                                                                                |
| AD-12 | Endpoint `POST /api/v1/cotacoes` ganha campo opcional `contratoMaeId` (não criar endpoint dedicado).                                                                                              | API menor, contrato consistente: `modalidade` discrimina; `contratoMaeId` é obrigatório quando modalidade=Refinimp (validado via FluentValidation com `When(...)`).                                                                                                |

---

## 3. Grafo de Dependências

```
ModalidadeContrato (existe — Refinimp = 2)
RefinimpDetail (existe)
StatusContrato.RefinanciadoParcial/Total (existe)
Contrato.MarcarRefinanciadoParcial/Total (existe)
IContratoRepository.GetAncestraNaoRefinimpAsync (existe)
IContratoRepository.AddRefinimpDetail (existe)
   │
   └─► Sgcf.Domain.Cotacoes.Cotacao (estende: ContratoMaeId nullable + invariante)
           │
           ├─► Migration S6_CotacaoContratoMae (aditiva)
           │      └─► CotacaoConfiguration (mapeia coluna)
           │             └─► CotacaoRepository (sem mudança de query)
           │
           ├─► Sgcf.Application.Cotacoes.Commands.CriarCotacaoCommand
           │      (aceita contratoMaeId, valida obrigatoriedade)
           │
           ├─► Sgcf.Application.Cotacoes.Commands.AdicionarBancoNaCotacaoCommand
           │      (valida Banco.AceitaRefinimp quando Refinimp)
           │
           ├─► Sgcf.Application.Cotacoes.Commands.RegistrarPropostaCommand
           │      (valida moeda da proposta == moeda do contrato mãe)
           │
           └─► Sgcf.Application.Cotacoes.Commands.ConverterEmContratoCommand
                  (branch Refinimp: walk ancestral, regra 70% BB,
                   criar RefinimpDetail, marcar mãe como Refinanciado*)
                  │
                  └─► Reusa IContratoRepository.AddRefinimpDetail,
                            GetAncestraNaoRefinimpAsync,
                            Contrato.MarcarRefinanciado{Parcial,Total}

API:
   POST /api/v1/cotacoes — request DTO ganha contratoMaeId
   POST /api/v1/cotacoes/{id}/converter-em-contrato — sem novos parâmetros
       (RefinimpDetail é derivado da Cotacao.ContratoMaeId + proposta aceita)

Documentação:
   docs/specs/cotacoes/SPEC.md (nova §13/§14 — modalidade REFINIMP)
   docs/api/cotacoes.md (atualiza payloads)
   docs/api/collections/sgcf-api/ (Bruno: novo cenário REFINIMP)
   tests/Sgcf.GoldenDataset/data/ (1 cenário REFINIMP completo)
   docs/changelog/CHANGELOG.md (v0.7.0 ADDITIVE)
```

---

## 4. Fases e Tarefas

### Fase 1 — Domínio

#### Task 1.1 — Adicionar `ContratoMaeId` ao agregado `Cotacao`

**Descrição:** Estender `Cotacao` para carregar opcionalmente o id do contrato mãe; tornar a propriedade obrigatória quando `Modalidade == Refinimp` via invariante na factory `Criar`.

**Critérios de aceite:**
- [ ] Propriedade `Guid? ContratoMaeId { get; private set; }` em `Cotacao`
- [ ] `Cotacao.Criar(...)` recebe `Guid? contratoMaeId = null`
- [ ] Invariante: se `modalidade == Refinimp` e `contratoMaeId` nulo/Empty → `ArgumentException`
- [ ] Invariante: se `modalidade != Refinimp` e `contratoMaeId` informado → `ArgumentException` (defesa contra dado inconsistente)
- [ ] Testes existentes de `Cotacao` continuam verdes (default novo parâmetro = null)

**Verificação:**
- [ ] `dotnet test --filter "FullyQualifiedName~Sgcf.Domain.Tests.Cotacoes"`
- [ ] Novos testes em `tests/Sgcf.Domain.Tests/Cotacoes/CotacaoRefinimpTests.cs`: criar Refinimp sem mãe → falha; criar Refinimp com mãe → ok; criar Finimp com mãe → falha

**Dependências:** nenhuma

**Arquivos prováveis:**
- `src/Sgcf.Domain/Cotacoes/Cotacao.cs`
- `tests/Sgcf.Domain.Tests/Cotacoes/CotacaoRefinimpTests.cs`

**Escopo:** S

---

#### Checkpoint A — Domínio

- [ ] `dotnet build` limpo
- [ ] Suite de testes do domínio verde
- [ ] Revisão humana da invariante AD-1/AD-8

---

### Fase 2 — Persistência

#### Task 2.1 — Migration `S6_CotacaoContratoMae`

**Descrição:** Adicionar coluna `contrato_mae_id uuid NULL` em `cotacao`; FK opcional para `contrato(id)` com `ON DELETE RESTRICT` (não permitir apagar mãe com cotação REFINIMP pendurada).

**Critérios de aceite:**
- [ ] Migration aditiva — não altera dados existentes
- [ ] FK `fk_cotacao_contrato_mae` para `contrato(id)`, `ON DELETE RESTRICT`
- [ ] Index não-único em `contrato_mae_id` (para listar cotações REFINIMP de um mãe)
- [ ] Migration reverte sem erro (down: drop FK + drop column)

**Verificação:**
- [ ] `dotnet ef migrations add S6_CotacaoContratoMae --project src/Sgcf.Infrastructure --startup-project src/Sgcf.Api`
- [ ] `dotnet ef database update` aplica em base com cotações FINIMP existentes (coluna NULL)
- [ ] `dotnet ef migrations remove` reverte

**Dependências:** Task 1.1

**Arquivos prováveis:**
- `src/Sgcf.Infrastructure/Migrations/2026xxxx_S6_CotacaoContratoMae.cs`
- `src/Sgcf.Infrastructure/Migrations/2026xxxx_S6_CotacaoContratoMae.Designer.cs`
- `src/Sgcf.Infrastructure/Migrations/SgcfDbContextModelSnapshot.cs`

**Escopo:** S

---

#### Task 2.2 — EF Configuration de `Cotacao.ContratoMaeId`

**Descrição:** Mapear a nova coluna em `CotacaoConfiguration` sem navegação inversa (relacionamento "fraco" — `Cotacao` aponta para `Contrato` por id apenas, evita ciclo de agregados).

**Critérios de aceite:**
- [ ] `Property(c => c.ContratoMaeId).HasColumnName("contrato_mae_id").IsRequired(false)`
- [ ] FK declarada via `HasOne<Contrato>().WithMany().HasForeignKey(c => c.ContratoMaeId).IsRequired(false).OnDelete(DeleteBehavior.Restrict)`
- [ ] `CotacaoRepository` não precisa de `.Include` (id puro)

**Verificação:**
- [ ] Teste de integração `CotacaoRepositoryTests.PersisteContratoMaeId` — round-trip
- [ ] Tentativa de DELETE em contrato com cotação REFINIMP filha falha com erro de FK

**Dependências:** Task 2.1

**Arquivos prováveis:**
- `src/Sgcf.Infrastructure/Persistence/Configurations/CotacaoConfiguration.cs`
- `tests/Sgcf.Api.IntegrationTests/Cotacoes/CotacaoRepositoryRefinimpTests.cs`

**Escopo:** S

---

#### Checkpoint B — Persistência

- [ ] Migration aplica e reverte sem erro
- [ ] Round-trip de `ContratoMaeId` funciona
- [ ] FK restritiva validada

---

### Fase 3 — Application: criação e captação

#### Task 3.1 — `CriarCotacaoCommand` aceita `contratoMaeId`

**Descrição:** Estender o command em `src/Sgcf.Application/Cotacoes/Commands/CriarCotacaoCommand.cs` para receber `Guid? ContratoMaeId`; validar obrigatoriedade quando `Modalidade == "Refinimp"`; validar existência do contrato mãe e que ele não está em status final inválido (cancelado/quitado).

**Critérios de aceite:**
- [ ] Command record ganha `Guid? ContratoMaeId = null`
- [ ] Validator: `When(c => Modalidade == Refinimp)` → `RuleFor(c => c.ContratoMaeId).NotNull().NotEqual(Guid.Empty)`
- [ ] Validator: `When(c => Modalidade != Refinimp)` → `RuleFor(c => c.ContratoMaeId).Null()` (defesa)
- [ ] Handler: se Refinimp, busca `Contrato` mãe via `IContratoRepository.GetByIdAsync` e rejeita se: não encontrado (404), status `Cancelado` ou `Quitado` (409 com mensagem clara)
- [ ] Handler propaga `contratoMaeId` para `Cotacao.Criar`

**Verificação:**
- [ ] Testes unitários do handler com mock do `IContratoRepository`: cenários ok / mãe inexistente / mãe cancelado / não-refi com mae setado
- [ ] Teste E2E: `POST /cotacoes` com modalidade=Refinimp e contratoMaeId válido → 201

**Dependências:** Task 1.1, Task 2.2

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/CriarCotacaoCommand.cs`
- `tests/Sgcf.Application.Tests/Cotacoes/CriarCotacaoRefinimpTests.cs`

**Escopo:** M

---

#### Task 3.2 — `AdicionarBancoNaCotacaoCommand` valida `AceitaRefinimp`

**Descrição:** Quando a cotação for REFINIMP, exigir que o `Banco` adicionado tenha `AceitaRefinimp == true`. Validação fail-fast (não esperar até a conversão).

**Critérios de aceite:**
- [ ] Handler de `AdicionarBancoNaCotacao` carrega o banco e verifica `Banco.AceitaRefinimp` quando `cotacao.Modalidade == Refinimp`
- [ ] Rejeita com `InvalidOperationException` (HTTP 409): "O banco '{apelido}' não aceita contratos Refinimp."
- [ ] Validação de `LimiteBanco` continua usando a modalidade da cotação (Refinimp tem registro próprio — AD-3)

**Verificação:**
- [ ] Teste E2E: cotação Refinimp + banco com AceitaRefinimp=false → 409 com mensagem específica
- [ ] Teste E2E: cotação Refinimp + banco com AceitaRefinimp=true + limite Refinimp suficiente → 201

**Dependências:** Task 3.1

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/AdicionarBancoNaCotacaoCommand.cs`
- `tests/Sgcf.Api.IntegrationTests/Cotacoes/AdicionarBancoRefinimpTests.cs`

**Escopo:** S

---

#### Task 3.3 — `RegistrarPropostaCommand` valida moeda vs contrato mãe

**Descrição:** Para cotações REFINIMP, a proposta deve ser em moeda igual à do contrato mãe (espelha `ProcessarRefinimpAsync` linhas 374–378). Validação na entrada da proposta.

**Critérios de aceite:**
- [ ] Handler carrega `cotacao.ContratoMaeId` e (se Refinimp) busca o `Contrato` mãe
- [ ] Compara `proposta.MoedaOriginal` com `contratoMae.Moeda`; rejeita com `InvalidOperationException` (HTTP 409) se diferentes — mensagem: "Proposta REFINIMP deve ser na mesma moeda do contrato mãe ({moedaMae}); recebida {moedaProposta}."
- [ ] FINIMP e demais modalidades não são afetadas (rota condicional)

**Verificação:**
- [ ] Teste unitário com mock: Refinimp em USD com mãe USD → ok
- [ ] Teste unitário: Refinimp em EUR com mãe USD → 409
- [ ] Teste de regressão FINIMP

**Dependências:** Task 3.1

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/RegistrarPropostaCommand.cs`
- `tests/Sgcf.Application.Tests/Cotacoes/RegistrarPropostaRefinimpTests.cs`

**Escopo:** S

---

#### Checkpoint C — Fluxo de captação REFINIMP

- [ ] Operador consegue criar cotação Refinimp com contratoMaeId
- [ ] Bancos sem AceitaRefinimp são rejeitados
- [ ] Propostas com moeda divergente do mãe são rejeitadas
- [ ] Suite E2E Cotações FINIMP continua verde (regressão zero)

---

### Fase 4 — Application: conversão em contrato

#### Task 4.1 — Estender `ConverterEmContratoCommand` para REFINIMP

**Descrição:** Adicionar branch `if (cotacao.Modalidade == ModalidadeContrato.Refinimp)` em `ConverterEmContratoCommand.Handle` (linha ~100, ponto de extensão atual do FINIMP). Reusar a lógica de `ProcessarRefinimpAsync` de `CreateContratoCommand`: walk até ancestral não-Refinimp, validar regra 70% BB, calcular `Percentual` e criar `RefinimpDetail`. Marcar contrato mãe via `MarcarRefinanciadoTotal/Parcial`.

**Critérios de aceite:**
- [ ] Após criar `Contrato` (já existente no handler), branch Refinimp executa:
  - Carrega `contratoPai = repo.GetByIdAsync(cotacao.ContratoMaeId)` — 404 se ausente
  - Valida moeda do contrato == moeda do mãe (defesa em profundidade)
  - Carrega `ancestral = repo.GetAncestraNaoRefinimpAsync(cotacao.ContratoMaeId)`
  - Se `Banco.CodigoCompe == "001"` (BB) e `valorPrincipal > 0.70m * ancestral.ValorPrincipal` → `InvalidOperationException` com mensagem idêntica ao `ProcessarRefinimpAsync`
  - Calcula `percentualFracao = valorPrincipal.Valor / ancestral.ValorPrincipal.Valor` e cria `RefinimpDetail`
  - `repo.AddRefinimpDetail(detail)`
  - Se `percentualFracao >= 1.0m` → `contratoPai.MarcarRefinanciadoTotal(clock)`, senão `MarcarRefinanciadoParcial`
- [ ] `ContratoDto.From` retornado inclui `RefinimpDetail` populado
- [ ] Atualização de `LimiteBanco` continua funcionando (sem mudança — modalidade já é Refinimp)

**Verificação:**
- [ ] Teste de integração: cotação Refinimp aceita → conversão → contrato Refinimp criado com `RefinimpDetail` + mãe marcada
- [ ] Teste: BB + valor > 70% ancestral → 409
- [ ] Teste: refi 100% → mãe vai para `RefinanciadoTotal`
- [ ] Teste: refi 50% → mãe vai para `RefinanciadoParcial`
- [ ] Regressão: cotação FINIMP continua convertendo

**Dependências:** Task 1.1, Task 3.1, Task 3.2, Task 3.3

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs`
- `tests/Sgcf.Api.IntegrationTests/Cotacoes/ConverterRefinimpTests.cs`

**Escopo:** M

---

#### Task 4.2 — Auditoria + snapshot da economia incluem REFINIMP

**Descrição:** Garantir que o `snapshotContratoJson` em `EconomiaNegociacao` documente o vínculo Refinimp (contratoMaeId, percentualRefinanciado, ancestralId). Auditoria do `state_transition` inclui referência ao contrato mãe.

**Critérios de aceite:**
- [ ] `snapshotContratoJson` ganha campos: `RefinimpContratoMaeId`, `RefinimpPercentualRefinanciado`, `RefinimpAncestralId` quando aplicável
- [ ] `audit_log` do `ConverterEmContrato` inclui `cotacao_id`, `contrato_mae_id` no payload
- [ ] `EconomiaNegociacao` permanece imutável; o snapshot reflete o estado da conversão

**Verificação:**
- [ ] Teste E2E lê `audit_log` após conversão Refinimp e valida payload
- [ ] Snapshot JSON inclui os 3 campos

**Dependências:** Task 4.1

**Arquivos prováveis:**
- `src/Sgcf.Application/Cotacoes/Commands/ConverterEmContratoCommand.cs`

**Escopo:** S

---

#### Checkpoint D — Conversão REFINIMP

- [ ] Cotação Refinimp → contrato Refinimp + RefinimpDetail + mãe marcada (transação atômica)
- [ ] Regra 70% BB bloqueia conversão indevida
- [ ] Snapshot/auditoria registram vínculo
- [ ] Suite E2E completa do módulo Cotações verde

---

### Fase 5 — API + Bruno + Golden

#### Task 5.1 — Atualizar request DTO de `POST /api/v1/cotacoes`

**Descrição:** Adicionar `contratoMaeId: Guid?` ao request da rota de criação. Documentar OpenAPI (XML doc).

**Critérios de aceite:**
- [ ] Controller `CotacoesController.Criar` aceita novo campo
- [ ] OpenAPI mostra o campo como opcional com descrição "obrigatório quando modalidade=Refinimp"

**Verificação:**
- [ ] Teste E2E HTTP `POST /api/v1/cotacoes` com modalidade=Refinimp + contratoMaeId → 201

**Dependências:** Task 3.1

**Escopo:** XS

---

#### Task 5.2 — Bruno collection: novo cenário REFINIMP

**Descrição:** Adicionar requests em `docs/api/collections/sgcf-api/` (subpasta de Cotações existente) cobrindo o fluxo completo: setup do contrato mãe FINIMP, criação da cotação Refinimp, adicionar banco BB, registrar proposta USD, aceitar, converter, verificar mãe marcada.

**Critérios de aceite:**
- [ ] Sequência completa em pasta `Cotacoes-Refinimp/`
- [ ] Variáveis de ambiente reutilizam `contratoMaeId` da resposta do setup
- [ ] Inclui caso de erro: banco sem AceitaRefinimp

**Verificação:**
- [ ] Execução manual em ambiente local valida fluxo
- [ ] Operador acompanha o smoke test

**Dependências:** Task 5.1, Task 4.1

**Escopo:** S

---

#### Task 5.3 — Golden Dataset: cenário REFINIMP

**Descrição:** Criar JSON em `tests/Sgcf.GoldenDataset/data/cotacoes-refinimp-001.json` com inputs do cenário completo (mãe FINIMP USD 1.000.000, refi 50% USD 500.000, BB) e `expectedOutput` para o CET, snapshot de proposta e economia.

**Critérios de aceite:**
- [ ] Cenário cobre proposta → aceitação → conversão → contrato Refinimp + mãe RefinanciadoParcial
- [ ] CET esperado calculado com a mesma `CalculadoraCet` do MVP (AD-7)
- [ ] Teste regressivo pulled automaticamente pela suite golden

**Verificação:**
- [ ] `dotnet test tests/Sgcf.GoldenDataset/Sgcf.GoldenDataset.csproj` verde com o novo cenário
- [ ] Aprovação humana do valor de CET esperado

**Dependências:** Task 4.1

**Escopo:** M

---

#### Checkpoint E — Verificação fim-a-fim

- [ ] Bruno collection valida o fluxo completo manualmente
- [ ] Golden test cobre o cenário canônico
- [ ] Suite completa `dotnet test` verde

---

### Fase 6 — Documentação

#### Task 6.1 — Atualizar `docs/specs/cotacoes/SPEC.md`

**Critérios de aceite:**
- [ ] Remover REFINIMP do §11 (boundaries) — modalidade passa a ser suportada
- [ ] Adicionar nova seção (§14 ou §15) "Modalidade REFINIMP" descrevendo: `ContratoMaeId` no agregado, regra 70% BB, marcação de mãe, divergências em relação a FINIMP
- [ ] Atualizar §1 (modalidades MVP) para "FINIMP + REFINIMP"
- [ ] §3 incrementa invariantes de `Cotacao` (AD-1, AD-8)

**Escopo:** S

---

#### Task 6.2 — Atualizar `docs/api/cotacoes.md`

**Critérios de aceite:**
- [ ] Payload de `POST /cotacoes` inclui `contratoMaeId`
- [ ] Exemplos de proposta REFINIMP
- [ ] Tabela de erros 409 nova (banco sem AceitaRefinimp, moeda divergente, regra 70% BB)

**Escopo:** S

---

#### Task 6.3 — CHANGELOG v0.7.0

**Critérios de aceite:**
- [ ] Bloco `ADDITIVE — Cotações — Modalidade REFINIMP` documenta nova capacidade
- [ ] Bloco `INTERNAL — Migration S6` documenta coluna nova
- [ ] Nota de compatibilidade: cotações FINIMP existentes não afetadas

**Escopo:** XS

---

#### Checkpoint Final

- [ ] `dotnet build` limpo
- [ ] `dotnet test` verde (Unit + Integration + Golden)
- [ ] SPEC + API doc atualizadas
- [ ] Bruno collection revisada
- [ ] CHANGELOG atualizado
- [ ] PR pronto para review

---

## 5. Riscos e Mitigações

| Risco                                                                                                            | Probabilidade | Impacto | Mitigação                                                                                                                                                                                                                  |
| ---------------------------------------------------------------------------------------------------------------- | ------------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Regra 70% BB bloqueia conversão depois que operador investiu tempo no fluxo                                       | Alta          | Médio   | Adicionar pré-cálculo opcional no `CompararPropostasQuery` que avise se o `ValorOferecido` excede 70% do ancestral (informativo, não bloqueante). Documentar a regra no `docs/api/cotacoes.md`.                              |
| Contrato mãe selecionado é ele próprio um REFINIMP (cadeia recursiva)                                             | Média         | Médio   | `GetAncestraNaoRefinimpAsync` já lida com walk recursivo. Garantir teste com cadeia de 3 níveis no golden ou integration.                                                                                                   |
| Contrato mãe foi cancelado/quitado entre criação da cotação e conversão                                           | Baixa         | Alto    | Task 3.1 valida status do mãe na criação; Task 4.1 revalida na conversão (defesa em profundidade). Mensagem clara orienta o operador.                                                                                       |
| Impacto duplo em `LimiteBanco`: cotação REFINIMP consome limite enquanto o FINIMP mãe ainda consome o seu próprio | Média         | Médio   | Documentar como decisão consciente — o REFINIMP toma novo recurso até a quitação do mãe ser registrada. Conciliação automática fica fora deste escopo (questão aberta Q3).                                                  |
| Moeda do mãe diverge da disposição do operador (operador quer refi em BRL um FINIMP USD)                          | Média         | Baixo   | Task 3.3 falha cedo na proposta com mensagem específica; questão aberta Q4 para PO decidir se cross-currency será suportado no futuro.                                                                                      |
| Mudança em `Cotacao.Criar` quebra testes/seed existentes                                                          | Baixa         | Médio   | Parâmetro novo com default `null`; toda fila de testes FINIMP continua compilando sem ajuste.                                                                                                                                |
| Snapshot da economia (REFINIMP) torna comparações de "economia mensal por modalidade" assimétricas                | Baixa         | Baixo   | Relatório `GetEconomiaPeriodoQuery` já agrupa por modalidade; REFINIMP entra como linha própria.                                                                                                                             |

---

## 6. Perguntas em Aberto

1. **Prazo da proposta vs prazo restante do contrato mãe (AD-9):** o sistema deve impor `proposta.PrazoDias <= diasRestantesDoMae` ou apenas avisar? Hoje `ProcessarRefinimpAsync` não impõe.
2. **Sublimite REFINIMP no Banco do Brasil (AD-3):** a regra 70% é codificada apenas como teto absoluto no contrato; deveria também restringir `LimiteBanco.Refinimp` a um percentual do `LimiteBanco.Finimp` automaticamente? Hoje são linhas independentes.
3. **Reconciliação de limite com mãe marcada `RefinanciadoTotal`:** quando o mãe é totalmente refinanciado, deve `LimiteBanco.Finimp.ValorUtilizadoBRL` ser decrementado automaticamente? Fora deste escopo, mas precisa decisão do PO.
4. **Cross-currency:** suportar refi em BRL de um mãe em USD (uso prático: liquidar exposição cambial)? Hoje Task 3.3 bloqueia. PO precisa confirmar.
5. **Cadeias de REFINIMP (refi de refi de refi):** suportadas estruturalmente, mas requer teste explícito. Definir profundidade máxima (n/a, 3, 5)?
6. **Status `RefinanciadoTotal` do mãe interage com `Quitado`?** O fluxo atual marca como `RefinanciadoTotal`, sem disparar `Quitado`. Confirmar com PO se há transição implícita.
7. **Listagem/filtro:** `ListCotacoesQuery` deve permitir filtrar por `contratoMaeId`? Útil para gestão de cadeias.

---

## 7. Paralelização

- **Sequencial obrigatório (caminho crítico):** 1.1 → 2.1 → 2.2 → 3.1 → 4.1 → 5.3
- **Paralelo após Task 3.1:** Tasks 3.2 e 3.3 podem rodar em paralelo (handlers independentes)
- **Paralelo após Checkpoint D:** Tasks 5.1, 5.2, 5.3 e a Fase 6 (6.1, 6.2, 6.3) podem rodar em paralelo
- **Fase 6 inteira:** documentação pode começar em paralelo com Fase 5 desde que a API esteja estável

---

## 8. Sumário Quantitativo

- **6 fases**, **13 tasks**, **6 checkpoints** (A, B, C, D, E, Final)
- **Escopo total:** 4 M, 6 S, 3 XS
- **Caminho crítico:** 1.1 → 2.1 → 2.2 → 3.1 → 4.1 → 5.3 (6 tasks)
- **Sem pré-requisito externo:** o módulo Contratos REFINIMP já está estável.
- **Decisões pendentes para humano:** 7 questões em §6
