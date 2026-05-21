# DTOs — Simulações + Quadro da Dívida

Este documento descreve todos os DTOs de entrada e saída do módulo, com tipos exatos, constraints e valores de enum extraídos diretamente do código-fonte.

---

## DTOs de entrada (request)

### `CriarCenarioSimulacaoCommand`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Simulacao/Commands/CriarCenarioSimulacaoCommand.cs`

| Campo | Tipo JSON | Obrigatório | Constraints |
|---|---|---|---|
| `nome` | `string` | Sim | Não vazio. Máx. 100 caracteres. |
| `anoBase` | `number` (inteiro) | Sim | Entre 2020 e 2050 inclusive. |
| `descricao` | `string \| null` | Não | Texto livre sem limitação definida. |

---

### `AtualizarCenarioCommand` (body do PATCH `/cenarios/{id}`)

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Simulacao/Commands/AtualizarCenarioCommand.cs`

Todos os campos são opcionais no sentido de que campos `null` são ignorados (patch parcial). Um `nome` não-null, porém, segue as mesmas constraints do create.

| Campo | Tipo JSON | Obrigatório | Constraints |
|---|---|---|---|
| `nome` | `string \| null` | Não | Quando não null: não vazio, máx. 100 chars. |
| `descricao` | `string \| null` | Não | Quando não null: substitui o valor anterior. |
| `anoBase` | `number \| null` | Não | Quando não null: entre 2020 e 2050. Apenas em status `Rascunho`. |

**Nota:** O campo `cenarioId` do command é preenchido pelo controller a partir do parâmetro de rota — não deve ser enviado no body.

---

### `AdicionarSimulacaoInput`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Simulacao/Dtos/AdicionarSimulacaoInput.cs`

Usado em `POST /cenarios/{id}/simulacoes` e como campo `simulacao` do `SimularCronogramaHipoteticoQuery`.

| Campo | Tipo JSON | Obrigatório | Constraints |
|---|---|---|---|
| `bancoId` | `string` (UUID) | Sim | Deve referenciar um banco existente. |
| `modalidade` | `string` | Sim | Ver enum `ModalidadeContrato` abaixo. |
| `moeda` | `string` | Sim | Ver enum `Moeda` abaixo. |
| `valorPrincipal` | `number` (decimal) | Sim | Maior que zero. Invariante I-1. |
| `dataContratacaoPrevista` | `string` (`YYYY-MM-DD`) | Sim | Deve ser hoje ou futuro (fuso BRT). Invariante I-2. Deve estar dentro do `anoBase` do cenário. Invariante I-4. |
| `dataPrimeiroVencimento` | `string` (`YYYY-MM-DD`) | Sim | Deve ser posterior a `dataContratacaoPrevista`. Invariante I-3. |
| `tipoTaxa` | `string` | Sim | `"Fixa"` ou `"CdiSpread"`. Ver enum `TipoTaxa`. |
| `taxaAa` | `number \| null` | Condicional | Obrigatório quando `tipoTaxa = "Fixa"`. Em % a.a. (ex: `15.50` = 15,50% ao ano). Invariante I-6. |
| `spreadAa` | `number \| null` | Condicional | Obrigatório quando `tipoTaxa = "CdiSpread"`. Em % a.a. sobre CDI. Invariante I-7. |
| `baseCalculo` | `string` | Sim | Ver enum `BaseCalculo` abaixo. |
| `estruturaAmortizacao` | `string` | Sim | Ver enum `EstruturaAmortizacao` abaixo. |
| `periodicidade` | `string` | Sim | Ver enum `Periodicidade` abaixo. |
| `quantidadeParcelas` | `number` (inteiro) | Sim | Mínimo 1. Invariante I-5. |
| `anchorDiaMes` | `string` | Sim | Ver enum `AnchorDiaMes` abaixo. |
| `anchorDiaFixo` | `number \| null` | Condicional | Dia do mês (1–31). Obrigatório quando `anchorDiaMes = "DiaFixo"`. |
| `garantiaExigidaPrevista` | `string \| null` | Não | Campo livre. Máx. 500 caracteres. Invariante I-11. |
| `observacoes` | `string \| null` | Não | Campo livre. Sem limitação de tamanho definida. |

