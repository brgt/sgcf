# SPEC — Módulo de Cotações (MVP)

> **Status:** Draft para aprovação
> **Data:** 2026-05-16
> **Autor:** Análise técnica colaborativa (PO + arquitetura)
> **Versão:** v1.0
> **Modalidades MVP:** FINIMP (somente)

---

## 1. Objetivo

Permitir que o SGCF registre e compare propostas de empréstimo recebidas de múltiplos bancos para uma mesma necessidade de captação, convertendo a melhor proposta em contrato e mensurando a economia obtida na negociação entre cotação inicial e fechamento.

### 1.1. Personas

| Persona                    | Necessidade                                                                                     |
| -------------------------- | ----------------------------------------------------------------------------------------------- |
| **Operador de Tesouraria** | Registrar propostas recebidas dos bancos, comparar lado a lado, aceitar e converter em contrato |
| **Gerente Financeiro**     | Visualizar economia total negociada por período e justificar escolhas em comitê                 |
| **Auditor**                | Rastrear quem aceitou qual proposta, quando, e qual a diferença vs contrato fechado             |

### 1.2. Problema atual

Hoje as cotações de FINIMP são feitas por planilha/email, sem rastreabilidade, sem cálculo padronizado de CET multi-moeda, sem comparação estruturada e sem registro de economia negociada. A escolha do banco depende de memória do operador e não há indicador agregado de valor gerado pela função de tesouraria.

### 1.3. Métricas de sucesso

- 100% das operações FINIMP novas passam pelo fluxo de cotação antes de virar contrato.
- Tempo médio de "captura de proposta → escolha → contrato" reduzido em pelo menos 30% vs processo atual.
- Relatório de economia negociada disponível mensalmente, com discriminação por banco e por modalidade.

---

## 2. Glossário

| Termo                             | Definição                                                                                                                                   |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| **Cotação**                       | Agregado raiz: pedido interno de captação que será enviado a um ou mais bancos                                                              |
| **Proposta**                      | Resposta de um banco específico a uma cotação, contendo taxa, prazo, garantias e condições                                                  |
| **CET (Custo Efetivo Total)**     | Custo total da operação anualizado, em moeda funcional (BRL), considerando taxa nominal, IOF, spread, custo de NDF e rendimento de garantia |
| **PTAX D0**                       | Taxa de câmbio de referência do BACEN do dia útil anterior à data-base da cotação                                                           |
| **NDF** (Non-Deliverable Forward) | Instrumento de hedge cambial; quando exigido pelo banco, seu custo entra no CET                                                             |
| **Limite Operacional**            | Valor máximo que pode ser tomado em determinado banco para determinada modalidade                                                           |
| **Moeda Funcional**               | BRL — moeda de comparação para ranking entre propostas multi-moeda                                                                          |
| **Economia Negociada**            | Diferença entre o CET da proposta inicial recebida e o CET do contrato efetivamente assinado, em BRL, equalizada por prazo via CDI          |
| **Snapshot**                      | Cópia imutável do estado de uma proposta ou contrato em um momento específico, usada para auditoria                                         |

---

## 3. Modelo de Domínio

### 3.1. Diagrama lógico

