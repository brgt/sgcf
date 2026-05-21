# Lifecycle, Arquitetura e Autorização

---

## 1. Lifecycle do `CenarioSimulacao`

### Diagrama de estados

```
                    ┌─────────────────┐
                    │                 │
               ┌───►    RASCUNHO      │◄── Estado inicial (Criar)
               │    │                 │
               │    └────────┬────────┘
               │             │  POST /ativar
               │             │  (Policy: Escrita)
               │             ▼
               │    ┌─────────────────┐
               │    │                 │
               │    │     ATIVO       │
               │    │                 │
               │    └────────┬────────┘
               │             │  POST /arquivar
               │             │  (Policy: Gerencial)
               │             ▼
               │    ┌─────────────────┐
               │    │                 │
               │    │   ARQUIVADO     │  (imutável via API)
               │    │                 │
               │    └─────────────────┘
               │
DuplicarComoRascunho (POST /duplicar, Policy: Escrita)
— disponível em qualquer status, exceto soft-deletado
```

### Operações por status

| Operação | Rascunho | Ativo | Arquivado |
|---|---|---|---|
| `Atualizar` (nome/descrição) | Sim | Sim | Não (409) |
| `Atualizar` (anoBase) | Sim | Não (409) | Não (409) |
| `Ativar` | Sim | Não (409 — já ativo) | Não (409) |
| `Arquivar` | Não (409 — deve estar Ativo) | Sim | Não (409) |
| `AdicionarSimulacao` | Sim | Sim | Não (409) |
| `RemoverSimulacao` | Sim | Sim | Não (409) |
| `AtualizarSimulacao` | Sim | Sim | Não (409) |
| `Deletar` (soft delete) | Sim | Sim | Sim |
| `Duplicar` | Sim | Sim | Sim (desde que não soft-deletado) |

### Regras de domínio importantes

**Soft delete:**  
O método `Deletar` preenche o campo `DeletedAt` (do tipo `Instant`) sem remover o registro do banco. Após o soft delete, `GET /cenarios/{id}` retorna 404 e o cenário não aparece na listagem. Cenários soft-deletados não podem ser duplicados.

Arquivo do agregado: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Simulacao/CenarioSimulacao.cs`

**`AnoBase` em cenário Ativo:**  
A tentativa de alterar `AnoBase` quando o cenário está `Ativo` lança `InvalidOperationException` (→ 409 Conflict). A mensagem orienta o usuário a duplicar o cenário como Rascunho para experimentar outro ano-base.

**Arquivamento:**  
Somente cenários `Ativos` podem ser arquivados. A tentativa de arquivar um `Rascunho` retorna 409 com mensagem explicativa. O backend não oferece endpoint de desarquivamento.

**Invariante da `SimulacaoContratacao` (I-4 — ano-base):**  
A `DataContratacaoPrevista` de cada simulação deve estar dentro do intervalo `[anoBase-01-01, anoBase-12-31]`. Isso é verificado no factory `SimulacaoContratacao.Criar`. O endpoint de preview `cronograma-hipotetico` ignora esta invariante (parâmetro `anoBase: null`), pois não tem cenário pai.

Arquivo da entidade: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Simulacao/SimulacaoContratacao.cs`

---

## 2. Decisões arquiteturais (AD-1 a AD-12)

Decisões registradas em `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/tasks/quadro-divida-simulacao/plan.md`.

