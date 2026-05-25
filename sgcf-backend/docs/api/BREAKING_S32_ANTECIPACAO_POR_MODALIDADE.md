# Breaking Change — S32: Configuração de Antecipação por Modalidade

**Versão:** S32 (commit `e0072d7` · revisão `92dbaab`)
**Data:** 2026-05-22
**Afeta:** Frontend, integrações MCP/A2A, scripts de seed

---

## Contexto do Projeto

O **SGCF** (Sistema de Gestão de Contratos de Financiamento) gerencia o ciclo completo de captações bancárias da Proxys: cotações, contratos ativos, antecipação de pagamentos, hedge cambial e painel executivo.

As principais entidades que o frontend manipula são:

| Entidade | Rota base | Papel |
|----------|-----------|-------|
| `Banco` | `/api/v1/bancos` | Credor. Armazena restrições institucionais (anuência, prazo de aviso). |
| `LimiteBanco` | `/api/v1/limites-banco` | Linha de crédito aprovada por banco × modalidade. Controla disponibilidade. |
| `Contrato` | `/api/v1/contratos` | Contrato ativo derivado de uma cotação aceita. |
| `SimulacaoAntecipacao` | `/api/v1/antecipacao/simular` | Cálculo de custo de liquidação antecipada. |

**Modalidades de captação suportadas:** `Finimp`, `Lei4131`, `Refinimp`, `Nce`, `BalcaoCaixa`, `Fgi`.

---

## O Que Mudou

### Motivação

O campo `PadraoAntecipacao` (fórmula usada para calcular o custo de liquidação antecipada) estava na entidade `Banco`. Isso estava errado: o mesmo banco pode usar fórmulas diferentes por modalidade. A Caixa Econômica, por exemplo, usa o Padrão D (fórmula TLA/BACEN) para a modalidade `BalcaoCaixa` e o Padrão E (abatimento proporcional de juros) para contratos prefixados Finimp.

A correção move `PadraoAntecipacao` e todos os seus parâmetros de cálculo para `LimiteBanco`, que já tem a chave composta `(BancoId, Modalidade)`.

### Resumo das alterações

| O quê | Antes | Depois |
|-------|-------|--------|
| Onde fica `PadraoAntecipacao` | `Banco` | `LimiteBanco` |
| `BancoDto` contém campos de cálculo | Sim | **Não** |
| `LimiteBancoDto` contém campos de cálculo | Não | **Sim** |
| `POST /api/v1/bancos` aceita `padraoAntecipacao` | Sim | **Não** |
| `PUT /api/v1/bancos/{id}/config-antecipacao` aceita parâmetros de cálculo | Sim | **Não** |
| `POST /api/v1/limites-banco` aceita parâmetros de antecipação | Não | **Sim** |
| `PATCH /api/v1/limites-banco/{id}` configura antecipação | Não | **Sim** (via flag) |

> **Atenção:** A documentação estática em `docs/api/bancos.md` e `docs/api/limites-banco.md` ainda reflete o estado anterior. Este documento prevalece enquanto a documentação oficial não for atualizada.

---

## Padrões de Antecipação

Os cinco padrões refletem as metodologias reais negociadas pela Proxys com cada banco/modalidade:

| Valor (string) | Fórmula | Banco/modalidade de referência |
|----------------|---------|-------------------------------|
| `A` | Pro rata + break funding fee fixo + indenização | BB — Finimp |
| `B` | Cobra juros do **período total** contratado — antecipação nunca gera economia | Sicredi |
| `C` | Desconto a taxa de mercado (MTM) | BV — FGI (PEAC) |
| `D` | Fórmula TLA BACEN (Resoluções 3401/06 e 3516/07) | Caixa — BalcaoCaixa |
| `E` | Pagamento ordinário com abatimento proporcional de juros futuros | Caixa — Finimp prefixado |

O enum é serializado pelo nome: `"A"`, `"B"`, `"C"`, `"D"`, `"E"`. A API aceita case-insensitive na entrada.

**Regra de negócio importante:** Contratos com Padrão B nunca entram no ranqueamento do painel de otimização (`GET /api/v1/painel/antecipacao-portfolio`) — o endpoint os exclui automaticamente porque a antecipação não gera economia.

---

## Mudanças por Endpoint

### `GET /api/v1/bancos` e `GET /api/v1/bancos/{id}`

Os campos de cálculo de antecipação foram **removidos** do `BancoDto`. O DTO passou a conter apenas restrições institucionais.

