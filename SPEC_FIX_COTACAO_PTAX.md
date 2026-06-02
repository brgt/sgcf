# SPEC — Correção dos Bugs de Cotação / PTAX

**Empresa:** Proxys Comércio Eletrônico
**Sponsor / Product Owner:** Welysson Soares
**Versão:** v0.1 — 02/junho/2026
**Status:** Proposta — aguardando aprovação
**Escopo:** Correção de bug no backend SGCF (módulo Câmbio/Cotações) + endpoint manual de PTAX + alinhamento de fixtures + guia para o time de front-end
**Documento âncora:** `SPEC.md` (mestre). Este documento cobre apenas as correções abaixo.

---

## 1. Objetivo

### 1.1 O que vamos corrigir

1. **BUG-PTAX (central):** leituras de PTAX consultam o tipo lógico `PtaxD1` diretamente no repositório, mas o ingestor do BCB grava apenas `PtaxD0`/`SpotIntraday`. Resultado: em produção alimentada apenas pela ingestão automática, **todas** as leituras de `PtaxD1` retornam `null`, quebrando a criação de cotação, registro de proposta, refresh de mercado e ~14 consultas de Painel/Tesouraria/Jobs/MCP.
2. **PTAX-MANUAL:** não existe endpoint para cadastrar/corrigir uma cotação USD/BRL manualmente (hoje só via job do BCB ou SQL direto). Criar endpoint admin.
3. **FIXTURES:** os testes semeiam linhas `PtaxD1` manualmente — formato que o ingestor real nunca produz — mascarando o BUG-PTAX. Alinhar fixtures ao dado real (`PtaxD0`).
4. **FRONT-GUIA:** produzir um markdown orientando o time de front-end sobre o erro 400 em `parametros-cotacao` e como o front deve consultar o sistema para cadastrar/usar cotações.

### 1.2 Por que

A criação de cotações cambiais é função-núcleo do MVP (FINIMP/REFINIMP/4131). O bug torna o fluxo inoperante em produção real e só não aparece nos testes por causa das fixtures divergentes. Ver diagnóstico na conversa de 02/jun/2026.

### 1.3 Sucesso

- Com o banco alimentado **apenas** por `PtaxD0` (como faz o ingestor real), criar cotação cambial, registrar proposta e abrir os painéis cambiais funcionam.
- Existe endpoint admin para registrar PTAX manualmente (formato `PtaxD0`, coerente com o ingestor).
- Os testes deixam de semear `PtaxD1`; passam a semear `PtaxD0` e continuam verdes — o bug não pode reaparecer sem quebrar testes.
- Time de front recebe um guia claro do contrato de `parametros-cotacao` e do cadastro de cotações.

### 1.4 Usuários-alvo

Tesouraria (cria cotações), Admin/TI (cadastra PTAX manual em contingência), consumidores de Painel/Tesouraria/MCP, e o time de front-end (guia).

---

## 2. Decisões (confirmadas em 02/jun/2026)

| # | Decisão | Escolha |
|---|---|---|
| D1 | Abordagem do BUG-PTAX | **Rotear leituras pelo `IResolveTipoCotacaoService`**, centralizando a tradução `PtaxD1 → PtaxD0(D-1)` |
| D2 | Endpoint manual de PTAX | **Incluir** (admin) |
| D3 | Fixtures | **Alinhar** ao formato do ingestor (`PtaxD0`) |
| D4 | Erro 400 do front | **Backend permanece como está** (validação correta); produzir **guia em markdown** para o front |

---

## 3. Diagnóstico técnico (fundamenta a correção)

### 3.1 Como o dado é gravado vs lido

- **Gravação (produção):** `PtaxIngestor` (`src/Sgcf.Infrastructure/Bcb/PtaxIngestor.cs`) consome a API Olinda do BCB (`BcbPtaxClient.cs:60`) e grava **`PtaxD0`** (boletim de Fechamento) e **`SpotIntraday`**. Nunca grava `PtaxD1`.
- **Tradução correta (existente):** `CotacaoResolverService` (`src/Sgcf.Infrastructure/Cambio/CotacaoResolverService.cs:39-40`) já mapeia `PtaxD1 → PtaxD0` consultando `dataRef.PlusDays(-1)`.
- **Leitura quebrada:** ~16 chamadores usam `fxRepo.GetMaisRecenteAsync(moeda, TipoCotacao.PtaxD1, data, ct)` direto no repositório, que filtra `c.Tipo == PtaxD1` (`CotacaoFxRepository.cs`). Como nada grava `PtaxD1`, retorna sempre `null`.