| # | Decisão | Impacto para o front-end |
|---|---|---|
| AD-1 | `Sgcf.Domain.Simulacao` é um módulo separado de `Cotacoes` e do `Simulador` existente. | Nenhum. Apenas explica a separação conceitual. |
| AD-2 | Cenário como agregado versionável com lifecycle Rascunho/Ativo/Arquivado. | O front sempre escolhe `cenarioId` explicitamente — não existe "cenário ativo global". |
| AD-3 | Cronograma calculado on-the-fly + cache Redis com TTL de 60s por chave `(cenarioId, simulacaoId, version)`. | O campo `version` no `SimulacaoContratacaoDto` é a versão de cache. Incrementado a cada mutação. |
| AD-4 | `SimulacaoContratacao` reutiliza os mesmos campos de input do motor de cronograma de contratos reais. | A simulação gera cronograma idêntico ao contrato real após formalização. |
| AD-5 | `ProjetorSaldoMensal` é função pura sem I/O. | Projeção é determinística: mesma entrada sempre produz mesma saída. |
| AD-6 | `EventoProjecao` unifica amortizações reais e captações simuladas. Juros não entram na projeção de saldo. | O quadro da dívida mostra apenas movimentação de principal. |
| AD-7 | Endpoint único `GET /painel/quadro-divida?ano=&cenarioId=` retorna snapshot + projeção + sumário. | Uma única chamada renderiza a tabela inteira. |
| AD-8 | Conversão de moedas usa spot/PTAX corrente flat para toda a projeção. | Para cenário cambial use `POST /painel/simulador/cenario-cambial` (módulo separado). |
| AD-9 | Sem `cenarioId`, o quadro retorna apenas dados reais. Com `cenarioId`, inclui captações simuladas como overlay. | Permita renderizar o quadro antes de qualquer cenário ser criado. |
| AD-10 | `BancoId` + `BancoApelido` como dimensão primária em todo breakdown do quadro. | O front já recebe o agrupamento por banco pronto, sem pós-processamento. |
| AD-11 | RBAC: criar/editar cenário exige `Escrita`; arquivar exige `Gerencial`; consultar exige `Leitura`. | Ver seção 3 deste documento. |
| AD-12 | Exceder `LimiteBanco` na simulação gera alerta (não bloqueio). | O array `alertas` do `QuadroDividaDto` conterá strings descritivas. Renderize como warning não-bloqueante. |

---

## 3. Camadas da aplicação

```
┌────────────────────────────────────────────────────────────────────┐
│  Sgcf.Api                                                          │
│  Controllers: SimulacoesController, PainelController,             │
│               ParametrosSistemaController                          │
│  Filters: IdempotencyFilter                                        │
└──────────────────────────┬─────────────────────────────────────────┘
                           │ (MediatR commands/queries)
┌──────────────────────────▼─────────────────────────────────────────┐
│  Sgcf.Application                                                  │
│  Simulacao/Commands: Criar, Atualizar, Ativar, Arquivar,          │
│                      Duplicar, Deletar, AdicionarSimulacao,        │
│                      AtualizarSimulacao, RemoverSimulacao          │
│  Simulacao/Queries:  GetCenarioById, ListCenarios,                │
│                      SimularCronogramaHipotetico, CompararCenarios │
│  Painel/Queries:     GetQuadroDivida, GetSaldoPorBancoAtual       │
│  Painel/Validators:  ValidadorTetaoMensal (pure function)         │
│  Sistema/Commands:   AtualizarTetaoMensal                         │
└──────────────────────────┬─────────────────────────────────────────┘
                           │ (interfaces de repositório)
┌──────────────────────────▼─────────────────────────────────────────┐
│  Sgcf.Domain                                                        │
│  Simulacao: CenarioSimulacao (aggregate root),                     │
│             SimulacaoContratacao (child entity),                   │
│             StatusCenarioSimulacao, TipoTaxa                      │
│  Painel:   ProjetorSaldoMensal (pure function),                   │
│             EventoProjecao, QuadroDividaProjecao,                  │
│             MesProjecao, SaldoBancoMes, TipoEventoProjecao        │
│  Sistema:  ParametroSistema                                        │
│  Common:   Money, Moeda, BaseCalculo, Percentual                   │
└──────────────────────────┬─────────────────────────────────────────┘
                           │ (EF Core)
┌──────────────────────────▼─────────────────────────────────────────┐
│  Sgcf.Infrastructure                                               │
│  PostgreSQL 16 (EF Core): CenarioSimulacaoRepository,             │
│                             SimulacaoContratacaoRepository         │
│  Redis 7: cache de cronograma                                      │
└────────────────────────────────────────────────────────────────────┘
                           
┌────────────────────────────────────────────────────────────────────┐
│  Sgcf.Mcp  (thin adapter, Application apenas)                     │
│  SimulacaoTools: get_quadro_divida, list_cenarios_simulacao,      │
│                  get_cenario_simulacao                             │
└────────────────────────────────────────────────────────────────────┘
```

**Regra de dependência:** `Mcp` e `A2a` dependem apenas de `Application`. Nunca de `Infrastructure`. Ver `CLAUDE.md` do projeto para as regras completas.

---

## 4. Cache Redis

**Chave de cache:** `sim:cronograma:{cenarioId}:{simulacaoId}:v{version}`

O cronograma hipotético de cada `SimulacaoContratacao` é calculado on-the-fly na primeira consulta ao Quadro da Dívida e armazenado no Redis com TTL de 60 segundos. Toda mutação na simulação incrementa o campo `Version` da entidade (AD-3), o que invalida implicitamente o cache — a próxima consulta usa uma chave diferente e o cronograma é recalculado.

