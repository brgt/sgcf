# 00 — Dias Úteis e Calendário de Feriados

**Pergunta do PO atendida**: Q2 — *"Quando uma data cai no final de semana ou feriado os bancos assumem que o recebimento será no próximo dia útil, isto está previsto no sistema?"*

**Resposta direta**: **Não, atualmente o sistema não trata dia não útil.** Este documento descreve o que precisa ser construído.

---

## 1. Resumo executivo

Os contratos bancários brasileiros seguem a convenção de mercado **Following Business Day** (próximo dia útil) para vencimentos que recaiam em sábado, domingo, feriado nacional, feriado bancário ou data sem expediente segundo o calendário ANBIMA. Em casos específicos de contratos cambiais e exportação, aplica-se também a convenção **Modified Following** (próximo dia útil, exceto se cair no mês seguinte — nesse caso, dia útil anterior). O sistema atual armazena datas previstas como `LocalDate` puro, sem ajuste algum, e não há serviço de calendário. Propõe-se criar:

1. Entidade `Feriado` (tabela persistida, atualizada anualmente a partir do calendário ANBIMA).
2. Serviço `IBusinessDayCalendar` com operações de teste, avanço e contagem em dias úteis.
3. Campo `ConvencaoDataNaoUtil` no `Contrato` (default `Following`).
4. Aplicação automática da convenção na geração do cronograma e na validação de pagamentos.

---

## 2. Cenário de negócio

### 2.1 Convenções de mercado

| Convenção | Regra | Uso típico |
|-----------|-------|-----------|
| `Following` | Próximo dia útil | FINIMP, FGI, NCE, Balcão — padrão de mercado |
| `ModifiedFollowing` | Próximo dia útil; se ultrapassar o mês, recua para dia útil anterior | NDF, swaps, derivativos atrelados a mês de competência |
| `Preceding` | Dia útil anterior | Pouco usado em empréstimos no Brasil |
| `ModifiedPreceding` | Dia útil anterior; se recuar para mês anterior, avança para próximo | Raro |
| `Unadjusted` | Mantém a data, paga no próximo expediente sem reajustar registro | Contratos antigos / informais |

### 2.2 Exemplos práticos

- **FINIMP BB**: parcela prevista para 25/12/2026 (Natal) → pagamento ajustado para 26/12/2026, mas como é sábado → 28/12/2026.
- **NDF Itaú**: vencimento previsto 30/11/2026 (segunda). Se fosse 30/11/2024 (sábado), e dezembro inicia útil em 02/12 → `ModifiedFollowing` mantém em novembro recuando para 29/11/2024 (sexta).
- **FGI BV**: parcela em dia fixo mensal (todo dia 15). Quando 15 cai em sábado, paga segunda dia 17.

### 2.3 Calendários de referência

| Fonte | Cobertura | URL/Doc |
|-------|----------|---------|
| **ANBIMA** | Feriados nacionais e bancários relevantes para o mercado financeiro | `anbima.com.br/feriados` (planilha XLS anual) |
| **B3** | Pregão da bolsa (deriva da ANBIMA) | `b3.com.br/pt_br/regulacao/estrutura-normativa/calendario-de-feriados` |
| **BACEN** | Feriados bancários nacionais | Decreto Federal 9.093/2017 + portarias anuais |
| **Lei Federal 662/1949** | Define feriados nacionais civis | Base legal |

Recomenda-se adotar o calendário **ANBIMA** como referência primária, pois cobre os três conjuntos relevantes para empréstimos no Brasil (feriados nacionais, bancários e dias sem expediente em centros financeiros como São Paulo e Rio).

### 2.4 Feriados locais (municipais e estaduais)

A maioria dos bancos opera com calendário **nacional** para fins de cronograma de contratos, **ignorando feriados municipais/estaduais**. Exceções:

- Algumas agências físicas seguem feriado municipal, mas o sistema central do banco mantém data nacional.
- Para o SGCF, recomenda-se **não considerar feriados municipais** no cálculo de cronograma, apenas registrá-los como `Escopo = Regional` para fins informativos.

---

## 3. Estado atual no sistema

### 3.1 O que existe

- `Sgcf.Domain/Cronograma/EventoCronograma.cs:1-84` — campo `DataPrevista` como `LocalDate`, sem qualquer ajuste de dia útil.
- `Sgcf.Application/Contratos/Commands/GerarCronogramaCommand.cs` — gera datas de parcelas sem consultar calendário (referência: arquivo modificado em git status).
- Nenhum serviço de calendário, nenhuma tabela de feriados, nenhuma lib de dias úteis integrada.

### 3.2 Limitações observadas

- Cronograma gerado pode conter datas em sábado, domingo ou feriado.
- Cálculo de juros por dias úteis (base 252) é impossível sem contar feriados.
- Validação de pagamento atrasado não diferencia atraso real de atraso por feriado.
- Relatórios de vencimentos próximos podem alertar para datas que serão pagas em outro dia útil.

---

## 4. GAPs identificados

| # | GAP | Severidade | Quem afeta |
|---|-----|-----------|-----------|
| G1 | Não existe entidade `Feriado` nem tabela persistida | Alta | Todas as modalidades |
| G2 | Não existe serviço `IBusinessDayCalendar` | Alta | Geração de cronograma, validação de pagamento |
| G3 | `EventoCronograma.DataPrevista` é gerada sem ajuste | Alta | Todas as modalidades |
| G4 | Não há campo `ConvencaoDataNaoUtil` no `Contrato` | Média | NDF e contratos cambiais |
| G5 | Cálculo de juros base 252 não é suportado | Média | FGI pós-fixada, contratos com CDI |
| G6 | Não existe rotina de atualização anual do calendário | Baixa | Operação |