**Antes (S31 e anteriores):**
```json
{
  "id": "3fa85f64-...",
  "codigoCompe": "341",
  "razaoSocial": "Itaú Unibanco S.A.",
  "apelido": "Itaú",
  "aceitaLiquidacaoTotal": true,
  "aceitaLiquidacaoParcial": false,
  "exigeAnuenciaExpressa": false,
  "exigeParcelaInteira": false,
  "avisoPrevioMinDiasUteis": 3,
  "padraoAntecipacao": "A",
  "breakFundingFeePct": 0.5,
  "tlaPctSobreSaldo": null,
  "tlaPctPorMesRemanescente": null,
  "valorMinimoParcialPct": 10.0,
  "observacoesAntecipacao": null,
  "createdAt": "2026-01-15T10:30:00Z",
  "updatedAt": "2026-05-01T08:00:00Z"
}
```

**Depois (S32 em diante):**
```json
{
  "id": "3fa85f64-...",
  "codigoCompe": "341",
  "razaoSocial": "Itaú Unibanco S.A.",
  "apelido": "Itaú",
  "aceitaLiquidacaoTotal": true,
  "aceitaLiquidacaoParcial": false,
  "exigeAnuenciaExpressa": false,
  "exigeParcelaInteira": false,
  "avisoPrevioMinDiasUteis": 3,
  "createdAt": "2026-01-15T10:30:00Z",
  "updatedAt": "2026-05-22T14:00:00Z"
}
```

**Action FE:** Remover qualquer leitura de `padraoAntecipacao`, `breakFundingFeePct`, `tlaPctSobreSaldo`, `tlaPctPorMesRemanescente`, `valorMinimoParcialPct` e `observacoesAntecipacao` do objeto `BancoDto`. Buscar esses dados em `LimiteBancoDto`.

---

### `POST /api/v1/bancos`

O campo `padraoAntecipacao` foi **removido** do request body. O endpoint cria apenas o cadastro básico do banco.

**Antes:**
```json
{
  "codigoCompe": "341",
  "razaoSocial": "Itaú Unibanco S.A.",
  "apelido": "Itaú",
  "padraoAntecipacao": "A"
}
```

**Depois:**
```json
{
  "codigoCompe": "341",
  "razaoSocial": "Itaú Unibanco S.A.",
  "apelido": "Itaú"
}
```

**Action FE:** Remover o campo `padraoAntecipacao` do formulário de criação de banco. O padrão de antecipação é configurado por modalidade no `POST /api/v1/limites-banco`.

---

### `PUT /api/v1/bancos/{id}/config-antecipacao`

O endpoint continua existindo, mas agora gerencia **apenas restrições institucionais**. Os campos de cálculo foram removidos.

**Antes — aceitava:**
```json
{
  "aceitaLiquidacaoTotal": true,
  "aceitaLiquidacaoParcial": true,
  "exigeAnuenciaExpressa": false,
  "exigeParcelaInteira": false,
  "avisoPrevioMinDiasUteis": 3,
  "padraoAntecipacao": "A",
  "breakFundingFeePct": 0.5,
  "valorMinimoParcialPct": 10.0,
  "tlaPctSobreSaldo": null,
  "tlaPctPorMesRemanescente": null,
  "observacoesAntecipacao": "Acordo 2026"
}
```

**Depois — aceita apenas:**
```json
{
  "aceitaLiquidacaoTotal": true,
  "aceitaLiquidacaoParcial": true,
  "exigeAnuenciaExpressa": false,
  "exigeParcelaInteira": false,
  "avisoPrevioMinDiasUteis": 3
}
```

**Action FE:** Remover os campos de cálculo de antecipação do formulário de edição de banco. Mover essas configurações para o formulário de `LimiteBanco`.

---

### `GET /api/v1/limites-banco` e `GET /api/v1/limites-banco/{id}`

O `LimiteBancoDto` agora inclui os campos de antecipação. O schema completo atualizado:

```json
{
  "id": "guid",
  "bancoId": "guid",
  "modalidade": "Finimp",
  "valorLimiteBrl": 50000000.00,
  "valorUtilizadoBrl": 12000000.00,
  "valorDisponivelBrl": 38000000.00,
  "dataVigenciaInicio": "2026-01-01",
  "dataVigenciaFim": "2026-12-31",
  "observacoes": "Limite aprovado em comitê 2026",
  "padraoAntecipacao": "A",
  "breakFundingFeePct": 0.5,
  "tlaPctSobreSaldo": null,
  "tlaPctPorMesRemanescente": null,
  "valorMinimoParcialPct": 10.0,
  "observacoesAntecipacao": "Acordo negociado em jan/2026",
  "createdAt": "2026-01-01T00:00:00Z",
  "updatedAt": "2026-05-22T14:00:00Z",
  "garantiasExigidas": [...],
  "historico": [...]
}
```