**Invariantes cruzadas (I-6 e I-7):**
- `tipoTaxa = "Fixa"` → `taxaAa` obrigatório, `spreadAa` deve ser `null`.
- `tipoTaxa = "CdiSpread"` → `spreadAa` obrigatório, `taxaAa` deve ser `null`, `moeda` deve ser `"Brl"`. Invariante I-7.
- `tipoTaxa = "CdiSpread"` → no endpoint `cronograma-hipotetico`, o campo `cdiReferenciaAaPercentual` é obrigatório no nível da query.

**Invariante I-8 (modalidades cambiais):**
- `modalidade = "Finimp"` ou `"Lei4131"` → `moeda` não pode ser `"Brl"`. Deve ser `"Usd"`, `"Eur"`, `"Jpy"` ou `"Cny"`.

---

### `AtualizarSimulacaoInput`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Simulacao/Dtos/AtualizarSimulacaoInput.cs`

Usado em `PATCH /cenarios/{id}/simulacoes/{simId}`. Substituição total dos campos mutáveis (não parcial). O campo `bancoId` não está presente — não é alterável após a criação.

| Campo | Tipo JSON | Obrigatório | Constraints |
|---|---|---|---|
| `modalidade` | `string` | Sim | Ver enum `ModalidadeContrato`. |
| `moeda` | `string` | Sim | Ver enum `Moeda`. |
| `valorPrincipal` | `number` (decimal) | Sim | Maior que zero. |
| `dataContratacaoPrevista` | `string` (`YYYY-MM-DD`) | Sim | Maior ou igual a hoje (BRT). |
| `dataPrimeiroVencimento` | `string` (`YYYY-MM-DD`) | Sim | Posterior a `dataContratacaoPrevista`. |
| `tipoTaxa` | `string` | Sim | `"Fixa"` ou `"CdiSpread"`. |
| `taxaAa` | `number \| null` | Condicional | Obrigatório para `"Fixa"`. |
| `spreadAa` | `number \| null` | Condicional | Obrigatório para `"CdiSpread"`. |
| `baseCalculo` | `string` | Sim | Ver enum `BaseCalculo`. |
| `estruturaAmortizacao` | `string` | Sim | Ver enum `EstruturaAmortizacao`. |
| `periodicidade` | `string` | Sim | Ver enum `Periodicidade`. |
| `quantidadeParcelas` | `number` (inteiro) | Sim | Mínimo 1. |
| `anchorDiaMes` | `string` | Sim | Ver enum `AnchorDiaMes`. |
| `anchorDiaFixo` | `number \| null` | Condicional | Obrigatório para `"DiaFixo"`. |
| `garantiaExigidaPrevista` | `string \| null` | Não | Máx. 500 caracteres. |
| `observacoes` | `string \| null` | Não | — |

---

### `CompararCenariosQuery`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Simulacao/Queries/CompararCenariosQuery.cs`

| Campo | Tipo JSON | Obrigatório | Constraints |
|---|---|---|---|
| `ano` | `number` (inteiro) | Sim | Entre 2020 e 2050. |
| `cenarioIds` | `string[]` (UUID[]) | Sim | Mínimo 1, máximo 5. Todos com o mesmo `anoBase`. |

---

### `AtualizarTetaoMensalRequest`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Api/Controllers/ParametrosSistemaController.cs`

| Campo | Tipo JSON | Obrigatório | Constraints |
|---|---|---|---|
| `valor` | `number \| null` | Sim | Valor em BRL. Não pode ser negativo. `null` remove o limite. |

---

## DTOs de saída (response)

### `CenarioSimulacaoDto`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Simulacao/Dtos/CenarioSimulacaoDto.cs`

