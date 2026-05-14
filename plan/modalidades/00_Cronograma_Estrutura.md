# 00 — Cronograma de Parcelas: Estrutura de Vencimentos e Periodicidades

**Perguntas do PO atendidas**:

- Q1 — _"Na tela de identificação possui um campo Data de Vencimento. Para um FINIMP, faz sentido ter data única, mas como funcionará para contratos parcelados como FGI com vencimento em data fixa por mês?"_
- Q3 — _"Alguns bancos têm FINIMP de até 720 dias com quitações semestrais. O sistema prevê?"_
- Q6 — _"Na 4131 existem bancos que operam com 24 meses e quitações trimestrais, outros semestrais e outros mensais. Como o sistema prevê?"_

**Resposta direta**: A estrutura de dados `EventoCronograma` 1:N **já existe e suporta qualquer periodicidade**. O que **falta** é (1) a UI capturar periodicidade no cadastro em vez de pedir uma data única e (2) a Strategy de geração de cronograma estar completa para os 7 padrões de mercado.

---

## 1. Resumo executivo

O sistema deve modelar o vencimento de um contrato como **lista de eventos** (`EventoCronograma`), não como atributo `DataVencimento` único. A escolha entre bullet, mensal, trimestral, semestral, anual e customizada é uma **propriedade do contrato** (campo `Periodicidade`), e o cronograma resultante é gerado por uma Strategy. O `EventoCronograma` já está implementado no domínio (`EventoCronograma.cs:1-84`); a lacuna está no wizard (que ainda pede `Data de Vencimento` única em `ContratoCreatePage.vue:514-530`) e na cobertura completa das Strategies de geração.

---

## 2. Cenário de negócio

### 2.1 Padrões de quitação observados

| Modalidade              | Padrão típico                                               | Banco / referência                                      |
| ----------------------- | ----------------------------------------------------------- | ------------------------------------------------------- |
| FINIMP curto (≤ 360d)   | Bullet (principal + juros no vencimento)                    | Sicredi, Itaú, BB                                       |
| FINIMP longo (até 720d) | Semestral (2 ou 4 parcelas)                                 | BB                                                      |
| 4131                    | Trimestral, semestral ou mensal — varia por banco e tomador | Santander (trimestral), BB (semestral), outros (mensal) |
| FGI                     | Mensal, em data fixa do mês (ex.: todo dia 15)              | BV, demais bancos                                       |
| NCE/CCE                 | Mensal ou bullet                                            | Diverso                                                 |
| Balcão (Caixa)          | Customizada / escalonada por antecipação de duplicatas      | CAIXA                                                   |
| REFINIMP                | Bullet ou herda do contrato-mãe                             | Itaú                                                    |

### 2.2 Casos práticos numéricos

**Caso A — FINIMP BB 720 dias, 2 parcelas semestrais (Pergunta 3)**

- Contratação: 14/05/2026
- Valor: USD 1.000.000
- Parcelas:
  - Evento 1: principal USD 500.000 + juros provisionados em 14/11/2026 (D+180, ajustado para próximo dia útil)
  - Evento 2: principal USD 500.000 + juros + IRRF gross-up em 14/05/2028 (D+720)
- _(Em alguns contratos BB o cronograma é com 3 ou 4 quotas semestrais — totalmente parametrizável.)_

**Caso B — 4131 Santander 24 meses, 8 parcelas trimestrais (Pergunta 6)**

- Contratação: 14/05/2026
- Valor: USD 5.000.000
- Parcelas: 8 eventos a cada 90 dias corridos, ajustados para dia útil. Principal pode ser SAC (US$ 625.000 cada) ou Bullet com juros trimestrais e principal único no fim.

**Caso C — FGI BV mensal, dia fixo 15 (Pergunta 1)**

- Contratação: 14/05/2026
- Valor: BRL 600.000
- 60 parcelas mensais (PRICE ou SAC), todo dia 15, primeira parcela em 15/06/2026, última em 15/05/2031.

**Caso D — Cronograma customizado (Balcão Caixa)**

- Cessão de duplicatas com vencimentos arbitrários definidos pelo tomador. Cronograma é importado/digitado linha a linha, não gerado por regra.

### 2.3 Estruturas de amortização

| Estrutura                       | Comportamento                                                | Modalidades típicas                |
| ------------------------------- | ------------------------------------------------------------ | ---------------------------------- |
| **Bullet**                      | Principal único no fim; juros podem ser bullet ou periódicos | FINIMP curto, NCE, REFINIMP        |
| **Price (parcelas iguais)**     | Parcela total constante; juros maiores no início             | FGI, financiamentos de longo prazo |
| **SAC**                         | Principal constante; parcela total decrescente               | 4131, FGI                          |
| **Customizada**                 | Importada linha a linha                                      | Balcão, contratos sob medida       |
| **Bullet com juros periódicos** | Principal só no fim, mas juros semestrais/anuais             | FINIMP 720d, alguns 4131           |

---

## 3. Estado atual no sistema

### 3.1 O que existe

