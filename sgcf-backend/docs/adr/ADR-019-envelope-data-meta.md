# ADR-019 — Envelope de Resposta: `EnvelopeResponse<T>` + `EnvelopeMeta`

| Campo    | Valor                         |
|----------|-------------------------------|
| Status   | Aceito                        |
| Data     | 2026-05-21                    |
| Autor    | Time de Arquitetura           |
| Sponsor  | Welysson Soares               |

---

## Contexto

O SGCF expõe endpoints que retornam dados calculados ou agregados (painel, cockpit, projeções).
Esses dados têm características especiais:

- Podem ser compostos de múltiplas fontes (banco de dados, cache, API do BCB, Redis).
- O grau de frescor e completude varia por endpoint e por disponibilidade das fontes.
- Clientes (frontend, agentes de IA, integrações) precisam saber se o dado retornado é
  completo, parcial, ou construído a partir de fallback.

Sem um contrato explícito, essas informações ficam implícitas ou ausentes, forçando o
cliente a inferir a qualidade do dado a partir de campos de negócio (o que não é responsabilidade dele).

---

## Decisão

**Adotar um envelope de resposta padronizado para endpoints que retornam dados calculados.**

O envelope é composto por dois campos raiz:

```json
{
  "data": { ... },
  "meta": {
    "dataHoraCalculo": "2026-05-21T12:00:00Z",
    "fontesConsultadas": [
      { "fonte": "banco_de_dados", "status": "ok", "registros": 42 },
      { "fonte": "cache_redis",    "status": "cache_hit", "registros": null }
    ],
    "completude": "Completo"
  }
}
```

### Tipos envolvidos

- `EnvelopeResponse<T>` — record genérico em `Sgcf.Application.Common`.
  Separa `Data: T` de `Meta: EnvelopeMeta` sem impor restrições ao tipo de negócio.

- `EnvelopeMeta` — record com três campos:
  - `DataHoraCalculo: Instant` — capturado via `IClock` (NodaTime), nunca `DateTime.UtcNow`.
  - `FontesConsultadas: IReadOnlyList<FonteConsultada>` — fontes usadas para montar a resposta.
  - `Completude: Completude` — enum `Completo | Parcial | Degradado`.

### Aplicação via filtro opt-in

O filtro `EnvelopeResultFilter` (`IAsyncResultFilter`) envolve respostas automaticamente
quando o endpoint declara `[ProducesEnvelope]`. Sem o atributo, a resposta passa inalterada —
garantindo que endpoints de mutação (POST/PUT/DELETE) e endpoints simples não sejam afetados.

Handlers que precisam fornecer metadados ricos (fontes, completude customizada) podem retornar
`EnvelopeResponse<T>` diretamente. O filtro detecta isso e não re-envolve.

O filtro mínimo preenche:
- `DataHoraCalculo = IClock.GetCurrentInstant()`
- `FontesConsultadas = []`
- `Completude = Completo`

### Onde vive cada tipo

| Tipo                    | Projeto            | Motivo                                              |
|-------------------------|--------------------|-----------------------------------------------------|
| `EnvelopeResponse<T>`   | `Sgcf.Application` | Contrato de saída da camada de aplicação            |
| `EnvelopeMeta`          | `Sgcf.Application` | Parte do contrato; sem dependência de HTTP          |
| `FonteConsultada`       | `Sgcf.Application` | Value object de observabilidade                     |
| `Completude`            | `Sgcf.Application` | Enum de domínio de qualidade dos dados              |
| `EnvelopeResultFilter`  | `Sgcf.Api`         | Infraestrutura HTTP — depende de `IAsyncResultFilter` |
| `ProducesEnvelopeAttribute` | `Sgcf.Api`     | Marcador de endpoint; pertence à camada HTTP        |

---

## Consequências

- **Positivo:** Contrato explícito e consistente para dados calculados — clientes não precisam
  inferir frescor ou completude.
- **Positivo:** Opt-in por atributo — endpoints simples não são afetados; não há risco de
  quebrar contratos existentes.
- **Positivo:** `IClock` injetado no filtro garante conformidade com a regra de nunca usar
  `DateTime.UtcNow` em código de aplicação — testabilidade garantida.
- **Positivo:** Handlers que já produzem `EnvelopeResponse<T>` não são re-envolvidos,
  permitindo que fontes e completude customizadas sejam preservadas.
- **Negativo:** Adiciona dois campos raiz ao JSON de resposta — clientes precisam navegar
  para `data` para acessar o payload de negócio (breaking change para endpoints existentes
  que adotarem o atributo).
- **Negativo:** `Activator.CreateInstance` no filtro usa reflection em runtime para construir
  `EnvelopeResponse<T>` com o tipo desconhecido. Custo é desprezível (uma vez por request,
  não em hot path de cálculo), mas deve ser re-avaliado se o filtro for aplicado globalmente
  em alta frequência.

---

## Alternativas Consideradas

| Alternativa                                     | Motivo da recusa                                                                      |
|-------------------------------------------------|---------------------------------------------------------------------------------------|
| Filtro global (sem atributo opt-in)             | Quebraria contratos de endpoints existentes (mutações, listas simples)                |
| Middleware de resposta                          | Exige desserializar/re-serializar o corpo HTTP — frágil e caro                        |
| Envelope no DTO de negócio                      | Viola SRP: DTOs de negócio não devem carregar metadados de infra                      |
| `IOperationResult<T>` (Result pattern)         | Overhead de pattern sem ganho adicional para observabilidade de dados calculados      |
| Source-gen para evitar reflection               | Prematura: custo de reflection é desprezível no contexto; revisitar se medir impacto |