Retornado nas operações: criar, obter por Id, atualizar, ativar, arquivar, duplicar, adicionar simulação, atualizar simulação.

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `id` | `string` (UUID) | Identificador único do cenário. |
| `nome` | `string` | Nome do cenário. |
| `descricao` | `string \| null` | Descrição opcional. |
| `anoBase` | `number` (inteiro) | Ano-calendário de referência (ex: `2026`). |
| `status` | `string` | Status atual: `"Rascunho"`, `"Ativo"` ou `"Arquivado"`. |
| `criadoPor` | `string` | `sub` do JWT do usuário criador. |
| `createdAt` | `string` (ISO 8601 com offset) | Timestamp de criação (ex: `"2026-05-20T14:30:00-03:00"`). |
| `updatedAt` | `string` (ISO 8601 com offset) | Timestamp da última atualização. |
| `simulacoes` | `SimulacaoContratacaoDto[]` | Lista de simulações filhas. Vazio ao criar. |

---

### `CenarioSimulacaoResumoDto`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Simulacao/Dtos/CenarioSimulacaoResumoDto.cs`

Retornado na listagem `GET /cenarios`. Não inclui as simulações filhas para reduzir o payload.

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `id` | `string` (UUID) | Identificador único. |
| `nome` | `string` | Nome do cenário. |
| `status` | `string` | `"Rascunho"`, `"Ativo"` ou `"Arquivado"`. |
| `anoBase` | `number` (inteiro) | Ano-calendário de referência. |
| `qtdeSimulacoes` | `number` (inteiro) | Quantidade de simulações filhas. |
| `criadoPor` | `string` | `sub` do JWT do criador. |
| `updatedAt` | `string` (ISO 8601 com offset) | Timestamp da última atualização. |

---

### `SimulacaoContratacaoDto`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Simulacao/Dtos/SimulacaoContratacaoDto.cs`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `id` | `string` (UUID) | Identificador da simulação. |
| `cenarioId` | `string` (UUID) | Cenário pai. |
| `bancoId` | `string` (UUID) | Banco credor. |
| `modalidade` | `string` | Ver enum `ModalidadeContrato`. |
| `moeda` | `string` | Ver enum `Moeda`. |
| `valorPrincipal` | `number` (decimal) | Valor principal na moeda informada. 2 casas decimais, HalfUp. |
| `dataContratacaoPrevista` | `string` (`YYYY-MM-DD`) | Data prevista de contratação. |
| `dataPrimeiroVencimento` | `string` (`YYYY-MM-DD`) | Data do primeiro vencimento. |
| `tipoTaxa` | `string` | `"Fixa"` ou `"CdiSpread"`. |
| `taxaAa` | `number \| null` | Taxa nominal anual em %. `null` para `CdiSpread`. |
| `spreadAa` | `number \| null` | Spread anual sobre CDI em %. `null` para `Fixa`. |
| `baseCalculo` | `string` | Ver enum `BaseCalculo`. |
| `estruturaAmortizacao` | `string` | Ver enum `EstruturaAmortizacao`. |
| `periodicidade` | `string` | Ver enum `Periodicidade`. |
| `quantidadeParcelas` | `number` (inteiro) | Quantidade de parcelas. |
| `anchorDiaMes` | `string` | Ver enum `AnchorDiaMes`. |
| `anchorDiaFixo` | `number \| null` | Dia fixo (1–31). `null` quando `anchorDiaMes != "DiaFixo"`. |
| `garantiaExigidaPrevista` | `string \| null` | Garantia informativa prevista. |
| `observacoes` | `string \| null` | Observações livres. |
| `version` | `number` (inteiro) | Versão da simulação. Incrementado a cada mutação. Usado para invalidação de cache Redis (AD-3). |
| `createdAt` | `string` (ISO 8601 com offset) | Timestamp de criação. |
| `updatedAt` | `string` (ISO 8601 com offset) | Timestamp da última mutação. |

---

### `CronogramaHipoteticoDto`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Simulacao/Dtos/CronogramaHipoteticoDto.cs`

