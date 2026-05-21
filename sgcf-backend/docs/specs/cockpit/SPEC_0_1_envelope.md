# SPEC — Task 0.1 — Envelope `{ data, meta }` (ADR-019)

> **Master:** `SPEC.md`
> **Plano:** `tasks/plan_cockpit_backend_gaps.md` Task 0.1
> **Status:** Draft
> **Versão:** v1.0
> **Escopo:** S
> **Dependências:** Nenhuma

---

## 1. Objetivo

Padronizar todo endpoint novo do cockpit a retornar um envelope JSON `{ data, meta }`, onde `meta` informa qualidade dos dados, timestamp de cálculo e fontes consultadas. Endpoints **existentes** não são migrados nesta task — mantêm o payload direto até depreciação gradual planejada para a Fase 4.

Substitui o anti-padrão atual de DTOs com `dataHoraCalculo` espalhado em campos próprios e sem indicador de parcialidade.

---

## 2. Contrato Funcional

### 2.1 Estrutura do envelope

```jsonc
{
  "data": { /* payload do domínio, tipado */ },
  "meta": {
    "dataHoraCalculo": "2026-05-19T14:32:00Z",
    "fontesConsultadas": [
      { "fonte": "contratos", "status": "OK",         "registros": 142 },
      { "fonte": "hedges",    "status": "DEGRADADO",  "mensagem": "Provider X timeout em 1 chamada" }
    ],
    "completude": "COMPLETO"
  }
}
```

### 2.2 Tipos C#

```csharp
namespace Sgcf.Application.Common;

public sealed record EnvelopeResponse<T>(T Data, EnvelopeMeta Meta);

public sealed record EnvelopeMeta(
    Instant DataHoraCalculo,
    IReadOnlyList<FonteConsultada> FontesConsultadas,
    Completude Completude);

public sealed record FonteConsultada(
    string Fonte,
    StatusFonte Status,
    int? Registros = null,
    string? Mensagem = null);

public enum StatusFonte { OK, DEGRADADO, FALHA }
public enum Completude { COMPLETO, PARCIAL, DEGRADADO }
```

### 2.3 Regras de preenchimento

| Cenário | `completude` | `status` por fonte |
|---------|--------------|---------------------|
| Todas as fontes responderam, dados completos | `COMPLETO` | Todas `OK` |
| Pelo menos uma fonte com registros parciais ou ausentes mas resultado utilizável | `PARCIAL` | Pelo menos uma `DEGRADADO` |
| Resultado inutilizável porque fonte crítica falhou — emitir 503 ao invés de 200 | n/a (não responde envelope) | n/a |

`dataHoraCalculo` sempre em UTC (`Instant.ToString()` no formato ISO 8601). A serialização JSON converte para string `"2026-05-19T14:32:00Z"`.

---

## 3. Implementação

### 3.1 Helper de construção

```csharp
public static class EnvelopeResponse
{
    public static EnvelopeResponse<T> Ok<T>(
        T data,
        Instant agora,
        IReadOnlyList<FonteConsultada> fontes) =>
        new(data, new EnvelopeMeta(agora, fontes, DeterminarCompletude(fontes)));

    private static Completude DeterminarCompletude(IReadOnlyList<FonteConsultada> fontes)
    {
        if (fontes.All(f => f.Status == StatusFonte.OK))
        {
            return Completude.COMPLETO;
        }

        return fontes.Any(f => f.Status == StatusFonte.DEGRADADO)
            ? Completude.PARCIAL
            : Completude.DEGRADADO;
    }
}
```

### 3.2 Filtro ASP.NET Core (opcional)

Para handlers que retornam o tipo de domínio puro sem se preocupar com envelope, decorar o endpoint com `[ProducesEnvelope]` aciona um `IAsyncResultFilter` que envolve a resposta:

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class ProducesEnvelopeAttribute : Attribute { }

