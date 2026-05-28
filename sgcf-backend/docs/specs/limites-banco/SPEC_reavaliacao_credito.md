# SPEC — Reavaliação de Crédito: Ciclo de Vida de Limites Operacionais

> **Status:** Aprovado — aguardando implementação
> **Data:** 2026-05-28
> **Versão:** v1.1

---

## 1. Objetivo

Permitir que o sistema suporte o ciclo completo de reavaliação de crédito bancário:
o banco encerra a linha vigente e concede uma nova (com valor, garantias e período distintos),
sem deixar janela de inconsistência nem exigir exclusão de dados históricos.

### 1.1. Problema atual

| Gap | Impacto operacional |
|-----|---------------------|
| `PATCH /limites-banco/{id}` não expõe `dataVigenciaFim` | Operador não consegue encerrar a vigência de um limite sem período — banco/modalidade fica **bloqueado** para criar sucessor (conflito 409) |
| Não existe operação atômica de substituição | Operador precisa de dois requests independentes; se o segundo falhar, o sistema fica sem limite vigente para o banco/modalidade |
| Não existe endpoint de exclusão | Sem alternativa de escape: limite de vigência indefinida é irremovível pela API atual |

O domínio (`LimiteBanco.Atualizar`) **já suporta** `novaDataVigenciaFim` e `novaDataVigenciaInicio`.
O gap está nas camadas Application (command não expõe os campos) e API (contrato não os documenta).

### 1.2. Personas

| Persona | Necessidade |
|---------|-------------|
| **Operador de Tesouraria** | Registrar que o banco revisou e concedeu novo limite a partir de uma data |
| **Gerente Financeiro** | Consultar o histórico contínuo de limites por banco/modalidade sem lacunas |
| **Auditor** | Rastrear qual limite estava vigente em qualquer data passada |

---

## 2. Glossário

| Termo | Definição |
|-------|-----------|
| **Limite vigente** | `LimiteBanco` com `DataVigenciaFim IS NULL` ou `DataVigenciaFim >= hoje` |
| **Encerramento de vigência** | Atribuir `DataVigenciaFim` a um limite até então aberto (sem fim definido) |
| **Substituição** | Operação atômica: encerra o limite atual e cria o sucessor em uma transação |
| **Períodos adjacentes** | `fim_anterior + 1 dia = início_sucessor` — não configuram sobreposição |

---

## 3. Escopo

### 3.1. Incluído nesta spec

| ID | Funcionalidade |
|----|---------------|
| **RV-01** | Expor `novaDataVigenciaFim` (e `novaDataVigenciaInicio`) no `PATCH /limites-banco/{id}` |
| **RV-02** | Novo endpoint `POST /limites-banco/{id}/substituir` — substituição atômica |
| **RV-03** | Validação de sobreposição ao atualizar `dataVigenciaFim` via PATCH |

### 3.2. Fora do escopo

- Exclusão (`DELETE`) de limites operacionais — não planejada; encerramento de vigência cobre o caso de uso.
- Transferência de `valorUtilizadoBrl` entre limite encerrado e sucessor — contratos existentes permanecem vinculados ao limite original.
- Alteração retroativa de `dataVigenciaInicio` enquanto `valorUtilizadoBrl > 0`.

---

## 4. Regras de negócio

### RV-01 — Encerrar / ajustar vigência via PATCH

| Regra | Descrição |
|-------|-----------|
| **RV-01-A** | `novaDataVigenciaFim` deve ser posterior a `DataVigenciaInicio` do limite |
| **RV-01-B** | Ao alterar `dataVigenciaFim`, o sistema deve verificar sobreposição com outros limites do mesmo par banco/modalidade (excluindo o próprio), usando `FindOverlappingAsync(excludeId: limite.Id)` |
| **RV-01-C** | Alterar `dataVigenciaFim` **não** afeta contratos já existentes vinculados ao limite — apenas impede novos usos após a data |
| **RV-01-D** | `novaDataVigenciaInicio` somente pode ser alterada quando `ValorUtilizadoBrl == 0` (sem contratos ativos) |

### RV-02 — Substituição atômica