Campos de porcentagem (`breakFundingFeePct`, `tlaPctSobreSaldo`, `tlaPctPorMesRemanescente`, `valorMinimoParcialPct`) são devolvidos em unidade "humana" — ex.: `0.5` significa 0,5% a.a. (não 0,005).

**Action FE:** Exibir as informações de padrão e parâmetros de cálculo a partir do `LimiteBancoDto`, e não mais do `BancoDto`.

---

### `POST /api/v1/limites-banco`

O endpoint aceita agora todos os campos de antecipação opcionalmente na criação:

```json
{
  "bancoId": "guid",
  "modalidade": "Finimp",
  "valorLimiteBrl": 50000000.00,
  "dataVigenciaInicio": "2026-01-01",
  "dataVigenciaFim": "2026-12-31",
  "observacoes": "Limite 2026",
  "padraoAntecipacao": "A",
  "breakFundingFeePct": 0.5,
  "tlaPctSobreSaldo": null,
  "tlaPctPorMesRemanescente": null,
  "valorMinimoParcialPct": 10.0,
  "observacoesAntecipacao": "Break funding conforme aditivo 2026",
  "garantiasExigidas": [...]
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `padraoAntecipacao` | string | Não | `"A"` \| `"B"` \| `"C"` \| `"D"` \| `"E"` |
| `breakFundingFeePct` | decimal | Não | Percentual ≥ 0. Obrigatório para Padrão A. |
| `tlaPctSobreSaldo` | decimal | Não | Percentual ≥ 0. Obrigatório para Padrão D. |
| `tlaPctPorMesRemanescente` | decimal | Não | Percentual ≥ 0. Obrigatório para Padrão D. |
| `valorMinimoParcialPct` | decimal | Não | Percentual ≥ 0. Mínimo para antecipação parcial. |
| `observacoesAntecipacao` | string | Não | Texto livre. |

**Consistência entre padrão e parâmetros:** A API não rejeita a criação se os parâmetros exigidos pelo padrão estiverem ausentes, mas a simulação falhará com `400 Bad Request` ao tentar usar um limite mal configurado. A responsabilidade de validação completa está no formulário do FE.

---

### `PATCH /api/v1/limites-banco/{id}`

O endpoint usa uma flag explícita `configurarAntecipacao` para indicar intenção de alterar a configuração de antecipação. Isso evita que um PATCH de valor de limite limpe inadvertidamente os parâmetros de cálculo.

**Exemplo — atualizar apenas o padrão de antecipação:**
```json
{
  "configurarAntecipacao": true,
  "padraoAntecipacao": "D",
  "tlaPctSobreSaldo": 1.0,
  "tlaPctPorMesRemanescente": 0.1
}
```

**Exemplo — atualizar limite e antecipação juntos:**
```json
{
  "novoValorLimiteBrl": 75000000.00,
  "configurarAntecipacao": true,
  "padraoAntecipacao": "A",
  "breakFundingFeePct": 0.75,
  "valorMinimoParcialPct": 5.0
}
```

**Exemplo — atualizar apenas o valor do limite (não toca antecipação):**
```json
{
  "novoValorLimiteBrl": 75000000.00
}
```

| Campo | Tipo | Default | Comportamento |
|-------|------|---------|---------------|
| `novoValorLimiteBrl` | decimal | `null` | Omitir = preserva valor atual |
| `garantiasExigidas` | array | `null` | `null` = preserva; `[]` = remove todas; itens = replace-all |
| `configurarAntecipacao` | bool | `false` | `false` = não toca nenhum campo de antecipação |
| `padraoAntecipacao` | string | `null` | Só aplicado quando `configurarAntecipacao: true` |
| `breakFundingFeePct` | decimal | `null` | Idem |
| `tlaPctSobreSaldo` | decimal | `null` | Idem |
| `tlaPctPorMesRemanescente` | decimal | `null` | Idem |
| `valorMinimoParcialPct` | decimal | `null` | Idem |
| `observacoesAntecipacao` | string | `null` | Idem |

---

## Interfaces TypeScript Atualizadas

```typescript
// BancoDto — sem campos de cálculo de antecipação
interface BancoDto {
  id: string;
  codigoCompe: string;
  razaoSocial: string;
  apelido: string;
  aceitaLiquidacaoTotal: boolean;
  aceitaLiquidacaoParcial: boolean;
  exigeAnuenciaExpressa: boolean;
  exigeParcelaInteira: boolean;
  avisoPrevioMinDiasUteis: number;
  createdAt: string; // ISO 8601
  updatedAt: string;
}