public sealed class EnvelopeResultFilter(IClock clock) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: { } payload, StatusCode: 200 } objectResult
            && context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<ProducesEnvelopeAttribute>() is not null
            && payload is not EnvelopeResponse<object>)
        {
            Instant agora = clock.GetCurrentInstant();
            EnvelopeMeta meta = new(
                agora,
                Array.Empty<FonteConsultada>(),
                Completude.COMPLETO);
            objectResult.Value = new { data = payload, meta };
        }

        await next();
    }
}
```

**Decisão de uso:** o handler explícito (`EnvelopeResponse.Ok(...)`) é o padrão preferido por preservar `fontesConsultadas`. O filtro é o fallback para endpoints simples.

### 3.3 Configuração de Swagger

Adicionar `EnvelopeResponse<T>` aos schemas do SwaggerGen para serializar nomes camelCase:

```csharp
services.AddSwaggerGen(opt =>
{
    opt.MapType<Instant>(() => new OpenApiSchema { Type = "string", Format = "date-time" });
    opt.MapType(typeof(EnvelopeResponse<>), () => new OpenApiSchema { /* genérico */ });
});
```

---

## 4. ADR-019 (resumo)

Texto a ser publicado em `docs/adr/ADR-019-envelope-data-meta.md`:

- **Contexto:** os DTOs atuais expõem `dataHoraCalculo` ad-hoc em campos próprios e usam `alertas: string[]` sem indicador de qualidade do dado. O cockpit precisa sinalizar parcialidade.
- **Decisão:** todo endpoint novo retorna `{ data, meta }`. Endpoints legados migram gradualmente na Fase 4.
- **Consequências:** assimetria temporária no consumo (FE precisa interceptor); ganho de observabilidade; deprecação de campos `dataHoraCalculo` em DTOs específicos.
- **Alternativas consideradas:** headers HTTP customizados (`X-Data-Source`), GraphQL extensions — descartadas por incompatibilidade com cache HTTP padrão e por requerer mudança de stack.

---

## 5. Casos de Borda

| Cenário | Comportamento |
|---------|---------------|
| Handler lança exceção antes de calcular meta | Filtro de exceção retorna `ProblemDetails`, sem envelope |
| Endpoint retorna 204 No Content | Sem envelope (response sem body) |
| Endpoint paginado (`PagedResult<T>`) | Envelope envolve o `PagedResult` inteiro: `{ data: { items, total, page, pageSize }, meta }` |
| Endpoint decorado com `[ProducesEnvelope]` que já retorna `EnvelopeResponse<T>` | Filtro detecta e não envolve novamente (idempotência) |
| Endpoint que retorna `Task` (sem valor) | Sem envelope |
| Cliente envia `Accept: text/csv` para exportação | Sem envelope (binário/texto direto) |

---

## 6. Critérios de Aceite

- [ ] `EnvelopeResponse<T>`, `EnvelopeMeta`, `FonteConsultada`, `StatusFonte`, `Completude` definidos em `Sgcf.Application.Common`.
- [ ] Helper estático `EnvelopeResponse.Ok<T>(...)` construído.
- [ ] Atributo `[ProducesEnvelope]` e filtro `EnvelopeResultFilter` registrados.
- [ ] Endpoint piloto (sugerido: `GET /api/v1/painel/divida/breakdown-modalidade` — Task 1.1) usa envelope corretamente.
- [ ] Schema gerado em Swagger reflete a estrutura camelCase.
- [ ] ADR-019 publicado em `docs/adr/`.
- [ ] Time FE consultado e ciente do novo formato.

---

## 7. Verificação

```bash
# Unit + integration
dotnet test --filter "FullyQualifiedName~Envelope"

# Verificar Swagger gerado
dotnet run --project src/Sgcf.Api
curl -s http://localhost:5000/swagger/v1/swagger.json | jq '.components.schemas.EnvelopeResponseBreakdownModalidadeDto'
```

**Teste de snapshot:** a resposta de um endpoint piloto bate com o schema:

```json
{
  "data": { /* ... */ },
  "meta": {
    "dataHoraCalculo": "2026-05-19T14:32:00Z",
    "fontesConsultadas": [],
    "completude": "COMPLETO"
  }
}
```

---

## 8. Boundaries específicas

### 8.1 Always do
- Aplicar envelope em **endpoint novo** desta entrega.
- Documentar fonte e status real de cada repositório consultado.

### 8.2 Ask first
- Migrar endpoint existente — exige aprovação por impactar consumidores legados (FE, MCP, A2A).

### 8.3 Never do
- Retornar envelope em status diferente de 200 (4xx e 5xx usam ProblemDetails).
- Misturar envelope com payload direto no mesmo endpoint.

---

## 9. Arquivos esperados

- `src/Sgcf.Application/Common/EnvelopeResponse.cs` (criar)
- `src/Sgcf.Application/Common/EnvelopeMeta.cs` (criar)
- `src/Sgcf.Application/Common/FonteConsultada.cs` (criar)
- `src/Sgcf.Application/Common/Completude.cs` (criar)
- `src/Sgcf.Application/Common/StatusFonte.cs` (criar)
- `src/Sgcf.Api/Filters/EnvelopeResultFilter.cs` (criar)
- `src/Sgcf.Api/Filters/ProducesEnvelopeAttribute.cs` (criar)
- `src/Sgcf.Api/Program.cs` (registrar filtro)
- `docs/adr/ADR-019-envelope-data-meta.md` (criar)
- `tests/Sgcf.Api.IntegrationTests/EnvelopeResponseTests.cs` (criar)