---

## 5. Proposta de estrutura

### 5.1 Modelo de dados

**Nova tabela `feriado`**

| Campo | Tipo | Nullable | Descrição |
|-------|------|----------|-----------|
| `Id` | `Guid` | Não | PK |
| `Data` | `LocalDate` | Não | Data do feriado (único por escopo) |
| `Tipo` | `enum TipoFeriado` | Não | `Nacional`, `Bancario`, `BolsaB3`, `Regional` |
| `Escopo` | `enum EscopoFeriado` | Não | `Brasil`, `SaoPaulo`, `RioDeJaneiro`, ... |
| `Descricao` | `string(120)` | Não | Ex.: "Natal", "Sexta-feira da Paixão" |
| `FonteOrigem` | `enum FonteFeriado` | Não | `ANBIMA`, `B3`, `BACEN`, `Manual` |
| `AnoReferencia` | `int` | Não | Para indexação e rotina de atualização anual |
| `CreatedAt` / `UpdatedAt` | `Instant` | Não | Auditoria |

Índice único: `(Data, Tipo, Escopo)`.

**Alteração no `Contrato`**

| Campo novo | Tipo | Default | Descrição |
|------------|------|---------|-----------|
| `ConvencaoDataNaoUtil` | `enum` | `Following` | Aplica-se na geração do cronograma |

### 5.2 Contrato do serviço

```csharp
public interface IBusinessDayCalendar
{
    bool IsBusinessDay(LocalDate date);
    LocalDate NextBusinessDay(LocalDate date);          // exclusivo: pula a data se for útil? Ver §5.4
    LocalDate PreviousBusinessDay(LocalDate date);
    LocalDate AddBusinessDays(LocalDate date, int n);   // n pode ser negativo
    int CountBusinessDays(LocalDate start, LocalDate end); // [start, end)
    LocalDate AjustarPorConvencao(LocalDate date, ConvencaoDataNaoUtil convencao);
}
```

A implementação consulta a tabela `feriado` em cache em memória (refresh diário) e considera sábado/domingo como não úteis sempre.

### 5.3 Regras de aplicação

1. **Geração de cronograma**: ao calcular cada `DataPrevista`, aplicar `AjustarPorConvencao(data, contrato.ConvencaoDataNaoUtil)` antes de persistir.
2. **Validação de pagamento**: ao registrar pagamento, comparar `DataPagamentoEfetivo` com `DataPrevista` já ajustada. Atraso só conta após o próximo dia útil seguinte à data ajustada.
3. **Cálculo de juros base 252**: usar `CountBusinessDays` na fórmula `(1 + i)^(du/252) - 1`.
4. **Reagendamento manual**: usuário pode forçar uma `DataPrevista` arbitrária; o sistema valida que é dia útil ou registra `MotivoSobrescrita`.

### 5.4 Decisões pendentes (ponto em aberto)

| # | Decisão | Opções |
|---|---------|--------|
| D1 | Origem do calendário | (a) Tabela manual atualizada anualmente via planilha ANBIMA; (b) Lib `Nager.Date` (cobertura BR); (c) API BACEN (não há endpoint oficial estável) |
| D2 | Semântica de `NextBusinessDay(d)` | (a) Inclusivo: se `d` é útil, retorna `d`; (b) Exclusivo: sempre retorna data > `d` |
| D3 | Considerar feriado regional/municipal? | (a) Não (recomendado, alinhado com bancos); (b) Sim, configurável por contrato |
| D4 | Onde versionar a tabela? | (a) Seed via migration (cada ano vira uma migration); (b) Endpoint admin para upload de planilha; (c) Job que importa CSV da ANBIMA |

**Recomendação**: D1=(a), D2=(a) inclusivo, D3=(a) não considerar, D4=(b) upload por admin.

### 5.5 Impacto no wizard / UI

- Adicionar campo `Convenção em dia não útil` no Step 1 do cadastro (default `Following`, com tooltip explicando).
- Na revisão do cronograma (Step 3 / tela de detalhes), destacar visualmente datas que foram ajustadas e a data nominal original.

### 5.6 Impacto em APIs

- Novo endpoint `GET /api/feriados?ano=2026&escopo=Brasil`.
- Novo endpoint admin `POST /api/feriados/importar` (multipart com planilha ANBIMA).
- Endpoint `GET /api/calendario/dia-util?data=2026-12-25` retornando próximo/anterior útil.

---

## 6. Critérios de aceite

- [ ] Tabela `feriado` criada com pelo menos os feriados nacionais de 2026 e 2027 carregados via seed
- [ ] `IBusinessDayCalendar` implementado e coberto por testes unitários (mínimo: feriados móveis Páscoa, Corpus Christi, Carnaval)
- [ ] `EventoCronograma.DataPrevista` é sempre dia útil quando `ConvencaoDataNaoUtil != Unadjusted`
- [ ] Campo `ConvencaoDataNaoUtil` exposto no payload do contrato
- [ ] Cobertura de testes: caso 25/12 em sexta, sábado, domingo, segunda
- [ ] Rotina documentada de atualização anual do calendário
- [ ] Endpoint público de consulta `GET /api/feriados` paginado

---

## 7. Referências

- ANBIMA — Tabela de Feriados (`anbima.com.br/feriados`)
- Lei Federal 662/1949 e Lei 10.607/2002 (feriados civis)
- B3 — Calendário de pregão
- ISDA Definitions 2006, §4.12 (Business Day Conventions) — base internacional das convenções usadas no Brasil
- `Sgcf.Domain/Cronograma/EventoCronograma.cs`
- `Sgcf.Application/Contratos/Commands/GerarCronogramaCommand.cs`