```
┌────────────────────────────────────────────────────┐
│                    Cotacao                          │
│  (aggregate root)                                   │
│  - Id                                               │
│  - CodigoInterno (ex: COT-2026-0001)                │
│  - Modalidade (Finimp no MVP)                       │
│  - ValorAlvoBRL (valor desejado em moeda funcional) │
│  - PrazoMaximoDias                                  │
│  - DataAbertura                                     │
│  - DataPtaxReferencia                               │
│  - PtaxUsadaUsdBrl                                  │
│  - Status (ver §4)                                  │
│  - PropostaAceitaId? (Guid?)                        │
│  - ContratoGeradoId? (Guid?)                        │
│  - AceitaPor (sub do operador, quando status=Aceita)│
│  - DataAceitacao                                    │
│  - Observacoes                                      │
│  - CreatedAt, UpdatedAt, DeletedAt                  │
└─────────────────┬──────────────────────────────────┘
                  │
                  │ 1..*
                  ▼
┌────────────────────────────────────────────────────┐
│                  Proposta                           │
│  - Id                                               │
│  - CotacaoId                                        │
│  - BancoId                                          │
│  - MoedaOriginal (Brl, Usd, Eur, Cny, Jpy)          │
│  - ValorOferecidoMoedaOriginal                      │
│  - TaxaAaPercentual                                 │
│  - IofPercentual                                    │
│  - SpreadAaPercentual                               │
│  - PrazoDias                                        │
│  - EstruturaAmortizacao (Bullet, Price, Sac)        │
│  - PeriodicidadeJuros                               │
│  - ExigeNdf (bool)                                  │
│  - CustoNdfAaPercentual? (preenchido se ExigeNdf)   │
│  - GarantiaExigida (descritivo)                     │
│  - ValorGarantiaExigidaBRL                          │
│  - GarantiaEhCdbCativo (bool)                       │
│  - RendimentoCdbAaPercentual? (se for CDB)          │
│  - CetCalculadoAaPercentual (calculado, cache)      │
│  - ValorTotalEstimadoBRL (calculado, cache)         │
│  - DataCaptura                                      │
│  - DataValidadeMercado? (snapshot do dólar/NDF)     │
│  - Status (Recebida, Aceita, Recusada, Expirada)    │
│  - MotivoRecusa?                                    │
└────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────┐
│             LimiteBanco                             │
│  (aggregate independente)                           │
│  - Id                                               │
│  - BancoId                                          │
│  - Modalidade                                       │
│  - ValorLimiteBRL                                   │
│  - ValorUtilizadoBRL (recalculado de contratos ativos) │
│  - ValorDisponivelBRL (computed: limite - utilizado)│
│  - DataVigenciaInicio, DataVigenciaFim?             │
│  - Observacoes                                      │
│  - CreatedAt, UpdatedAt                             │
└────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────┐
│         EconomiaNegociacao                          │
│  (criada no momento de "Convertida em Contrato")    │
│  - Id                                               │
│  - CotacaoId                                        │
│  - ContratoId                                       │
│  - SnapshotPropostaJson (jsonb, imutável)           │
│  - SnapshotContratoJson (jsonb, imutável)           │
│  - CetPropostaAaPercentual                          │
│  - CetContratoAaPercentual                          │
│  - EconomiaBRL (positivo = economia, negativo = perda) │
│  - EconomiaAjustadaCdiBRL (equalizada por prazo)    │
│  - DataReferenciaCdi                                │
│  - CreatedAt                                        │
└────────────────────────────────────────────────────┘
```

### 3.2. Invariantes do agregado `Cotacao`

1. `ValorAlvoBRL > 0`.
2. `PrazoMaximoDias >= 1`.
3. `Status` segue máquina de estados definida em §4 (transições inválidas lançam exceção).
4. Apenas uma proposta pode estar com `Status = Aceita` por cotação.
5. Cotação só transiciona para `Convertida` se houver proposta aceita e `ContratoGeradoId` for atribuído.
6. `AceitaPor` é obrigatório quando `Status >= Aceita`.
7. `DataPtaxReferencia` deve ser dia útil anterior à `DataAbertura`.
8. Não permite enviar proposta a banco que não tenha `LimiteDisponivelBRL >= ValorAlvoBRL` para a modalidade da cotação.

### 3.3. Invariantes do agregado `Proposta`

1. Pertence a exatamente uma `Cotacao`.
2. `TaxaAaPercentual >= 0`.
3. `PrazoDias >= 1`.
4. Se `ExigeNdf = true`, `CustoNdfAaPercentual` é obrigatório.
5. Se `GarantiaEhCdbCativo = true`, `RendimentoCdbAaPercentual` é obrigatório.
6. `CetCalculadoAaPercentual` é recalculado em toda alteração da proposta (cache invalidado).
7. Status `Aceita` só é permitido se a `Cotacao` parent não tem outra proposta aceita.

---

## 4. Ciclo de Vida da Cotação