### 3.2 Inventário de chamadores e semântica de data (auditoria)

> **Perfil A — pré-subtraem o dia** (passam uma data **já em D-1**):

| Chamador | Data passada hoje | Valor usado | Ajuste exigido |
|---|---|---|---|
| `CriarCotacaoCommand.cs:82-86` | `dataAbertura.PlusDays(-1)` | `ValorVenda` | Passar `dataAbertura` (sem `-1`) e deixar o resolver aplicar a regra D-1 |
| `RegistrarPropostaCommand.cs:264-267` | `cotacao.DataPtaxReferencia` (já é a data do fechamento usado) | cross-rate | Resolver por **`PtaxD0` na data exata** (sem novo `-1`), preservando a data travada na criação |

> **Perfil B — passam a data corrente** (`hoje`/`dataRef`) e usam **mid-rate** `(compra+venda)/2`:

`RefreshCotacaoMercadoCommand.cs:38-41`, `GetFluxoCaixaQuery.cs:218`, `GetPosicaoCaixaQuery.cs:160`, `GetHedgeEfetividadeQuery.cs:200`, `GetSensibilidadeIndexadoresQuery.cs:197`, `GetPainelDividaQueryHandler.cs:195`, `GetQuadroDividaQuery.cs:483`, `GetDashboardKpisQueryHandler.cs:303` (`dataReferencia`), `GetBreakdownModalidadeQueryHandler.cs:149`, `GetCalendarioVencimentosQueryHandler.cs:340`, `GetCurvaVencimentosQueryHandler.cs:210`, `GetInadimplenciaQueryHandler.cs:186`, `SimularCenarioCambialQueryHandler.cs:242`, `SimularAntecipacaoPortfolioQueryHandler.cs:268`, `RecalcularMtmJob.cs:156`, `SnapshotMensalJob.cs:153`, `DividaTools.cs:94` (MCP).

→ Para o Perfil B, a regra "PTAX D-1 relativa à data de referência R = fechamento `PtaxD0` mais recente **antes** de R" produz o resultado correto **sem alterar o argumento de data** — apenas trocando a chamada para o resolver.

### 3.3 Risco central: off-by-one

Se a tradução `PtaxD1 → PtaxD0(R-1)` for aplicada uniformemente, o **Perfil A** (que já subtraiu 1 dia) sofre **dupla subtração**. Por isso a correção **deve** normalizar a semântica de data por chamador (tabela 3.2). Este é o ponto mais sensível da entrega e tem critérios de aceite dedicados (§8).

---

## 4. Requisitos funcionais

### 4.1 BUG-PTAX — centralizar a resolução

- **RF-01** Adicionar ao `IResolveTipoCotacaoService` um método de resolução por **(moeda, tipoLógico, dataReferência)** que retorna a `CotacaoFx?` aplicando a tradução `PtaxD1 → PtaxD0` em `dataReferência.PlusDays(-1)`; para os demais tipos, consulta o próprio tipo em `dataReferência`. Retornar `CotacaoFx` (não mid-rate) para preservar a escolha de cada chamador (venda vs mid).
- **RF-02** A lógica de tradução hoje embutida em `CotacaoResolverService` (`:39-40`) passa a residir nesse método reutilizável; `ResolveAsync` (por banco/modalidade) reaproveita-o.
- **RF-03** Substituir, em todos os chamadores do Perfil B, `fxRepo.GetMaisRecenteAsync(moeda, PtaxD1, R, ct)` pela chamada ao resolver, **sem alterar** o argumento de data. Comportamento de mid-rate preservado.
- **RF-04** Ajustar os chamadores do Perfil A conforme a tabela 3.2 para evitar off-by-one:
  - `CriarCotacaoCommand`: passar `dataAbertura` (remover `PlusDays(-1)`); manter uso de `ValorVenda` e o cálculo de `DataPtaxReferencia` a partir do `Momento` retornado.
  - `RegistrarPropostaCommand`: resolver `PtaxD0` na data exata `DataPtaxReferencia` (sem nova subtração).
- **RF-05** As mensagens de erro de PTAX ausente permanecem (`CriarCotacaoCommand.cs:89`, `RefreshCotacaoMercadoCommand.cs:44`), mas passam a refletir a regra correta (fechamento D-1).

### 4.2 PTAX-MANUAL — endpoint admin