// Enum de padrões — use string na serialização
type PadraoAntecipacao = 'A' | 'B' | 'C' | 'D' | 'E';

// LimiteBancoDto — agora inclui campos de antecipação
interface LimiteBancoDto {
  id: string;
  bancoId: string;
  modalidade: string;
  valorLimiteBrl: number;
  valorUtilizadoBrl: number;
  valorDisponivelBrl: number;
  dataVigenciaInicio: string; // YYYY-MM-DD
  dataVigenciaFim: string | null;
  observacoes: string | null;
  // Campos de antecipação — novos em S32
  padraoAntecipacao: PadraoAntecipacao | null;
  breakFundingFeePct: number | null;       // % humano, ex.: 0.5 = 0,5%
  tlaPctSobreSaldo: number | null;
  tlaPctPorMesRemanescente: number | null;
  valorMinimoParcialPct: number | null;
  observacoesAntecipacao: string | null;
  // ---
  createdAt: string;
  updatedAt: string;
  garantiasExigidas: GarantiaExigidaLimiteDto[];
  historico: LimiteBancoHistoricoDto[];
}

// Request de criação de limite — agora inclui antecipação
interface CreateLimiteBancoRequest {
  bancoId: string;
  modalidade: string;
  valorLimiteBrl: number;
  dataVigenciaInicio: string;      // YYYY-MM-DD
  dataVigenciaFim?: string;
  observacoes?: string;
  padraoAntecipacao?: PadraoAntecipacao;
  breakFundingFeePct?: number;
  tlaPctSobreSaldo?: number;
  tlaPctPorMesRemanescente?: number;
  valorMinimoParcialPct?: number;
  observacoesAntecipacao?: string;
  garantiasExigidas?: CriarGarantiaExigidaLimiteRequest[];
}

// Request de atualização de limite — flag obrigatória para tocar antecipação
interface UpdateLimiteBancoRequest {
  novoValorLimiteBrl?: number;
  garantiasExigidas?: CriarGarantiaExigidaLimiteRequest[] | null;
  configurarAntecipacao?: boolean;         // deve ser true para os campos abaixo serem aplicados
  padraoAntecipacao?: PadraoAntecipacao | null;
  breakFundingFeePct?: number | null;
  tlaPctSobreSaldo?: number | null;
  tlaPctPorMesRemanescente?: number | null;
  valorMinimoParcialPct?: number | null;
  observacoesAntecipacao?: string | null;
}

// Request de criação de banco — simplificado
interface CreateBancoRequest {
  codigoCompe: string;
  razaoSocial: string;
  apelido: string;
  // padraoAntecipacao REMOVIDO — configurar no LimiteBanco correspondente
}

// Request de atualização de config do banco — apenas restrições institucionais
interface UpdateBancoConfigRequest {
  aceitaLiquidacaoTotal: boolean;
  aceitaLiquidacaoParcial: boolean;
  exigeAnuenciaExpressa: boolean;
  exigeParcelaInteira: boolean;
  avisoPrevioMinDiasUteis: number;
  // padraoAntecipacao, breakFundingFeePct etc. REMOVIDOS
}
```

---

## Checklist de Migração FE

- [ ] Remover leitura de `padraoAntecipacao` e parâmetros de cálculo do objeto `BancoDto`
- [ ] Remover campo `padraoAntecipacao` do formulário `POST /api/v1/bancos`
- [ ] Remover campos de cálculo do formulário `PUT /api/v1/bancos/{id}/config-antecipacao`
- [ ] Adicionar campos de antecipação ao formulário de criação de limite (`POST /api/v1/limites-banco`)
- [ ] Adicionar campos de antecipação ao formulário de edição de limite (`PATCH /api/v1/limites-banco/{id}`) com a flag `configurarAntecipacao: true`
- [ ] Exibir `padraoAntecipacao` e parâmetros a partir do `LimiteBancoDto`, não do `BancoDto`
- [ ] Atualizar interfaces TypeScript conforme a seção acima
- [ ] Atualizar mocks/fixtures de testes que usavam os campos removidos do `BancoDto`

---

## Referências

- Commits: `e0072d7` (refactoring S32), `92dbaab` (revisão pós-review)
- Migration: `20260522111616_S32_AntecipacaoParaBancoModalidade` + `20260522135929_S32b_PadraoAntecipacaoSmallint`
- Docs de antecipação: `docs/simulacoes/integracao-frontend.md`
- Cockpit FE: `docs/api/cockpit-fe-guide.md` e `docs/12_FE_COCKPIT_HANDOVER.md`
- Dúvidas técnicas: w.soares@proxysgroup.com