```
                  ┌────────┐
       Criar      │Rascunho│      Editar/Adicionar bancos
       ─────────►│         │◄────┐
                  └────┬────┘     │
                       │ Enviar   │
                       ▼          │
                  ┌──────────────┐│
                  │  Em Captação │┘   Registrar propostas
                  │              ├───────────────────────┐
                  └──────┬───────┘                       │
                         │ Todas propostas recebidas     │
                         │ (ou operador encerra captação)│
                         ▼                               │
                  ┌──────────────┐                       │
              ┌──┤  Comparada   │◄──────────────────────┘
              │   └──────┬───────┘
              │          │ Aceitar proposta
              │          ▼
   Expirar    │   ┌──────────────┐
   ou Recusar │   │   Aceita     │
   tudo       │   └──────┬───────┘
              │          │ Converter em contrato
              ▼          ▼
       ┌──────────┐  ┌──────────────┐
       │ Recusada │  │ Convertida   │  ◄── EconomiaNegociacao registrada aqui
       │          │  │ em Contrato  │
       └──────────┘  └──────────────┘
```

### 4.1. Transições válidas

| De         | Para       | Comando                                                                       |
| ---------- | ---------- | ----------------------------------------------------------------------------- |
| Rascunho   | EmCaptacao | `EnviarCotacaoCommand`                                                        |
| Rascunho   | Recusada   | `CancelarCotacaoCommand`                                                      |
| EmCaptacao | Comparada  | `EncerrarCaptacaoCommand` (manual ou quando todas propostas estão `Recebida`) |
| EmCaptacao | Recusada   | `CancelarCotacaoCommand`                                                      |
| Comparada  | Aceita     | `AceitarPropostaCommand`                                                      |
| Comparada  | Recusada   | `CancelarCotacaoCommand`                                                      |
| Aceita     | Convertida | `ConverterEmContratoCommand`                                                  |
| Aceita     | Comparada  | `DesfazerAceitacaoCommand` (apenas se ainda não convertida)                   |

Status finais (sem saída): `Convertida`, `Recusada`.

### 4.2. Auditoria

Toda transição registra evento no `audit_log` existente, com:

- `entity = "Cotacao"`
- `entity_id = Id`
- `operation = "STATE_TRANSITION"`
- `payload` JSON com `de`, `para`, `motivo`, `actor_sub`
- Para `AceitarPropostaCommand`: payload adicional com `proposta_id` aceita.

---

## 5. Cálculos Centrais

### 5.1. CET de uma proposta

Reutiliza o motor de amortização existente em `Sgcf.Domain.Cronograma`:

```
1. Converter ValorAlvoBRL para MoedaOriginal usando PtaxUsadaUsdBrl
   (se MoedaOriginal != BRL, aplicar cross-rate via USD).
2. Projetar cronograma hipotético usando:
     - Principal = ValorOferecido na MoedaOriginal
     - Taxa = TaxaAaPercentual + SpreadAaPercentual
     - Prazo = PrazoDias
     - Estrutura = EstruturaAmortizacao
     - Periodicidade = PeriodicidadeJuros
3. Adicionar custos extras ao fluxo:
     - IOF (sobre principal)
     - Custo NDF (se ExigeNdf): aplica sobre principal × prazo
     - Rendimento CDB cativo (se GarantiaEhCdbCativo):
       SUBTRAI do custo total (rendimento reduz o custo efetivo)
4. Converter cada fluxo de caixa projetado para BRL usando PtaxUsadaUsdBrl.
5. Calcular TIR do fluxo em BRL → CET anualizado.
```

A precisão do cálculo depende do motor já existente; nenhuma fórmula nova é introduzida. O CET é cacheado em `CetCalculadoAaPercentual` e invalidado a cada alteração da proposta.

### 5.2. Economia negociada (equalizada por CDI)

Cenário: proposta A do BB tem CET de 5% a.a. e prazo 180 dias; contrato fechado com BB tem CET de 4,8% a.a. e prazo 180 dias.

```
Economia bruta = (CET_proposta - CET_contrato) × ValorPrincipal × (Prazo/360)
```

Cenário com prazos diferentes (proposta vs contrato fechado mudou prazo):