- **RF-06** Criar endpoint `POST /api/v1/cotacoes-fx` (policy **Admin**) que registra uma `CotacaoFx`. Corpo: `moedaBase`, `moedaQuote` (default BRL), `momento` (instante/data do fechamento), `tipo` (default `PtaxD0`), `valorCompra`, `valorVenda`, `fonte` (default `MANUAL`).
- **RF-07** O endpoint grava preferencialmente **`PtaxD0`** (coerente com o ingestor); a leitura D-1 resolve a partir dele. Usa `CotacaoFx.Criar` + `repo.UpsertAsync` (idempotente pela unique key `moeda_base, moeda_quote, momento, tipo`).
- **RF-08** Validação (FluentValidation): `valorCompra`/`valorVenda` > 0; `moedaQuote == BRL`; `tipo` ∈ enum; `momento` não no futuro (limite = agora, via `IClock`).
- **RF-09** `GET /api/v1/cotacoes-fx?moeda=USD&tipo=PtaxD0&ate=YYYY-MM-DD` para conferência (opcional, leitura).

### 4.3 FIXTURES — alinhar ao dado real

- **RF-10** Atualizar `CriarCotacaoRefinimpTests` e `RegistrarPropostaCommandHandlerTests` para semear **`PtaxD0`** (como o ingestor) e validar a resolução via resolver/mapeamento, em vez de semear `PtaxD1`.
- **RF-11** Adicionar teste de **off-by-one** garantindo que `CriarCotacao` (Perfil A) e um caso de Painel (Perfil B) resolvem a **mesma** data de fechamento esperada para a mesma referência.

### 4.4 FRONT-GUIA — documentação para o front-end

- **RF-12** Produzir `sgcf-backend/docs/api/GUIA_FRONT_COTACOES.md` cobrindo: (a) que `parametros-cotacao` configura **qual tipo** de cotação por banco/modalidade e **não** a taxa; (b) valores válidos de `TipoCotacao` (`PtaxD0, PtaxD1, SpotIntraday, Fixing`) e o contrato do 400 (`CreateParametroCommand.cs:19`); (c) como cadastrar a taxa USD/BRL (endpoint `POST /cotacoes-fx` ou ingestão automática); (d) `GET /parametros-cotacao/resolve?bancoId&modalidade`.

---

## 5. Comandos (build / test / run)

```bash
docker compose -f sgcf-backend/infra/dev/docker-compose.yml up -d
dotnet build sgcf-backend/sgcf-backend.sln

# Testes do módulo de câmbio/cotações (unit)
dotnet test sgcf-backend/tests/Sgcf.Application.Tests --filter "FullyQualifiedName~Cambio|FullyQualifiedName~Cotac"

# Integração (Testcontainers)
dotnet test sgcf-backend/tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~Cotac|FullyQualifiedName~CotacaoFx"

# Suíte completa
dotnet test sgcf-backend/sgcf-backend.sln
```

---

## 6. Estrutura do projeto (arquivos tocados)

| Camada | Arquivo | Mudança |
|---|---|---|
| Application | `Cambio/IResolveTipoCotacaoService.cs` | novo método de resolução (moeda, tipoLógico, dataRef) → `CotacaoFx?` |
| Infrastructure | `Cambio/CotacaoResolverService.cs` | extrair tradução D1→D0(D-1) para o novo método; `ResolveAsync` reaproveita |
| Application | `Cotacoes/Commands/CriarCotacaoCommand.cs` | usar resolver; passar `dataAbertura` (sem `-1`) |
| Application | `Cotacoes/Commands/RegistrarPropostaCommand.cs` | resolver `PtaxD0` na data exata |
| Application | `Cotacoes/Commands/RefreshCotacaoMercadoCommand.cs` | usar resolver |
| Application | `Painel/Queries/*` (10 handlers) e `Tesouraria/Queries/*` (3) e `Contratos/Queries/GetSensibilidadeIndexadoresQuery.cs` | trocar repo→resolver (sem mudar data) |
| Jobs | `Jobs/RecalcularMtmJob.cs`, `Jobs/SnapshotMensalJob.cs` | trocar repo→resolver |
| Mcp | `Tools/DividaTools.cs` | trocar repo→resolver |
| Api | `Controllers/CotacoesFxController.cs` (novo) + command/validator em `Application/Cambio/Commands` | endpoint manual de PTAX |
| Tests | `tests/Sgcf.Application.Tests/Cotacoes/*`, `tests/Sgcf.Api.IntegrationTests/*` | fixtures `PtaxD0` + off-by-one + endpoint |
| Docs | `docs/api/GUIA_FRONT_COTACOES.md` (novo) | guia do front |

> Restrições de camada (`CLAUDE.md`): `Sgcf.Mcp`/`Sgcf.A2a` **não** importam `Infrastructure`. O `IResolveTipoCotacaoService` vive em `Application` (interface) com implementação em `Infrastructure` — o MCP depende da interface, mantendo a regra.

