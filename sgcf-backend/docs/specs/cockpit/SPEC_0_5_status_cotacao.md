# SPEC — Task 0.5 — Expansão `StatusCotacao` com Estágios Intermediários

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 0.5
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** S
> **Dependências:** Nenhuma

---

## 1. Objetivo

Expandir o enum `StatusCotacao` com dois novos estágios — `EmAnaliseBanco` e `PropostaRecebida` — para que o funil agregado de cotações (UX §3.2) tenha a granularidade requerida pelo Gerente Financeiro. Decisão registrada pelo sponsor em 2026-05-20.

---

## 2. Enum Atualizado

```csharp
namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Ciclo de vida de uma Cotacao. Valores byte fixos — não reordenar.
/// Novos valores recebem bytes não contíguos (7, 8) para preservar a ordem
/// histórica das colunas SMALLINT no PostgreSQL.
/// Ver máquina de estados em docs/specs/cotacoes/SPEC.md §4.
/// </summary>
public enum StatusCotacao : byte
{
    Rascunho         = 1,
    EmCaptacao       = 2,  // existente: enviada aos bancos, aguarda primeiro feedback
    EmAnaliseBanco   = 7,  // NOVO: banco confirmou recebimento e está analisando
    PropostaRecebida = 8,  // NOVO: ao menos uma proposta registrada
    Comparada        = 3,  // existente: captação encerrada, análise interna em curso
    Aceita           = 4,
    Convertida       = 5,
    Recusada         = 6,
}
```

**Ordem lógica de progressão (independente do byte):**

`Rascunho → EmCaptacao → EmAnaliseBanco → PropostaRecebida → Comparada → Aceita → Convertida` (caminho feliz)

`Recusada` é estado terminal alternativo, alcançável a partir de qualquer estado intermediário com permissão de cancelamento.

---

## 3. Máquina de Estados

### 3.1 Transições válidas

| De | Para | Trigger | Quem |
|----|------|---------|------|
| `Rascunho` | `EmCaptacao` | Enviar a banco(s) — `Cotacao.EnviarParaBancos()` | Operador |
| `EmCaptacao` | `EmAnaliseBanco` | Banco confirma análise (manual ou timeout — ver §3.2) | Sistema/Operador |
| `EmCaptacao` | `PropostaRecebida` | Primeira proposta chega (pula `EmAnaliseBanco`) | Banco |
| `EmAnaliseBanco` | `PropostaRecebida` | Primeira proposta chega | Banco |
| `PropostaRecebida` | `Comparada` | `Cotacao.EncerrarCaptacao()` | Operador |
| `Comparada` | `Aceita` | `Cotacao.AceitarProposta(...)` | Gerente Financeiro |
| `Aceita` | `Convertida` | `Cotacao.ConverterEmContrato(...)` | Operador |
| `Aceita` | `Comparada` | `Cotacao.DesfazerAceitacao(...)` (reverso permitido) | Gerente Financeiro |
| `Rascunho` `EmCaptacao` `EmAnaliseBanco` `PropostaRecebida` `Comparada` | `Recusada` | `Cotacao.Cancelar()` | Operador |

### 3.2 Política de transição `EmCaptacao → EmAnaliseBanco`

Refinamento aberto (`SPEC.md` §10). Opções em discussão:

- **Opção A — manual:** endpoint `POST /api/v1/cotacoes/{id}/marcar-em-analise` que registra a confirmação do banco.
- **Opção B — automática por timeout:** job marca `EmAnaliseBanco` após 24 h em `EmCaptacao` sem proposta.
- **Opção C — híbrida:** A + B (manual sempre vence; timeout só atua se ninguém marcou).

**Default proposto neste SPEC enquanto não há decisão:** Opção C, com endpoint manual no MVP e cron diário (06:00 BRT) marcando timeout em cotações antigas.

### 3.3 Transições inválidas

Tentativa de pular estágios (`Rascunho → PropostaRecebida` direto, ou `Convertida → qualquer coisa`) lança `InvalidStateTransitionException` no agregado.

---

## 4. Mudanças no Agregado `Cotacao`

Métodos novos:

```csharp
public sealed class Cotacao : Entity
{
    // ... existente

    public void MarcarEmAnaliseBanco(IClock clock)
    {
        if (Status is not StatusCotacao.EmCaptacao)
        {
            throw new InvalidStateTransitionException(Status, StatusCotacao.EmAnaliseBanco);
        }

        Status = StatusCotacao.EmAnaliseBanco;
        UpdatedAt = clock.GetCurrentInstant();
    }

    // O método existente RegistrarProposta deve, ao adicionar a PRIMEIRA proposta,
    // transitar para PropostaRecebida (de EmCaptacao ou EmAnaliseBanco).
    public Proposta RegistrarProposta(/* ... params ... */, IClock clock)
    {
        if (Status is not (StatusCotacao.EmCaptacao or StatusCotacao.EmAnaliseBanco or StatusCotacao.PropostaRecebida))
        {
            throw new InvalidStateTransitionException(Status, "RegistrarProposta requer cotação em captação");
        }

        // ... lógica existente

        if (Status is StatusCotacao.EmCaptacao or StatusCotacao.EmAnaliseBanco)
        {
            Status = StatusCotacao.PropostaRecebida;
        }

        UpdatedAt = clock.GetCurrentInstant();
        return proposta;
    }

    // EncerrarCaptacao continua aceitando EmCaptacao/EmAnaliseBanco/PropostaRecebida
    // e transita para Comparada.
}
```

---

## 5. Endpoint Novo

### 5.1 `POST /api/v1/cotacoes/{id}/marcar-em-analise`

**Auth:** `Policies.Escrita`.
**Body:** `{ }` (vazio) ou `{ "observacoes": "..." }` (opcional).
**Response:** 200 com `CotacaoDto` atualizado, ou 409 se transição inválida.

```csharp
[HttpPost("{id:guid}/marcar-em-analise")]
[Authorize(Policy = Policies.Escrita)]
[ServiceFilter(typeof(IdempotencyFilter))]
public async Task<IActionResult> MarcarEmAnalise(Guid id, CancellationToken ct)
{
    try
    {
        await mediator.Send(new MarcarCotacaoEmAnaliseBancoCommand(id), ct);
        return Ok(await mediator.Send(new GetCotacaoQuery(id), ct));
    }
    catch (InvalidStateTransitionException ex)
    {
        return Conflict(new ProblemDetails { Detail = ex.Message });
    }
}
```

---

## 6. Migration

```sql
-- Nenhuma alteração de schema necessária: enum está em coluna SMALLINT.
-- Apenas atualizar comentário/documentação se houver.

COMMENT ON COLUMN cotacao.status IS
  'StatusCotacao byte enum: 1=Rascunho, 2=EmCaptacao, 3=Comparada, 4=Aceita, 5=Convertida, 6=Recusada, 7=EmAnaliseBanco, 8=PropostaRecebida';
```

Migration EF Core gera apenas atualização de metadata. Dados existentes ficam em `EmCaptacao` (valor 2) ou estados posteriores — nenhuma cotação histórica precisa ser remapeada.

---

## 7. Impacto Downstream

| Componente | Impacto |
|------------|---------|
| `ListCotacoesQuery` | Aceita novos valores na string `status`. Validação por `Enum.TryParse` cobre. |
| `CotacoesController.List` filtros | Sem alteração — já é genérico. |
| `CotacaoDto.Status` (string) | Serializa novos valores `"EmAnaliseBanco"` e `"PropostaRecebida"`. |
| Swagger | Atualizar enum exposto. |
| Front-end | Mapeamento de rótulos atualizado conforme `12_BACKEND_API_COCKPIT_FE_GUIDE.md`. |
| MCP/A2A | Se algum tool retorna `StatusCotacao` em prompt, atualizar lista de valores aceitos. |

---

## 8. Atualização de Documentação

`docs/specs/cotacoes/SPEC.md §4` (máquina de estados) precisa ser atualizado com:

- Diagrama de transições incluindo os dois novos estados.
- Tabela de transições válidas (§3.1 deste SPEC).
- Nota sobre política de `EmCaptacao → EmAnaliseBanco`.

---