```
1. Calcular VPL do fluxo da proposta usando CDI como taxa de desconto.
2. Calcular VPL do fluxo do contrato fechado usando CDI como taxa de desconto.
3. Economia = VPL_proposta - VPL_contrato.
```

Snapshot da curva de CDI usada fica em `EconomiaNegociacao.DataReferenciaCdi`. CDI é consultado do mesmo serviço de cotação já existente, ou cadastro manual no MVP.

### 5.3. Comparação de propostas com prazos diferentes (motivo "tela comparativa")

Para a tela de comparação, exibir lado a lado **três métricas por proposta**:

1. **Taxa nominal anualizada** (TaxaAa + Spread) — o que o banco oferece "de cara".
2. **CET** — métrica padrão regulada, comparável.
3. **Custo Total Equivalente em BRL para o prazo da cotação** — equaliza propostas com prazos diferentes trazendo o fluxo a valor presente via CDI.

A coluna 3 é a única que permite ranking matemático puro; as três juntas permitem ao operador conversar com o banco ("seu CET é melhor mas seu prazo é mais curto").

---

## 6. Casos de Uso (Commands e Queries)

### 6.1. Commands (escrita)

| Command                          | Descrição                                                   | Política |
| -------------------------------- | ----------------------------------------------------------- | -------- |
| `CriarCotacaoCommand`            | Cria cotação em Rascunho                                    | Escrita  |
| `AdicionarBancoNaCotacaoCommand` | Adiciona banco-alvo (sem proposta ainda); valida limite     | Escrita  |
| `RemoverBancoDaCotacaoCommand`   | Remove banco da cotação (apenas em Rascunho/EmCaptacao)     | Escrita  |
| `EnviarCotacaoCommand`           | Rascunho → EmCaptacao                                       | Escrita  |
| `RegistrarPropostaCommand`       | Registra proposta recebida do banco; recalcula CET          | Escrita  |
| `AtualizarPropostaCommand`       | Edita proposta antes de aceitação; recalcula CET            | Escrita  |
| `EncerrarCaptacaoCommand`        | EmCaptacao → Comparada (manual)                             | Escrita  |
| `AceitarPropostaCommand`         | Aceita uma proposta; grava `AceitaPor`                      | Escrita  |
| `DesfazerAceitacaoCommand`       | Aceita → Comparada (se ainda não convertida)                | Escrita  |
| `ConverterEmContratoCommand`     | Aceita → Convertida; cria `Contrato` + `EconomiaNegociacao` | Escrita  |
| `CancelarCotacaoCommand`         | Move para Recusada com motivo                               | Escrita  |
| `RefreshCotacaoMercadoCommand`   | Re-snapshot de PTAX/NDF para cotação ativa                  | Escrita  |

### 6.2. Queries (leitura)

| Query                       | Descrição                                                                                  | Política  |
| --------------------------- | ------------------------------------------------------------------------------------------ | --------- |
| `ListCotacoesQuery`         | Lista paginada com filtros (status, modalidade, banco, período)                            | Leitura   |
| `GetCotacaoQuery`           | Detalhe de uma cotação com todas as propostas                                              | Leitura   |
| `CompararPropostasQuery`    | Tabela comparativa lado a lado: taxa nominal, CET, custo total equivalente, garantias, NDF | Leitura   |
| `GetEconomiaPeriodoQuery`   | Relatório agregado de economia por período (mês/ano), por banco, por modalidade            | Leitura   |
| `ListLimitesBancoQuery`     | Lista limites operacionais por banco/modalidade com utilizado/disponível                   | Leitura   |
| `GetCotacaoAuditTrailQuery` | Trilha de auditoria específica da cotação (todas transições, quem aceitou)                 | Auditoria |

---

## 7. Endpoints REST Propostos

Convenção: prefixo `/api/v1/cotacoes/`.

### 7.1. Cotações