## 7. Estilo de código

- `Money` para dinheiro; NodaTime + `IClock`; `MidpointRounding.AwayFromZero`; cálculos puros; EF só em Infrastructure (regras do `CLAUDE.md`).
- Preservar o padrão de mid-rate `(compra+venda)/2` já usado no Perfil B; não alterar arredondamentos existentes.

## 8. Estratégia de testes e critérios de aceite

- **Unit (resolução):** o novo método mapeia `PtaxD1`→`PtaxD0(R-1)` e repassa outros tipos sem alteração.
- **Off-by-one (crítico):**
  - **AC-1** `CriarCotacao` com abertura R: dado **apenas** um `PtaxD0` de fechamento em `R-1`, a criação resolve esse fechamento (não `R-2`, não null).
  - **AC-2** Caso de Painel com referência R e o mesmo `PtaxD0` de `R-1`: resolve o mesmo fechamento de AC-1.
  - **AC-3** `RegistrarProposta` usa a **mesma** data de PTAX travada na criação (sem deslocamento).
- **Regressão de "dados vazios":**
  - **AC-4** Banco contendo **somente** `PtaxD0` (como o ingestor real) → `CriarCotacao`, `RefreshMercado` e um painel cambial funcionam (hoje falham).
  - **AC-5** Banco sem nenhuma `PtaxD0` → erro de PTAX ausente claro (mensagem mantida).
- **Endpoint manual:**
  - **AC-6** `POST /cotacoes-fx` (admin) grava `PtaxD0`; repetição idempotente (mesma chave) não duplica.
  - **AC-7** Não-admin recebe 403; payload inválido (valor ≤ 0, quote ≠ BRL, momento futuro) recebe 400.
- **Fixtures:** suíte completa verde após RF-10/RF-11; nenhum teste depende de linha `PtaxD1` semeada.

## 9. Boundaries

**Sempre fazer**
- Preservar comportamento já correto (mid-rate do Perfil B; venda no `CriarCotacao`; data travada no `RegistrarProposta`).
- Centralizar a regra D-1 em um único ponto (resolver).
- Manter mensagens de erro de PTAX ausente.

**Perguntar antes**
- Mudar a semântica de `RefreshCotacaoMercado` (hoje pede D-1; confirmar se deveria usar D0/spot).
- Expor `GET /cotacoes-fx` publicamente além de leitura admin.

**Nunca fazer**
- Fazer o ingestor gravar `PtaxD1` (decisão D1 descartou essa via).
- Alterar a validação do `parametros-cotacao` para aceitar valores fora do enum.
- `decimal` cru para dinheiro; `DateTime.Now` em domínio/aplicação; importar Infrastructure em Mcp/A2a.
- Alterar expected outputs do golden dataset sem sign-off.

## 10. Fora de escopo

- Correção no código do front-end (entregamos apenas o guia — D4).
- Agendamento/observabilidade do `Sgcf.Jobs` (coberto pelo runbook de serviços, em produção paralela).
- Backfill histórico de PTAX (pode usar `BackfillPtaxJob` à parte).

## 11. Riscos

| Risco | Impacto | Mitigação |
|---|---|---|
| Off-by-one no Perfil A | Alto | AC-1..AC-3 dedicados; tabela 3.2 audita cada chamador |
| Regressão em mid-rate do Painel | Médio | Resolver retorna `CotacaoFx` (não mid); chamadores mantêm seu cálculo |
| Quebra de camada (MCP→Infra) | Médio | Interface em Application; implementação em Infrastructure |
| Endpoint manual gravar tipo errado | Médio | Default `PtaxD0` + validação; idempotência por unique key |

## 12. Open Questions

- `RefreshCotacaoMercado` deveria usar D-1 (atual) ou a cotação mais recente (D0/spot)? (assumido: manter D-1.)
- O endpoint manual deve aceitar `PtaxD1` explicitamente, ou apenas `PtaxD0`/`SpotIntraday`/`Fixing`? (assumido: aceitar o enum, mas recomendar `PtaxD0`.)

## 13. Referências

- Diagnóstico (conversa 02/jun/2026); `SPEC.md` mestre §5.1/§6.1.
- Código: `CriarCotacaoCommand.cs`, `RegistrarPropostaCommand.cs`, `RefreshCotacaoMercadoCommand.cs`, `CotacaoResolverService.cs`, `CotacaoFxRepository.cs`, `PtaxIngestor.cs`, `BcbPtaxClient.cs`, `CreateParametroCommand.cs`, `ParametrosCotacaoController.cs`.