| Regra | Descrição |
|-------|-----------|
| **RV-02-A** | `novoInicio` (data de início do sucessor) deve ser posterior a `DataVigenciaInicio` do limite substituído |
| **RV-02-B** | O sistema define automaticamente `DataVigenciaFim` do limite substituído como `novoInicio.MinusDays(1)` (períodos adjacentes sem lacuna) |
| **RV-02-C** | O successor herda `BancoId` e `Modalidade` do substituído — não é possível mudar banco ou modalidade numa substituição |
| **RV-02-D** | Toda a lógica de criação do sucessor (validação de sobreposição, LG-09, garantias) aplica-se normalmente |
| **RV-02-E** | Ambas as operações (encerramento do atual + criação do sucessor) ocorrem em uma única transação — em caso de falha, nenhuma persiste |
| **RV-02-F** | O response retorna o DTO do **sucessor** criado (HTTP 201 + Location header) |

---

## 5. Contrato de API

### 5.1. PATCH /api/v1/limites-banco/{id} — campos adicionados (RV-01)

Novos campos opcionais no request body:

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `novaDataVigenciaFim` | `date \| null` | Encerra a vigência; `null` = preserva o valor atual |
| `novaDataVigenciaInicio` | `date \| null` | Ajusta o início; `null` = preserva; só permitido com `valorUtilizadoBrl == 0` |
| `motivoEncerramento` | `string \| null` | Registrado no histórico quando `novaDataVigenciaFim` é informado; ex.: "Banco retirou a linha" |

**Aviso quando `valorUtilizadoBrl > 0`:** se o limite possui utilização ativa no momento do encerramento, o `200 OK` deve incluir o campo `avisos` no body:

```json
{
  "limite": { /* LimiteBancoDto */ },
  "avisos": [
    "Este limite possui BRL 12.000.000,00 em utilização ativa. Contratos vinculados não são afetados, mas nenhuma nova cotação poderá usar este limite após 2026-06-30."
  ]
}
```

Quando não há utilização ativa, `avisos` é omitido (ou `[]`) e o response continua sendo o `LimiteBancoDto` diretamente para não quebrar clientes existentes que não enviam os novos campos.

> **Decisão de contrato:** o PATCH sem `novaDataVigenciaFim` continua retornando `LimiteBancoDto` simples (compatibilidade). Com `novaDataVigenciaFim`, retorna `AtualizarLimiteBancoResponse` com `limite` + `avisos`.

Novos códigos de resposta:

| Código | Condição |
|--------|----------|
| `409 Conflict` | Nova vigência sobrepõe outro limite do mesmo banco/modalidade |
| `400 Bad Request` | `dataVigenciaFim <= dataVigenciaInicio` ou ajuste de início com contratos ativos |

Campos existentes permanecem com semântica inalterada.

### 5.2. POST /api/v1/limites-banco/{id}/substituir (RV-02)

```
Autorização: Admin
```

**Path parameter:** `{id}` — ID do limite a ser encerrado.

**Request body:**