| Verbo    | Rota                                         | Descrição                        |
| -------- | -------------------------------------------- | -------------------------------- |
| `POST`   | `/api/v1/cotacoes`                           | Cria cotação                     |
| `GET`    | `/api/v1/cotacoes?page=&status=&modalidade=` | Lista paginada                   |
| `GET`    | `/api/v1/cotacoes/{id}`                      | Detalhe                          |
| `PATCH`  | `/api/v1/cotacoes/{id}`                      | Atualiza campos editáveis        |
| `DELETE` | `/api/v1/cotacoes/{id}`                      | Soft delete (apenas em Rascunho) |
| `POST`   | `/api/v1/cotacoes/{id}/bancos`               | Adiciona banco-alvo              |
| `DELETE` | `/api/v1/cotacoes/{id}/bancos/{bancoId}`     | Remove banco-alvo                |
| `POST`   | `/api/v1/cotacoes/{id}/enviar`               | Rascunho → EmCaptacao            |
| `POST`   | `/api/v1/cotacoes/{id}/encerrar-captacao`    | EmCaptacao → Comparada           |
| `POST`   | `/api/v1/cotacoes/{id}/cancelar`             | → Recusada                       |
| `POST`   | `/api/v1/cotacoes/{id}/refresh-mercado`      | Re-snapshot PTAX/NDF             |
| `GET`    | `/api/v1/cotacoes/{id}/comparativo`          | Tabela comparativa               |
| `GET`    | `/api/v1/cotacoes/{id}/auditoria`            | Trilha de auditoria              |

### 7.2. Propostas

| Verbo   | Rota                                                              | Descrição                           |
| ------- | ----------------------------------------------------------------- | ----------------------------------- |
| `POST`  | `/api/v1/cotacoes/{id}/propostas`                                 | Registra proposta recebida          |
| `PATCH` | `/api/v1/cotacoes/{id}/propostas/{propostaId}`                    | Atualiza proposta                   |
| `POST`  | `/api/v1/cotacoes/{id}/propostas/{propostaId}/aceitar`            | Aceita proposta                     |
| `POST`  | `/api/v1/cotacoes/{id}/propostas/{propostaId}/desfazer-aceitacao` | Reverte aceitação                   |
| `POST`  | `/api/v1/cotacoes/{id}/converter-em-contrato`                     | Converte cotação aceita em contrato |

### 7.3. Limites

| Verbo   | Rota                                         | Descrição               |
| ------- | -------------------------------------------- | ----------------------- |
| `GET`   | `/api/v1/limites-banco?bancoId=&modalidade=` | Lista limites           |
| `POST`  | `/api/v1/limites-banco`                      | Cria limite             |
| `PATCH` | `/api/v1/limites-banco/{id}`                 | Atualiza limite (admin) |

### 7.4. Relatórios

| Verbo | Rota                                                        | Descrição                      |
| ----- | ----------------------------------------------------------- | ------------------------------ |
| `GET` | `/api/v1/cotacoes/economia?de=YYYY-MM&ate=YYYY-MM&bancoId=` | Relatório de economia agregado |

---

## 8. Estrutura de Código

Seguindo Clean Architecture já estabelecida no SGCF:

```
src/
├── Sgcf.Domain/
│   └── Cotacoes/
│       ├── Cotacao.cs                          (aggregate root)
│       ├── Proposta.cs                         (entity)
│       ├── StatusCotacao.cs                    (enum)
│       ├── StatusProposta.cs                   (enum)
│       ├── LimiteBanco.cs                      (aggregate root)
│       ├── EconomiaNegociacao.cs               (entity)
│       └── CalculadoraCet.cs                   (domain service puro)
│
├── Sgcf.Application/
│   └── Cotacoes/
│       ├── ICotacaoRepository.cs
│       ├── ILimiteBancoRepository.cs
│       ├── IEconomiaRepository.cs
│       ├── CotacaoDto.cs
│       ├── PropostaDto.cs
│       ├── ComparativoDto.cs
│       ├── EconomiaPeriodoDto.cs
│       ├── Commands/
│       │   ├── CriarCotacaoCommand.cs
│       │   ├── AdicionarBancoNaCotacaoCommand.cs
│       │   ├── EnviarCotacaoCommand.cs
│       │   ├── RegistrarPropostaCommand.cs
│       │   ├── AtualizarPropostaCommand.cs
│       │   ├── AceitarPropostaCommand.cs
│       │   ├── DesfazerAceitacaoCommand.cs
│       │   ├── ConverterEmContratoCommand.cs
│       │   ├── CancelarCotacaoCommand.cs
│       │   ├── RefreshCotacaoMercadoCommand.cs
│       │   └── EncerrarCaptacaoCommand.cs
│       └── Queries/
│           ├── ListCotacoesQuery.cs
│           ├── GetCotacaoQuery.cs
│           ├── CompararPropostasQuery.cs
│           ├── GetEconomiaPeriodoQuery.cs
│           └── ListLimitesBancoQuery.cs
│
├── Sgcf.Infrastructure/
│   └── Persistence/
│       ├── Configurations/
│       │   ├── CotacaoConfiguration.cs
│       │   ├── PropostaConfiguration.cs
│       │   ├── LimiteBancoConfiguration.cs
│       │   └── EconomiaNegociacaoConfiguration.cs
│       └── Repositories/
│           ├── CotacaoRepository.cs
│           ├── LimiteBancoRepository.cs
│           └── EconomiaRepository.cs
│
├── Sgcf.Api/
│   └── Controllers/
│       ├── CotacoesController.cs
│       ├── PropostasController.cs (alternativa: dentro de CotacoesController)
│       └── LimitesBancoController.cs
│
└── (Sgcf.Mcp e Sgcf.A2a — fora do escopo MVP, ver §11)
```

### 8.1. Tabelas PostgreSQL (schema `sgcf`)

| Tabela                | Chave          | Notas                                                 |
| --------------------- | -------------- | ----------------------------------------------------- |
| `cotacao`             | `id` (uuid v7) | `codigo_interno` único                                |
| `proposta`            | `id` (uuid v7) | FK para `cotacao`                                     |
| `limite_banco`        | `id` (uuid v7) | UQ (`banco_id`, `modalidade`, `data_vigencia_inicio`) |
| `economia_negociacao` | `id` (uuid v7) | UQ `cotacao_id` (1:1); FK `contrato_id`               |

Migration EF Core: `S3Cotacoes` (próxima na sequência).

---

## 9. Estilo de Código

Segue o `CLAUDE.md` do projeto:

- **Money:** valores monetários sempre em `Money` value object, nunca `decimal` cru.
- **Datas:** `LocalDate` (NodaTime) para datas calendário; `Instant` para timestamps de auditoria.
- **Rounding:** `MidpointRounding.AwayFromZero` em todo cálculo financeiro.
- **Cálculos:** `CalculadoraCet` é função pura — sem I/O, sem estado.
- **EF:** apenas em `Sgcf.Infrastructure`. Domínio livre de atributos EF.
- **Idiomas:** termos de domínio em português (`Cotacao`, `Proposta`, `Limite`); termos técnicos em inglês (`Repository`, `Handler`).
- **Status enums:** `byte` underlying type, ordens fixas, sem renumeração futura (compatibilidade com migrations).

---

## 10. Estratégia de Testes

### 10.1. Pirâmide

| Camada               | Quantidade alvo | Foco                                                                                       |
| -------------------- | --------------- | ------------------------------------------------------------------------------------------ |
| **Unit Domain**      | ~60 testes      | Invariantes do agregado, máquina de estados, `CalculadoraCet` (property-based com FsCheck) |
| **Unit Application** | ~30 testes      | Cada Command/QueryHandler com mocks de repositório                                         |
| **Integration**      | ~10 testes      | Repositórios com Testcontainers PostgreSQL                                                 |
| **API/E2E**          | ~5 fluxos       | Fluxos críticos via `WebApplicationFactory`                                                |
| **Golden Dataset**   | 3-5 cenários    | Cálculo de CET multi-moeda + economia equalizada                                           |

### 10.2. Cenários golden obrigatórios

1. **FINIMP USD com BB vs Bradesco** — três propostas comparadas, ranking por CET, economia calculada.
2. **FINIMP CNY com BB + NDF obrigatório** — custo NDF entra no CET.
3. **FINIMP USD com garantia CDB cativo** — rendimento do CDB reduz CET.
4. **Comparação com prazos diferentes** — equalização via CDI.
5. **Economia mensal agregada** — soma de várias cotações convertidas em mesmo mês.