- **Domínio**: `Sgcf.Domain/Cronograma/EventoCronograma.cs:1-84` — entidade 1:N por contrato, com:
  - `NumeroEvento`, `TipoEventoCronograma`
  - `DataPrevista` (LocalDate)
  - `ValorMoedaOriginal` (Money), `ValorBrlEstimado`, `SaldoDevedorApos`
  - `Status: StatusEventoCronograma { Previsto, Pago, Atrasado }`
  - Campos de efetivação: `DataPagamentoEfetivo`, `ValorPagamentoEfetivo`, `TaxaCambioPagamento`
- **Application**: `Sgcf.Application/Contratos/Commands/GerarCronogramaCommand.cs` (em modificação no git status).
- **Repositório**: `Sgcf.Infrastructure/Persistence/Repositories/EventoCronogramaRepository.cs` (referenciado em `IEventoCronogramaRepository.cs`).

### 3.2 Limitações observadas

| Limitação                                                        | Local                                       |
| ---------------------------------------------------------------- | ------------------------------------------- |
| Wizard pede **data única** "Data de Vencimento"                  | `ContratoCreatePage.vue:514-530`            |
| Não há enum `Periodicidade` exposto no contrato                  | Falta no domínio                            |
| Não há enum `EstruturaAmortizacao` exposto                       | Falta no domínio                            |
| Não há Strategy de geração formalizada (Bullet/Price/SAC/Custom) | Backlog em `plan/Anexo_B...md:321-331`      |
| Ajuste de dia útil nas datas geradas                             | Pendente — ver `00_DiasUteis_Calendario.md` |

---

## 4. GAPs identificados

| #   | GAP                                                                                                                  | Severidade | Quem afeta                                  |
| --- | -------------------------------------------------------------------------------------------------------------------- | ---------- | ------------------------------------------- |
| G1  | Wizard captura `DataVencimento` único em vez de tripla (Periodicidade + DataPrimeiroVencimento + QuantidadeParcelas) | Alta       | Todas as modalidades parceladas             |
| G2  | Enum `Periodicidade` não existe no domínio                                                                           | Alta       | Todas                                       |
| G3  | Enum `EstruturaAmortizacao` não existe (apenas `TipoEventoCronograma` por evento)                                    | Alta       | FGI, 4131                                   |
| G4  | Strategy de geração só cobre Bullet (assumindo o que está em GerarCronogramaCommand)                                 | Alta       | FGI (Price), 4131 (SAC ou Bullet com juros) |
| G5  | Não há suporte a "data fixa do mês" (dia 15 todo mês)                                                                | Alta       | FGI                                         |
| G6  | Não há rota para importar cronograma customizado                                                                     | Média      | Balcão                                      |
| G7  | Cronograma não recalcula automaticamente quando há mudança de taxa ou pré-pagamento                                  | Média      | FGI pós-fixada                              |

---

## 5. Proposta de estrutura

### 5.1 Novos enums no domínio

```csharp
public enum Periodicidade
{
    Bullet,        // pagamento único no fim
    Mensal,
    Bimestral,
    Trimestral,
    Quadrimestral,
    Semestral,
    Anual,
    Customizada    // cronograma manual, datas livres
}

public enum EstruturaAmortizacao
{
    Bullet,                 // 1 parcela de principal no fim
    Price,                  // parcelas iguais
    Sac,                    // principal constante
    BulletComJurosPeriodicos, // principal no fim + juros recorrentes
    Customizada
}

public enum AnchorDiaMes
{
    DiaContratacao,    // replica o dia da contratação
    DiaFixo,           // usa AnchorDiaFixo (ex.: 15)
    UltimoDiaUtil,     // último dia útil do mês
    PrimeiroDiaUtil    // primeiro dia útil do mês
}
```

### 5.2 Novos campos no `Contrato`

| Campo                    | Tipo                   | Obrigatório                                   | Default          |
| ------------------------ | ---------------------- | --------------------------------------------- | ---------------- |
| `Periodicidade`          | `Periodicidade`        | Sim                                           | —                |
| `EstruturaAmortizacao`   | `EstruturaAmortizacao` | Sim                                           | —                |
| `DataPrimeiroVencimento` | `LocalDate`            | Sim                                           | —                |
| `QuantidadeParcelas`     | `int`                  | Sim (≥ 1)                                     | 1                |
| `AnchorDiaMes`           | `AnchorDiaMes`         | Não (default `DiaContratacao`)                | `DiaContratacao` |
| `AnchorDiaFixo`          | `int` (1-31)           | Condicional (quando `AnchorDiaMes = DiaFixo`) | —                |
| `PeriodicidadeJuros`     | `Periodicidade?`       | Não (default = `Periodicidade`)               | herda principal  |

**Remover/depreciar**: `DataVencimento` único do payload do wizard. Para Bullet, o sistema deduz `DataVencimento = DataPrimeiroVencimento`.

### 5.3 Strategy pattern de geração

```csharp
public interface ICronogramaStrategy
{
    IReadOnlyList<EventoCronograma> Gerar(GerarCronogramaInput input);
}

// Implementações:
//  - BulletStrategy
//  - PriceStrategy
//  - SacStrategy
//  - BulletComJurosPeriodicosStrategy
//  - CustomizadaStrategy (consome lista pré-definida)
```