Retornado pelo endpoint de preview `POST /cronograma-hipotetico`.

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `taxaEfetivaAaPercentual` | `number` (decimal) | Taxa efetiva anual em %. Para CdiSpread: composição `(1+CDI)×(1+spread)−1 × 100`. |
| `quantidadeEventos` | `number` (inteiro) | Total de linhas no cronograma (principal + juros). |
| `principalTotal` | `number` (decimal) | Soma de todos os eventos de tipo `"Principal"`. |
| `jurosTotal` | `number` (decimal) | Soma de todos os eventos de tipo `"Juros"`. |
| `eventos` | `EventoCronogramaItemDto[]` | Linha a linha do cronograma. |

#### `EventoCronogramaItemDto`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `numero` | `number` (inteiro) | Número sequencial do evento. |
| `tipo` | `string` | `"Principal"` ou `"Juros"`. |
| `data` | `string` (`YYYY-MM-DD`) | Data do evento. |
| `valor` | `number` (decimal) | Valor em BRL (ou moeda da operação). |
| `saldoDevedorApos` | `number \| null` | Saldo devedor após o evento. `null` para eventos de Juros. |

---

### `QuadroDividaDto`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Painel/Queries/QuadroDividaDto.cs`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `ano` | `number` (inteiro) | Ano civil consultado. |
| `dataReferencia` | `string` (`YYYY-MM-DD`) | Data em que o snapshot foi calculado (hoje). |
| `snapshotInicial` | `SaldoPorBancoAtualDto` | Saldo atual por banco — base da projeção. |
| `projecao` | `QuadroDividaProjecaoDto` | 12 meses projetados. |
| `sumario` | `QuadroDividaSumarioDto` | Totais anuais agregados. |
| `alertas` | `string[]` | Alertas de tetão. Vazio se não há tetão configurado ou nenhum mês excede o limite. |
| `cenarioAplicado` | `CenarioAplicadoDto \| null` | Metadados do cenário aplicado. `null` quando sem `cenarioId`. |

#### `SaldoPorBancoAtualDto`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Painel/Queries/SaldoPorBancoAtualDto.cs`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `bancos` | `SaldoBancoAtualDto[]` | Posição de cada banco. |
| `saldoTotalBrl` | `number` (decimal) | Soma de todos os saldos em BRL. |
| `dataReferencia` | `string` (`YYYY-MM-DD`) | Data da apuração. |

#### `SaldoBancoAtualDto`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `bancoId` | `string` (UUID) | Identificador do banco. |
| `bancoApelido` | `string` | Nome curto do banco (ex: `"Itaú"`). |
| `bancoCodigoCompe` | `string` | Código COMPE do banco (ex: `"341"`). |
| `saldoBrl` | `number` (decimal) | Saldo total em BRL (contratos em moeda estrangeira convertidos com PTAX D-1). |
| `quantidadeContratosAtivos` | `number` (inteiro) | Contratos ativos com este banco. |

#### `QuadroDividaProjecaoDto`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `meses` | `MesProjecaoDto[]` | Exatamente 12 entradas. Índice 0 = janeiro, índice 11 = dezembro. |

#### `MesProjecaoDto`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `ano` | `number` (inteiro) | Ano ao qual o mês pertence. |
| `mes` | `number` (inteiro) | Número do mês (1–12). |
| `bancos` | `SaldoBancoMesDto[]` | Breakdown por banco. Apenas bancos com saldo ou eventos no mês. |
| `saldoTotalInicio` | `number` (decimal) | Soma de `saldoInicio` de todos os bancos do mês, em BRL. |
| `saldoTotalFim` | `number` (decimal) | Soma de `saldoFim` de todos os bancos do mês, em BRL. |
| `totalAmortizacaoMes` | `number` (decimal) | Total de amortizações de principal no mês, em BRL. |
| `totalCaptacaoMes` | `number` (decimal) | Total de captações no mês, em BRL. |

#### `SaldoBancoMesDto`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `bancoId` | `string` (UUID) | Identificador do banco. |
| `bancoApelido` | `string` | Nome curto do banco. |
| `saldoInicio` | `number` (decimal) | Saldo no início do mês, em BRL. Igual a `saldoFim` do mês anterior. |
| `saldoFim` | `number` (decimal) | Saldo no fim do mês após eventos, em BRL. |
| `totalAmortizacaoNoMes` | `number` (decimal) | Amortizações de principal do banco no mês, em BRL. |
| `totalCaptacaoNoMes` | `number` (decimal) | Captações do banco no mês (reais + simuladas), em BRL. |
| `sharePercentual` | `number` (decimal) | Percentual do banco no saldo total de fechamento do mês. 4 casas decimais. |