### 10.3. Property-based testing (CalculadoraCet)

- CET sempre ≥ taxa nominal (não pode ser menor).
- CET com NDF obrigatório > CET sem NDF (mesmas outras condições).
- CET com garantia CDB rendendo > 0 < CET sem garantia rendendo.
- CET com prazo maior, mantendo outras variáveis, gera VPL maior em BRL.

---

## 11. Boundaries (Out of Scope MVP)

**Explicitamente fora do MVP:**

1. **Integração com agentes de IA.** Parser de PDF/email, recomendador contextual, explicador — todos catalogados em `docs/agentes-ia/IDEIAS-AGENTES.md` para sprints futuras.
2. **Modalidades além de FINIMP.** REFINIMP, Lei 4131, NCE, BalcãoCaixa e FGI ficam para sprints subsequentes (reutilizando estrutura).
3. **Workflow multi-nível de aprovação.** Aceitação direta pelo operador; log da aprovação satisfaz auditoria.
4. **Notificações automáticas.** Sem email/push quando NDF varia ou cotação fica "velha" — operador olha quando precisa.
5. **Integração com bancos via API/Open Finance.** Captura 100% manual no MVP.
6. **Multi-tenancy.** Cotações são de uma única organização.
7. **Versionamento de cotação.** Editar uma proposta sobrescreve; histórico via `audit_log` existente.
8. **Hedge separado da proposta.** Hedge segue modelo existente em contratos; cotação registra apenas se o banco "exige NDF" e o custo.
9. **Splitting de cotação em múltiplos contratos.** 1:1 fixo no MVP.
10. **Ingestão automática de PTAX do BACEN.** No MVP, PTAX é cadastrada manualmente ou consultada do cadastro existente (verificar §13).

---

## 12. Sempre / Pergunte Primeiro / Nunca

### 12.1. Sempre

- Calcular CET reutilizando `Sgcf.Domain.Cronograma` (motor existente).
- Persistir snapshot imutável em `EconomiaNegociacao` no momento da conversão.
- Registrar `AceitaPor` (sub do JWT do operador) em toda aceitação.
- Registrar transições de estado no `audit_log`.
- Validar limite disponível antes de adicionar banco a uma cotação.
- Usar `Money` em todos os valores financeiros.

### 12.2. Pergunte primeiro

- Mudar fórmula de cálculo de CET.
- Adicionar novo status ao ciclo de vida.
- Permitir saída de status final (Convertida/Recusada).
- Reduzir nível de auditoria de qualquer operação.

### 12.3. Nunca

- Permitir aceitar mais de uma proposta por cotação.
- Permitir conversão sem proposta aceita.
- Alterar `EconomiaNegociacao` após criação (snapshot imutável).
- Adicionar IA no caminho determinístico (cálculo de CET, ranking matemático, economia).
- Excluir cotação que tenha contrato gerado (apenas soft delete em Rascunho).
- Calcular CET sem PTAX D-1 explícita registrada.

---

## 13. Decisões Pendentes (a confirmar antes do `/plan`)

| #   | Pergunta                                                                      | Default proposto se não respondido                                       |
| --- | ----------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| 1   | Fonte da PTAX D-1                                                             | Cadastro manual via tela admin no MVP; integração BACEN em sprint futura |
| 2   | Fonte da curva CDI usada na equalização                                       | Cadastro manual; integração ANBIMA futura                                |
| 3   | Validade temporal do snapshot de mercado (após quantas horas mostrar alerta?) | 4 horas para cotações com NDF obrigatório; 24 horas para demais          |
| 4   | Formato do `CodigoInterno` da cotação                                         | `COT-{ano}-{seq:05d}` (ex: COT-2026-00001)                               |
| 5   | Granularidade do relatório de economia                                        | Mensal + anual no MVP; semanal/diário sob demanda                        |
| 6   | Permissão para editar limite operacional                                      | Apenas role `Admin`                                                      |
| 7   | Cotação pode ser reaberta após Recusada?                                      | Não no MVP; criar nova cotação                                           |