Seleção da Strategy: `CronogramaStrategyFactory.Create(contrato.EstruturaAmortizacao)`.

### 5.4 Algoritmo de geração de datas

```
para i de 1 até QuantidadeParcelas:
    base = DataPrimeiroVencimento + (i-1) * passo(Periodicidade)
    se AnchorDiaMes == DiaFixo:
        base = MesDe(base) com dia = AnchorDiaFixo (capping no último dia se 31 em fevereiro)
    data_prevista = IBusinessDayCalendar.AjustarPorConvencao(base, contrato.ConvencaoDataNaoUtil)
    persiste EventoCronograma
```

`passo(Periodicidade)` retorna: 1 mês, 2 meses, 3 meses, 6 meses, 12 meses, etc.

### 5.5 Tipos de evento dentro do cronograma

O `TipoEventoCronograma` já permite separar PRINCIPAL, JUROS, IRRF, IOF, COMISSAO_SBLC, TARIFA_ROF (ver `plan/Anexo_B...md:182-193`). A Strategy gera eventos do tipo `PRINCIPAL` e `JUROS` separados quando `PeriodicidadeJuros != Periodicidade` (caso BulletComJurosPeriodicos).

### 5.6 Recálculo de cronograma

Eventos disparadores:

1. Pagamento de parcela com valor diferente do previsto (gera ajuste de saldo).
2. Antecipação total ou parcial (regras em `plan/Anexo_C_Regras_Antecipacao_Pagamento.md`).
3. Reajuste de taxa pós-fixada (ver `00_TaxasPosFixadas_Indexadores.md`).
4. Repactuação contratual (gera novo cronograma vinculado à versão).

Cada recálculo gera nova versão do cronograma (não destrutivo); a versão ativa é a corrente, as anteriores ficam para auditoria.

---

## 6. Impacto no wizard

Detalhado em `00_Wizard_Fluxo_Cadastro.md`. Resumo:

- Step 0 (novo): Modalidade
- Step 1: Identificação inclui `Periodicidade`, `EstruturaAmortizacao`, `DataPrimeiroVencimento`, `QuantidadeParcelas`, `AnchorDiaMes/AnchorDiaFixo` (condicional).
- Step 2: Detalhes específicos por modalidade.
- Step 3 (novo): **Pré-visualização do cronograma gerado** — usuário vê as parcelas antes de salvar; pode editar pontualmente.

---

## 7. Impacto em APIs

- `POST /api/contratos` — payload ganha campos da §5.2; remover `dataVencimento`.
- `GET /api/contratos/{id}/cronograma` — retorna lista de `EventoCronograma`.
- `POST /api/contratos/{id}/cronograma/recalcular` — força regeneração (uso administrativo).
- `POST /api/contratos/{id}/cronograma/importar` — upload de cronograma customizado (Balcão).

---

## 8. Critérios de aceite

- [ ] Enums `Periodicidade`, `EstruturaAmortizacao`, `AnchorDiaMes` adicionados ao domínio
- [ ] Campos novos no `Contrato` migrados (com default razoável para contratos legados se houver)
- [ ] 5 Strategies implementadas e cobertas por teste unitário (Bullet, Price, SAC, BulletComJurosPeriodicos, Customizada)
- [ ] Geração de cronograma sempre passa por `IBusinessDayCalendar`
- [ ] Wizard refatorado: remove `DataVencimento` único, adiciona tripla periodicidade + qty + 1º venc.
- [ ] Cenários 3a/3b/3c/3d da §2.2 cobertos por testes de integração
- [ ] Endpoint de pré-visualização (`POST /api/contratos/cronograma-preview`) implementado para a UI
- [ ] Versionamento de cronograma: recálculos não destroem versão anterior

---

## 9. Pontos em aberto

| #   | Decisão pendente                                                       | Recomendação                                                                     |
| --- | ---------------------------------------------------------------------- | -------------------------------------------------------------------------------- |
| D1  | "Mensal com dia 31" em meses curtos: capping no último dia ou rolling? | Capping (dia 28/29/30/31 do mês corrente, recomendado)                           |
| D2  | Permitir editar parcela individual após gerar?                         | Sim, com auditoria (`MotivoSobrescrita`)                                         |
| D3  | Cronograma de juros separado do principal por padrão?                  | Sim para `BulletComJurosPeriodicos`; senão eventos PRINCIPAL+JUROS na mesma data |
| D4  | Mudança de periodicidade em contrato existente                         | Tratada como repactuação → nova versão de cronograma                             |

---

## 10. Referências

- `Sgcf.Domain/Cronograma/EventoCronograma.cs`
- `Sgcf.Application/Contratos/Commands/GerarCronogramaCommand.cs`
- `sgcf-frontend/src/pages/ContratoCreatePage.vue:404-580`
- `plan/Anexo_B_Modalidades_e_Modelo_Dados.md:182-193, 321-331, 364-381`
- `plan/Anexo_C_Regras_Antecipacao_Pagamento.md`
- `00_DiasUteis_Calendario.md` (este pacote)