```json
{
  "novoInicio": "2026-07-01",
  "novoValorLimiteBrl": 80000000.00,
  "novaDataVigenciaFim": "2027-06-30",
  "observacoes": "Renovação anual — comitê de crédito mai/2026",
  "garantiasExigidas": [
    {
      "tipo": "CdbCativo",
      "percentualSobreLimite": 25.0,
      "obrigatoria": true
    }
  ]
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `novoInicio` | `date` | Sim | Início do limite sucessor; define automaticamente `dataVigenciaFim` do atual como `novoInicio - 1 dia` |
| `novoValorLimiteBrl` | `decimal` | Sim | Valor do novo limite; deve ser > 0 |
| `novaDataVigenciaFim` | `date` | Não | Fim da vigência do sucessor; omitir = vigência indefinida |
| `observacoes` | `string` | Não | Observações do novo limite |
| `garantiasExigidas` | `CriarGarantiaExigidaItemRequest[]` | Não | `null` ou omitido = sem garantias; lista = replace-all |
| `motivoEncerramento` | `string` | Não | Motivo do encerramento do limite atual; registrado no histórico |

**O sucessor não herda configurações de antecipação** do limite encerrado. Se `PadraoAntecipacao` e afins forem necessários no sucessor, devem ser configurados separadamente via `PATCH /limites-banco/{id-sucessor}` após a substituição.

**Responses:**

| Código | Descrição |
|--------|-----------|
| `201 Created` | Substituição concluída; body = `LimiteBancoDto` do **sucessor**; `Location` aponta para o novo limite |
| `400 Bad Request` | Validação falhou (ex.: `novoInicio <= dataVigenciaInicio` do atual) |
| `404 Not Found` | Limite `{id}` não encontrado |
| `409 Conflict` | Sobreposição de vigência ou tipo de garantia duplicado |

---

## 6. Modelo de domínio — impacto

O domínio **não requer alterações**. `LimiteBanco.Atualizar()` já suporta todos os campos necessários para RV-01. `LimiteBanco.Criar()` já suporta os campos do sucessor para RV-02.

```
LimiteBanco.Atualizar(
    clock,
    novoLimiteBrl: null,
    novaDataVigenciaInicio: null,   ← já existe, só não exposto
    novaDataVigenciaFim: LocalDate  ← já existe, só não exposto
)
```

---

## 7. Alterações necessárias

### 7.1. Application layer

| Arquivo | Alteração |
|---------|-----------|
| `UpdateLimiteBancoCommand.cs` | Adicionar `NovaDataVigenciaFim: LocalDate?`, `NovaDataVigenciaInicio: LocalDate?` e `MotivoEncerramento: string?` ao record |
| `UpdateLimiteBancoCommandValidator` | Regras: `NovaDataVigenciaFim > DataVigenciaInicio`; ajuste de início apenas sem utilização |
| `UpdateLimiteBancoCommandHandler` | (1) Verificar sobreposição via `FindOverlappingAsync(excludeId)` quando `NovaDataVigenciaFim` informado; (2) Passar campos ao `limite.Atualizar()`; (3) Se `NovaDataVigenciaFim` informado e `ValorUtilizadoBrl > 0`, montar `AtualizarLimiteBancoResponse` com aviso; caso contrário, retornar `LimiteBancoDto` diretamente |
| `AtualizarLimiteBancoResponse.cs` | Novo DTO: `Limite: LimiteBancoDto` + `Avisos: string[]` — retornado apenas quando `NovaDataVigenciaFim` é informado |
| `SubstituirLimiteBancoCommand.cs` | Novo command: `LimiteId`, `NovoInicio`, `NovoValorLimiteBrl`, `NovaDataVigenciaFim?`, `Observacoes?`, `GarantiasExigidas?`, `MotivoEncerramento?` |
| `SubstituirLimiteBancoCommandValidator` | Validar campos do command |
| `SubstituirLimiteBancoCommandHandler` | (1) Carregar limite atual com tracking; (2) Verificar sobreposição do sucessor; (3) Verificar LG-09 para novo valor; (4) `limite.Atualizar(novaDataVigenciaFim: novoInicio.MinusDays(1))`; (5) `LimiteBanco.Criar(...)` com os campos do sucessor — **sem** herdar antecipação; (6) `repo.Add(sucessor)` + `repo.SaveChangesAsync()` |

### 7.2. API layer

| Arquivo | Alteração |
|---------|-----------|
| `LimitesBancoController.cs` | Adicionar campos `NovaDataVigenciaFim` e `NovaDataVigenciaInicio` ao request model do PATCH; adicionar novo endpoint `[HttpPost("{id}/substituir")]` mapeado para `SubstituirLimiteBancoCommand` |
| `docs/api/limites-banco.md` | Atualizar seção PATCH (novos campos); adicionar seção `Substituir Limite` |

### 7.3. Infrastructure layer

Verificar se `ILimiteBancoRepository.FindOverlappingAsync` aceita `excludeId` como parâmetro opcional para excluir o próprio limite na verificação de sobreposição do PATCH. Se não, adicionar o parâmetro.

---

## 8. Testes

### 8.1. Unitários (Sgcf.Domain.Tests)

Nenhum necessário — domínio não muda.

### 8.2. Application (Sgcf.Application.Tests)

| Cenário | Tipo |
|---------|------|
| PATCH com `novaDataVigenciaFim` válida e sem utilização ativa → `LimiteBancoDto` sem avisos | Handler |
| PATCH com `novaDataVigenciaFim` válida e `valorUtilizadoBrl > 0` → `AtualizarLimiteBancoResponse` com aviso descrevendo o valor em uso | Handler |
| PATCH com `novaDataVigenciaFim <= dataVigenciaInicio` → `ArgumentException` | Handler |
| PATCH com `novaDataVigenciaFim` sobrepondo outro limite → `InvalidOperationException` | Handler |
| PATCH sem `novaDataVigenciaFim` → retorna `LimiteBancoDto` simples (compatibilidade) | Handler |
| Substituir limite → sucesso; DTO do **sucessor** retornado; `dataVigenciaFim` do anterior = `novoInicio - 1 dia`; antecipação do sucessor = null | Handler |
| Substituir limite → falha ao criar sucessor → nenhuma alteração persiste (rollback) | Handler |
| Substituir limite com `novoInicio <= dataVigenciaInicio` → `ArgumentException` | Handler |
| Substituir limite com valor acima do limite global (LG-09) → `InvalidOperationException` | Handler |
| Substituir limite com `motivoEncerramento` → campo persistido e visível no `LimiteBancoDto` do anterior | Handler |

### 8.3. Integração (Sgcf.Api.IntegrationTests)

| Cenário | Endpoint |
|---------|----------|
| PATCH com `novaDataVigenciaFim` → `200 OK` com DTO correto | `PATCH /limites-banco/{id}` |
| PATCH com sobreposição → `409 Conflict` | `PATCH /limites-banco/{id}` |
| Substituição bem-sucedida → `201 Created` + `Location` header correto | `POST /limites-banco/{id}/substituir` |
| Substituição → `GET /limites-banco/{id-antigo}` retorna limite com `dataVigenciaFim` preenchida | Fluxo completo |
| Substituição → `GET /limites-banco` lista o novo limite como vigente | Fluxo completo |

---

## 9. Critérios de aceitação

- [ ] `PATCH /limites-banco/{id}` aceita e persiste `novaDataVigenciaFim` sem quebrar clientes que não enviam o campo.
- [ ] PATCH com `novaDataVigenciaFim` e `valorUtilizadoBrl > 0` retorna `200 OK` com campo `avisos` no body descrevendo o valor em uso; sem utilização, retorna `LimiteBancoDto` simples.
- [ ] Campo `motivoEncerramento` é persistido e visível no DTO do limite encerrado.
- [ ] Não é possível criar dois limites para o mesmo par banco/modalidade com períodos que se sobrepõem (inclusive via PATCH que estenda vigência).
- [ ] Períodos adjacentes (fim = D, início = D+1) **não** são rejeitados como sobreposição.
- [ ] `POST /limites-banco/{id}/substituir` executa em uma única transação: se a criação do sucessor falhar, o limite atual **não** é encerrado.
- [ ] O response de substituição retorna o DTO do **sucessor** (não do limite encerrado).
- [ ] O limite encerrado ainda é recuperável via `GET /limites-banco/{id-antigo}` com `dataVigenciaFim` preenchida.
- [ ] Todos os testes do cenário novo passam; nenhum teste existente regride.

---

## 10. Questões em aberto

Todas as questões foram respondidas em 2026-05-28:

| # | Questão | Decisão |
|---|---------|---------|
| Q1 | Aviso ao encerrar com `valorUtilizadoBrl > 0`? | **Sim** — retornar `AtualizarLimiteBancoResponse` com campo `avisos` descrevendo o valor em uso |
| Q2 | Substituição herda configurações de antecipação? | **Não** — o sucessor inicia sem configuração de antecipação |
| Q3 | Campo `motivoEncerramento` explícito? | **Sim** — adicionado ao PATCH e ao endpoint de substituição |

---

## Referências

- [`src/Sgcf.Domain/Cotacoes/LimiteBanco.cs`](../../src/Sgcf.Domain/Cotacoes/LimiteBanco.cs) — `Atualizar()` linha 384
- [`src/Sgcf.Application/Cotacoes/Commands/UpdateLimiteBancoCommand.cs`](../../src/Sgcf.Application/Cotacoes/Commands/UpdateLimiteBancoCommand.cs)
- [`docs/api/limites-banco.md`](../api/limites-banco.md)
- [SPEC Cotações §3.2](../cotacoes/SPEC.md) — consumidor do limite
- [SPEC Limite Global](./SPEC_LIMITE_GLOBAL.md) — regra LG-09