---

## 14. Riscos e Mitigações

| Risco                                                              | Probabilidade | Impacto | Mitigação                                                                    |
| ------------------------------------------------------------------ | ------------- | ------- | ---------------------------------------------------------------------------- |
| Motor de amortização existente não suporta multi-moeda em projeção | Média         | Alto    | Validar precoce na Sprint; extensão localizada se necessário                 |
| Cálculo de CET diverge do esperado pelos bancos                    | Alta          | Médio   | Implementar Golden Dataset com casos validados pelo time financeiro          |
| Operador esquece de cadastrar PTAX antes de cotar                  | Alta          | Médio   | Validação na API + tela de PTAX como pré-requisito visível                   |
| Limite operacional desatualizado leva a cotação inválida           | Média         | Alto    | Recalcular `ValorUtilizadoBRL` em background diário; permitir refresh manual |
| Economia ajustada por CDI parece "complicada" para operador        | Média         | Baixo   | Exibir economia bruta E ajustada lado a lado                                 |

---

## 15. Dependências Externas (do próprio SGCF)

- `Sgcf.Domain.Cronograma` — motor de amortização (reutilizado para projeção).
- `Sgcf.Domain.Bancos` — entidade `Banco`.
- `Sgcf.Domain.Contratos` — entidade `Contrato` (cotação convertida cria contrato existente).
- `Sgcf.Domain.Auditoria` — log de auditoria já existente.
- `Sgcf.Application.Cotacoes` (atual) — `ParametroCotacao` existente (diferente conceito, atenção: nomear o novo módulo como `Sgcf.Application.CotacoesPropostas` se houver conflito de nome; ou rebatizar o existente).

> ⚠️ **Conflito de nome detectado:** já existe `Sgcf.Application.Cotacoes` ligado a `ParametroCotacao` (regras de qual cotação cambial aplicar a cada banco/modalidade). Decidir antes de iniciar implementação: (a) renomear módulo atual para `Sgcf.Application.ParametrosCambiais`; ou (b) usar `Sgcf.Application.CotacoesBancarias` para o novo módulo. **Recomendação:** opção (a) — `ParametroCotacao` é sobre câmbio, não sobre proposta de empréstimo.

---

## 16. Critérios de Aceitação (alto nível)

A SPEC é considerada implementada quando:

1. ✅ Operador consegue criar cotação FINIMP em rascunho via API.
2. ✅ Operador consegue adicionar 1-N bancos à cotação, com validação de limite.
3. ✅ Operador consegue enviar cotação (Rascunho → EmCaptacao).
4. ✅ Operador consegue registrar propostas com taxa, IOF, prazo, garantia, NDF.
5. ✅ Sistema calcula CET automaticamente em cada proposta, em BRL.
6. ✅ Endpoint comparativo retorna as três métricas lado a lado para todas as propostas.
7. ✅ Operador aceita uma proposta; `AceitaPor` é registrado.
8. ✅ Operador converte em contrato; `Contrato` é criado com referência à cotação; `EconomiaNegociacao` é persistida com snapshot imutável.
9. ✅ Relatório de economia agregada por período retorna valores corretos validados por Golden Dataset.
10. ✅ Trilha de auditoria mostra toda transição com timestamp e ator.
11. ✅ Cobertura de testes: ≥90% Domain, ≥80% Application, ≥70% Infrastructure.
12. ✅ Zero regressão nos testes existentes (364 + novos).

---

## 17. Próximos Passos

1. **Aprovação desta SPEC** pelo PO/usuário.
2. **Resposta às 7 decisões pendentes** (§13) ou aceite dos defaults.
3. **Resolução do conflito de nome** (§15).
4. **`/plan`** — quebra em tarefas verticais com TDD por slice.
5. **`/build`** — implementação incremental.

---

## 18. Histórico

| Data       | Versão | Mudança                                       |
| ---------- | ------ | --------------------------------------------- |
| 2026-05-16 | v1.0   | Draft inicial após levantamento de requisitos |