**O front-end não precisa gerenciar cache.** Ao receber um `SimulacaoContratacaoDto` com `version = 5`, saiba que qualquer resposta do Quadro da Dívida gerada após essa versão incorpora os dados mais recentes.

---

## 5. Autorização (policies e roles)

### Tabela de policies

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Authorization/Policies.cs`

| Constante | Valor da string | Descrição |
|---|---|---|
| `Policies.Leitura` | `"Leitura"` | Consulta de dados (GETs). Nível mais permissivo. |
| `Policies.Escrita` | `"Escrita"` | Criação e mutação de cenários e simulações. |
| `Policies.Gerencial` | `"Gerencial"` | Operações irreversíveis (arquivar cenário). Equivale ao papel "Gerente" descrito na SPEC. |
| `Policies.Executivo` | `"Executivo"` | KPIs executivos do dashboard (`GET /painel/kpis`). |
| `Policies.Auditoria` | `"Auditoria"` | Operações de auditoria (EBITDA mensal). |
| `Policies.Admin` | `"Admin"` | Administração do sistema (tetão mensal). |

### Policies por endpoint

| Endpoint | Policy |
|---|---|
| `POST /simulacoes/cronograma-hipotetico` | `Escrita` |
| `POST /simulacoes/cenarios` | `Escrita` |
| `GET /simulacoes/cenarios` | `Leitura` |
| `GET /simulacoes/cenarios/{id}` | `Leitura` |
| `PATCH /simulacoes/cenarios/{id}` | `Escrita` |
| `POST /simulacoes/cenarios/{id}/ativar` | `Escrita` |
| `POST /simulacoes/cenarios/{id}/arquivar` | `Gerencial` |
| `POST /simulacoes/cenarios/{id}/duplicar` | `Escrita` |
| `DELETE /simulacoes/cenarios/{id}` | `Escrita` |
| `POST /simulacoes/cenarios/{id}/simulacoes` | `Escrita` |
| `PATCH /simulacoes/cenarios/{id}/simulacoes/{simId}` | `Escrita` |
| `DELETE /simulacoes/cenarios/{id}/simulacoes/{simId}` | `Escrita` |
| `GET /simulacoes/cenarios/{id}/quadro-divida` | `Leitura` |
| `POST /simulacoes/comparar` | `Leitura` |
| `GET /painel/quadro-divida` | `Leitura` |
| `GET /parametros-sistema` | `Leitura` |
| `PATCH /parametros-sistema/tetao-mensal` | `Admin` |

### Idempotency-Key

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Api/Filters/IdempotencyFilter.cs`

O `IdempotencyFilter` está ativo nos seguintes endpoints:

- `POST /simulacoes/cenarios`
- `POST /simulacoes/cenarios/{id}/duplicar`
- `POST /simulacoes/cenarios/{id}/simulacoes`

**Comportamento:**

1. Se o header `Idempotency-Key` estiver **ausente**, a requisição prossegue normalmente sem deduplicação.
2. Se o header estiver presente com formato **inválido**, a API retorna 400 imediatamente com corpo RFC 7807.
3. Se a key for válida e **já existir** no cache com o mesmo escopo, a resposta em cache é retornada sem executar o handler (deduplicação sem reprocessamento).
4. Se a key for válida e **não existir**, a requisição é executada e a resposta 2xx é armazenada em cache por 24 horas.

**Formato aceito:**
```
UUID v4 canônico: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
OU
String alfanumérica: [A-Za-z0-9_-]{1,64}
```

**Escopo da chave de cache:**
```
idempotency:{userSub}:{method}:{path}:{key}
```

O escopo inclui o `sub` do JWT para prevenir que dois usuários com a mesma `Idempotency-Key` recebam respostas cruzadas (proteção IDOR).

**Resposta de formato inválido (400):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Idempotency-Key inválida.",
  "status": 400,
  "detail": "O header Idempotency-Key deve ser um UUID v4 (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx) ou uma string alfanumérica de 1 a 64 caracteres (A-Z, a-z, 0-9, hífens, underscores)."
}
```

### JWT e claims

O token JWT deve conter o claim `sub` (ou `ClaimTypes.NameIdentifier`). Esse valor é usado como:
- `criadoPor` ao criar cenários.
- Componente do escopo da `Idempotency-Key`.

---

## 6. Ownership e edição por outros usuários

Decisão D-6 (Q1): o domínio **não impõe ownership exclusivo** sobre cenários. Qualquer membro da tesouraria com policy `Escrita` pode editar qualquer cenário, independentemente de quem o criou. O campo `criadoPor` existe apenas para rastreabilidade de auditoria.
