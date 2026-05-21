# Módulo Simulações + Quadro da Dívida — Documentação da API

**Versão:** v0.10.0  
**Data:** 2026-05-20  
**Audiência:** Desenvolvedores front-end integrando com o SGCF Backend (.NET 11)

---

## O que este módulo faz

O módulo Simulações permite que a equipe de tesouraria crie **cenários hipotéticos de contratação** — conjuntos nomeados de captações futuras que ainda não foram formalizadas — e visualize como esses cenários alterariam o **Quadro da Dívida** mês a mês ao longo de um ano civil.

Um cenário típico recebe um nome descritivo como "Realista 2026" ou "Otimista Q3" e contém uma lista de simulações de contratação (cada uma equivalente a um contrato bancário futuro). O sistema calcula o cronograma de amortização e juros de cada simulação usando o mesmo motor financeiro dos contratos reais, garantindo que os números sejam comparáveis.

O **Quadro da Dívida** é a visão central da tesouraria: uma tabela de 12 linhas (uma por mês) que mostra o saldo devedor de cada banco no início e no fim do mês, as amortizações de principal previstas, as novas captações (reais ou simuladas) e o share percentual de cada banco no total. Quando você passa um `cenarioId`, a projeção incorpora as captações hipotéticas do cenário sem alterar os dados reais.

### Casos de uso principais

- Criar um cenário com várias captações hipotéticas e visualizar o impacto no quadro da dívida.
- Comparar até 5 cenários lado a lado com deltas mensais e anuais em relação ao primeiro (baseline).
- Pré-visualizar o cronograma financeiro de uma captação hipotética sem persistir nada (endpoint stateless).
- Consultar e configurar o tetão mensal — limite de movimentação que gera alertas quando excedido.

---

## Conceitos centrais

| Conceito | Tipo | Descrição |
|---|---|---|
| `CenarioSimulacao` | Agregado raiz | Conjunto nomeado de captações hipotéticas. Lifecycle: `Rascunho` → `Ativo` → `Arquivado`. |
| `SimulacaoContratacao` | Entidade filha | Uma única captação hipotética dentro de um cenário. Contém todos os campos para gerar um cronograma financeiro. |
| `QuadroDividaProjecao` | Valor de domínio | 12 meses projetados calculados pela função pura `ProjetorSaldoMensal`. |
| `EventoProjecao` | Valor de domínio | Unidade de entrada do projetor: `(BancoId, Data, Tipo, ValorBrl)`. Representa amortizações de principal ou captações. |
| `ParametroSistema` | Singleton | Configurações globais do sistema. No MVP contém o `TetaoMensalCapacidadeBrl`. |

---

## Índice

| Documento | Conteúdo |
|---|---|
| [api-reference.md](./api-reference.md) | Todos os endpoints REST com payloads, status codes e exemplos |
| [dtos.md](./dtos.md) | Todos os DTOs request/response com tipos, constraints e valores de enum |
| [lifecycle-e-arquitetura.md](./lifecycle-e-arquitetura.md) | Lifecycle do `CenarioSimulacao`, decisões AD-1..AD-12, camadas, cache e autorização |
| [quadro-divida.md](./quadro-divida.md) | Cálculo de projeção mensal, tetão, alertas e integração MCP |
| [integracao-frontend.md](./integracao-frontend.md) | Quatro fluxos completos com exemplos de código |

---

## Rotas disponíveis (sumário)

### `SimulacoesController` — `/api/v1/simulacoes`

| Método | Path | Policy | Descrição |
|---|---|---|---|
| POST | `/cronograma-hipotetico` | Escrita | Pré-visualiza cronograma sem persistir |
| POST | `/cenarios` | Escrita | Cria cenário em Rascunho |
| GET | `/cenarios` | Leitura | Lista cenários com filtros |
| GET | `/cenarios/{id}` | Leitura | Detalhe completo do cenário |
| PATCH | `/cenarios/{id}` | Escrita | Atualiza nome/descrição/anoBase |
| POST | `/cenarios/{id}/ativar` | Escrita | Transita Rascunho → Ativo |
| POST | `/cenarios/{id}/arquivar` | Gerencial | Transita Ativo → Arquivado (irreversível) |
| POST | `/cenarios/{id}/duplicar` | Escrita | Copia o cenário como novo Rascunho |
| DELETE | `/cenarios/{id}` | Escrita | Soft delete |
| POST | `/cenarios/{id}/simulacoes` | Escrita | Adiciona simulação ao cenário |
| PATCH | `/cenarios/{id}/simulacoes/{simId}` | Escrita | Atualiza simulação (substituição total) |
| DELETE | `/cenarios/{id}/simulacoes/{simId}` | Escrita | Remove simulação |
| GET | `/cenarios/{id}/quadro-divida` | Leitura | Quadro da dívida com cenário aplicado |
| POST | `/comparar` | Leitura | Comparativo de até 5 cenários |

### `PainelController` — `/api/v1/painel`

| Método | Path | Policy | Descrição |
|---|---|---|---|
| GET | `/quadro-divida` | Leitura | Quadro da dívida (com ou sem cenário) |

### `ParametrosSistemaController` — `/api/v1/parametros-sistema`

| Método | Path | Policy | Descrição |
|---|---|---|---|
| GET | `` | Leitura | Parâmetros globais do sistema |
| PATCH | `/tetao-mensal` | Admin | Configura o tetão mensal |

---

## Convenções para o front-end

- **Autenticação:** `Authorization: Bearer <token>` em todas as requisições.
- **Idempotência:** Envie `Idempotency-Key: <uuid-v4>` nos POSTs de criação (`/cenarios`, `/cenarios/{id}/duplicar`, `/cenarios/{id}/simulacoes`). Formato aceito: UUID v4 canônico ou string alfanumérica de 1–64 caracteres (A-Z, a-z, 0-9, hífens, underscores). Keys inválidas retornam 400.
- **Money:** Todos os valores monetários são `decimal` com até 2 casas decimais, arredondados com HalfUp (regulação BR). Nunca rearredonde no front-end.
- **Datas:** ISO 8601 em formato `"YYYY-MM-DD"`. O backend assume fuso `America/Sao_Paulo` para comparações de "hoje" vs datas previstas. Envie datas como strings neste formato.
- **Enums:** Enviados e recebidos como strings PascalCase exatas. Exemplos: `"Brl"`, `"Finimp"`, `"Fixa"`, `"Rascunho"`.
- **Soft delete:** Cenários com `DeletedAt` preenchido não aparecem nas listagens. Um `GET /cenarios/{id}` de cenário deletado retorna 404.

---

## Dependências de serviço

- **PostgreSQL 16** — persistência dos cenários e simulações.
- **Redis 7** — cache de cronograma hipotético com chave `sim:cronograma:{cenarioId}:{simulacaoId}:v{version}` e TTL de 60 segundos. Invalidado automaticamente a cada mutação de simulação (via incremento de `Version`).