#### `QuadroDividaSumarioDto`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `saldoTotalInicioAno` | `number` (decimal) | Saldo total no início de janeiro. |
| `saldoTotalFimAno` | `number` (decimal) | Saldo total no fim de dezembro. |
| `totalAmortizacaoNoAno` | `number` (decimal) | Soma de todas as amortizações de principal no ano. |
| `totalCaptacaoNoAno` | `number` (decimal) | Soma de todas as captações no ano. |
| `variacaoAnualPercentual` | `number` (decimal) | `(saldoFimAno − saldoInicioAno) / saldoInicioAno × 100`. Zero quando `saldoInicioAno = 0`. |

#### `CenarioAplicadoDto`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `id` | `string` (UUID) | Identificador do cenário. |
| `nome` | `string` | Nome do cenário. |
| `status` | `string` | `"Rascunho"`, `"Ativo"` ou `"Arquivado"`. |
| `anoBase` | `number` (inteiro) | Ano-calendário de referência do cenário. |
| `quantidadeSimulacoes` | `number` (inteiro) | Quantidade de captações hipotéticas no cenário. |

---

### `ResultadoComparacaoCenariosDto`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Simulacao/Queries/ResultadoComparacaoCenariosDto.cs`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `ano` | `number` (inteiro) | Ano civil consultado. |
| `dataReferencia` | `string` (`YYYY-MM-DD`) | Data de referência da projeção. |
| `cenarios` | `CenarioComparadoDto[]` | Lista na mesma ordem de entrada. Primeiro item é o baseline. |

#### `CenarioComparadoDto`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `cenarioId` | `string` (UUID) | Identificador do cenário. |
| `nome` | `string` | Nome do cenário. |
| `status` | `string` | Status atual. |
| `anoBase` | `number` (inteiro) | Ano-base do cenário. |
| `ehBaseline` | `boolean` | `true` apenas para o primeiro cenário da lista. |
| `projecao` | `QuadroDividaProjecaoDto` | Os 12 meses projetados. |
| `sumario` | `QuadroDividaSumarioDto` | Totais anuais. |
| `deltasMensais` | `DeltaMensalDto[] \| null` | `null` para o baseline. 12 entradas para os demais. |
| `deltaAnual` | `DeltaAnualDto \| null` | `null` para o baseline. |

#### `DeltaMensalDto`

Todos os deltas são calculados como `cenário − baseline`.

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `mes` | `number` (inteiro) | Número do mês (1–12). |
| `saldoFimDelta` | `number` (decimal) | Diferença de `saldoTotalFim` em BRL. |
| `totalCaptacaoDelta` | `number` (decimal) | Diferença de `totalCaptacaoMes` em BRL. |
| `totalAmortizacaoDelta` | `number` (decimal) | Diferença de `totalAmortizacaoMes` em BRL. |
| `saldoFimDeltaPercentual` | `number` (decimal) | `(saldoFimDelta / saldoFimBaseline) × 100`. Zero quando `saldoFimBaseline = 0`. |

#### `DeltaAnualDto`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `saldoFimAnoDelta` | `number` (decimal) | Diferença de `saldoTotalFimAno` em BRL. |
| `totalCaptacaoAnoDelta` | `number` (decimal) | Diferença de `totalCaptacaoNoAno` em BRL. |
| `saldoFimAnoDeltaPercentual` | `number` (decimal) | `(saldoFimAnoDelta / saldoFimAnoBaseline) × 100`. |

---

### `ParametrosSistemaDto`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Sistema/ParametrosSistemaDto.cs`

| Campo | Tipo JSON | Descrição |
|---|---|---|
| `tetaoMensalCapacidadeBrl` | `number \| null` | Limite mensal de movimentação em BRL. `null` = sem limite configurado. |