## 9. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Cotação em `Rascunho` recebe `POST /marcar-em-analise` | 409 Conflict |
| Primeira proposta chega quando ainda `EmCaptacao` | Pula direto para `PropostaRecebida` |
| Segunda proposta chega quando já `PropostaRecebida` | Mantém status, apenas adiciona à coleção |
| Cotação criada antes desta task (todas em `EmCaptacao`) | Continua válida; pode ser marcada `EmAnaliseBanco` normalmente |
| Tentativa de `Convertida → EmAnaliseBanco` | 409 |
| Tentativa de `Recusada → qualquer` | 409 |

---

## 10. Critérios de Aceite

- [ ] Enum atualizado com bytes 7 e 8 + XMLDoc.
- [ ] Método `Cotacao.MarcarEmAnaliseBanco` implementado.
- [ ] Método `Cotacao.RegistrarProposta` transita automaticamente para `PropostaRecebida`.
- [ ] Endpoint `POST /cotacoes/{id}/marcar-em-analise` registrado.
- [ ] Migration de metadata aplicada.
- [ ] `ListCotacoesQuery` aceita novos valores (`?status=EmAnaliseBanco` retorna 200).
- [ ] `docs/specs/cotacoes/SPEC.md §4` atualizado.
- [ ] Diagrama de máquina de estados visualizado em PR.

---

## 11. Verificação

```bash
# Domain unit tests da máquina de estado
dotnet test --filter "FullyQualifiedName~CotacaoMachineState"

# Integration: endpoint novo
dotnet test --filter "FullyQualifiedName~MarcarEmAnaliseTests"

# Smoke
dotnet run --project src/Sgcf.Api
curl -X POST http://localhost:5000/api/v1/cotacoes/{id}/marcar-em-analise -H "Authorization: Bearer ..."
```

**Teste-chave:**

```csharp
[Theory]
[InlineData(StatusCotacao.Rascunho)]
[InlineData(StatusCotacao.Comparada)]
[InlineData(StatusCotacao.Convertida)]
public void MarcarEmAnaliseBanco_quando_status_invalido_lanca(StatusCotacao statusAtual)
{
    var cotacao = CriarCotacaoEmStatus(statusAtual);

    Action act = () => cotacao.MarcarEmAnaliseBanco(_clock);

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void RegistrarProposta_quando_primeira_em_EmCaptacao_transita_para_PropostaRecebida()
{
    var cotacao = CriarCotacaoEmStatus(StatusCotacao.EmCaptacao);

    cotacao.RegistrarProposta(/* ... */, _clock);

    cotacao.Status.Should().Be(StatusCotacao.PropostaRecebida);
}
```

---

## 12. Boundaries específicas

### 12.1 Always do
- Atualizar XMLDoc do enum com a tabela de bytes.
- Validar transição no agregado, não no controller.
- Manter ordem lógica documentada (independe da ordem byte).

### 12.2 Ask first
- Adicionar mais estágios ao enum — discutir antes de criar value 9.
- Implementar timeout automático de `EmAnaliseBanco` (Opção B) — confirmar com Gerente Financeiro.

### 12.3 Never do
- Reordenar bytes 1-6 (quebra migrations + dados existentes).
- Permitir transição direta `Rascunho → PropostaRecebida` (pula a captação).
- Aceitar transição que regrida estado terminal (`Convertida`, `Recusada`).

---

## 13. Arquivos esperados

- `src/Sgcf.Domain/Cotacoes/StatusCotacao.cs` (atualizar)
- `src/Sgcf.Domain/Cotacoes/Cotacao.cs` (novos métodos)
- `src/Sgcf.Domain/Common/InvalidStateTransitionException.cs` (se não existir)
- `src/Sgcf.Application/Cotacoes/Commands/MarcarCotacaoEmAnaliseBancoCommand.cs` + Handler
- `src/Sgcf.Api/Controllers/CotacoesController.cs` (endpoint novo)
- `src/Sgcf.Infrastructure/Migrations/<ts>_ExpandirStatusCotacao.cs` (apenas metadata)
- `docs/specs/cotacoes/SPEC.md` (atualizar §4)
- `tests/Sgcf.Domain.Tests/Cotacoes/CotacaoMachineStateTests.cs`
- `tests/Sgcf.Api.IntegrationTests/CotacoesControllerMarcarEmAnaliseTests.cs`