---

## Enums

### `StatusCenarioSimulacao`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Simulacao/StatusCenarioSimulacao.cs`

| Valor (string) | Descrição |
|---|---|
| `"Rascunho"` | Em elaboração. Aceita todas as operações de edição. |
| `"Ativo"` | Aprovado para uso. Ainda aceita adição/remoção de simulações. |
| `"Arquivado"` | Encerrado. Imutável — consulta apenas para auditoria. |

---

### `TipoTaxa`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Simulacao/TipoTaxa.cs`

| Valor (string) | Descrição | Campos obrigatórios |
|---|---|---|
| `"Fixa"` | Taxa nominal anual fixa. | `taxaAa` (não null). `spreadAa` deve ser null. |
| `"CdiSpread"` | CDI + spread anual fixo. | `spreadAa` (não null). `taxaAa` deve ser null. `moeda` deve ser `"Brl"`. |

---

### `ModalidadeContrato`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Contratos/ModalidadeContrato.cs`

| Valor (string) | Notas |
|---|---|
| `"Finimp"` | Financiamento de importação. Modalidade cambial — não aceita `"Brl"`. |
| `"Refinimp"` | Refinanciamento de importação. |
| `"Lei4131"` | Captação externa Lei 4.131. Modalidade cambial — não aceita `"Brl"`. |
| `"Nce"` | Nota de Crédito à Exportação. |
| `"CapitalDeGiro"` | Capital de giro. |
| `"Fgi"` | Fundo de Garantia para Investimentos. |

---

### `Moeda`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Common/Moeda.cs`

| Valor (string) | Descrição |
|---|---|
| `"Brl"` | Real Brasileiro. |
| `"Usd"` | Dólar Americano. |
| `"Eur"` | Euro. |
| `"Jpy"` | Iene Japonês. |
| `"Cny"` | Yuan Chinês. |

---

### `BaseCalculo`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Common/BaseCalculo.cs`

| Valor (string) | Denominador | Uso típico |
|---|---|---|
| `"Dias252"` | 252 dias úteis | CDI/DI (padrão para operações em BRL). |
| `"Dias360"` | 360 dias corridos | Comercial (FINIMP, Lei4131, operações internacionais). |
| `"Dias365"` | 365 dias corridos | Base exata (alguns NCE/CCE). |

---

### `EstruturaAmortizacao`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Contratos/EstruturaAmortizacao.cs`

| Valor (string) | Descrição |
|---|---|
| `"Bullet"` | Principal pago de uma só vez no vencimento final. |
| `"Price"` | Parcelas iguais (tabela Price / PMT constante). |
| `"Sac"` | Sistema de Amortizações Constantes (amortização fixa, juros decrescentes). |
| `"BulletComJurosPeriodicos"` | Principal no final com pagamento periódico de juros. |
| `"Customizada"` | Fluxo de pagamentos customizado. |

---

### `Periodicidade`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Contratos/Periodicidade.cs`

| Valor (string) | Frequência |
|---|---|
| `"Bullet"` | Uma única ocorrência (vencimento final). |
| `"Mensal"` | Mensal. |
| `"Bimestral"` | A cada 2 meses. |
| `"Trimestral"` | A cada 3 meses. |
| `"Quadrimestral"` | A cada 4 meses. |
| `"Semestral"` | A cada 6 meses. |
| `"Anual"` | Anual. |
| `"Customizada"` | Frequência customizada. |

---

### `AnchorDiaMes`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Contratos/AnchorDiaMes.cs`

| Valor (string) | Descrição | Campo adicional obrigatório |
|---|---|---|
| `"DiaContratacao"` | Vencimentos caem no mesmo dia da contratação. | — |
| `"DiaFixo"` | Vencimentos caem num dia fixo do mês (ex: dia 15). | `anchorDiaFixo` (1–31). |
| `"UltimoDiaUtil"` | Vencimentos no último dia útil do mês. | — |
| `"PrimeiroDiaUtil"` | Vencimentos no primeiro dia útil do mês. | — |
