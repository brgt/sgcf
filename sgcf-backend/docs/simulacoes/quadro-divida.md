# Quadro da Dívida — Funcionamento e Detalhes

---

## 1. O que é o Quadro da Dívida

O Quadro da Dívida é a visão central da tesouraria: uma projeção de 12 meses que mostra, para cada banco credor, quanto de dívida existe no início e no fim de cada mês, quantas amortizações de principal vencem e quantas novas captações estão previstas.

A resposta do endpoint tem três partes:

- **Snapshot inicial** (`snapshotInicial`): saldo atual de cada banco apurado hoje, convertido para BRL usando PTAX D-1 para contratos em moeda estrangeira.
- **Projeção mensal** (`projecao.meses`): 12 objetos com breakdown por banco, calculados pela função pura `ProjetorSaldoMensal`.
- **Sumário anual** (`sumario`): totais consolidados para o ano inteiro.

---

## 2. Cálculo de projeção mensal — `ProjetorSaldoMensal`

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Painel/ProjetorSaldoMensal.cs`

O projetor recebe:
1. `saldoInicialPorBanco`: dicionário `BancoId → Money` com o saldo em BRL no primeiro dia do ano.
2. `eventos`: lista de `EventoProjecao` com tipo, data, banco e valor em BRL.
3. `ano`: ano civil a projetar.

E retorna um `QuadroDividaProjecao` com exatamente 12 `MesProjecao`.

### Tipos de evento

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Painel/TipoEventoProjecao.cs`

| Tipo | Efeito no saldo |
|---|---|
| `AmortizacaoPrincipal` | Reduz o saldo do banco no mês (parcela de principal vencendo). |
| `Captacao` | Aumenta o saldo do banco no mês (nova captação, real ou simulada). |

**Nota importante:** Juros não entram na projeção de saldo. O quadro mostra apenas movimentação de principal (AD-6). O custo financeiro está exposto em outros endpoints do painel.

### Fórmula por banco e mês

```
SaldoFim[banco, mês] = SaldoInicio[banco, mês]
                      - TotalAmortizacaoPrincipal[banco, mês]
                      + TotalCaptacao[banco, mês]
```

### Invariantes garantidas

| Invariante | Descrição |
|---|---|
| P-1 | `SaldoFim[banco, mês m] == SaldoInicio[banco, mês m+1]` para todo banco e m em 1..11. |
| P-2 | `SaldoTotalFim[mês] == Σ SaldoFim[mês, banco]` para todo banco com posição no mês. |
| P-3 | `Σ SharePercentual[mês] == 100 ± 0,01` quando `SaldoTotalFim[mês] > 0`. |
| P-4 | Banco sem saldo inicial mas com captação no ano é incluído a partir do mês da captação. |
| P-5 | Eventos com `Data.Year != ano` são ignorados silenciosamente. |
| P-6 | Banco sem saldo inicial e sem eventos não aparece no resultado. |

### Integração de cenários simulados

Quando `cenarioId` é informado na consulta, o `GetQuadroDividaQueryHandler` adiciona à lista de eventos os cronogramas calculados on-the-fly de cada `SimulacaoContratacao` do cenário. Esses eventos têm `Tipo = Captacao` e representam a entrada de principal na data prevista da captação.

O resultado é transparente para o front-end: os campos `totalCaptacaoMes` e `totalCaptacaoNoAno` incluem tanto captações reais quanto simuladas. A presença do cenário é indicada pelo campo `cenarioAplicado` na resposta.

---

## 3. Tetão mensal — configuração e alertas

### Conceito

O tetão mensal (`TetaoMensalCapacidadeBrl`) é um parâmetro global configurável pela equipe `Admin`. Quando configurado, qualquer mês da projeção em que a soma de amortizações e captações (`TotalAmortizacaoMes + TotalCaptacaoMes`) exceda esse valor gera um alerta no array `alertas` do `QuadroDividaDto`.

O tetão **não bloqueia** nenhuma operação — é apenas informativo. A lógica de validação é uma função pura.

### Onde está implementado

**Domínio (parâmetro):**  
`/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Sistema/ParametroSistema.cs`

**Validação (função pura):**  
`/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Application/Painel/ValidadorTetaoMensal.cs`

### Regra de geração de alerta

```
movimentacao_mensal = TotalAmortizacaoMes + TotalCaptacaoMes

Se movimentacao_mensal > TetaoMensalCapacidadeBrl:
    alertas.Add("Mês {MM}/{YYYY}: movimentação R$ {movimentacao:N2} excede tetão configurado R$ {tetão:N2}.")
```

### Exemplo de resposta com alertas

```json
{
  "alertas": [
    "Mês 09/2026: movimentação R$ 62.500.000,00 excede tetão configurado R$ 50.000.000,00.",
    "Mês 10/2026: movimentação R$ 58.200.000,00 excede tetão configurado R$ 50.000.000,00."
  ]
}
```

Quando `TetaoMensalCapacidadeBrl` é `null` (sem limite configurado), o array `alertas` é sempre vazio `[]`.

### Como o front-end deve exibir alertas

Renderize cada string do array `alertas` como um aviso não-bloqueante na interface. O usuário deve ver os alertas, mas não deve ser impedido de salvar ou ativar cenários por causa deles.

---

## 4. `ParametroSistema` — singleton

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Domain/Sistema/ParametroSistema.cs`

O `ParametroSistema` é um singleton (chave fixa `"GLOBAL"` na tabela). No MVP, contém apenas o tetão mensal. O design permite adicionar novas configurações globais no futuro sem alterar a estrutura da tabela.

**Constraints do tetão:**
- Deve ser em BRL.
- Não pode ser negativo.
- `null` remove o limite.

---

## 5. Integração MCP (tools read-only)

Arquivo: `/Users/welysson/Library/CloudStorage/GoogleDrive-w.soares@proxysgroup.com/Meu Drive/Governança/Projetos/Agentes de Finanças/sgcf-backend/src/Sgcf.Mcp/Tools/SimulacaoTools.cs`

O adapter MCP expõe três tools read-only que agentes externos (ex: co-piloto de finanças) podem invocar:

### `get_quadro_divida`

```
Parâmetros:
  ano        (int)     — Ano da projeção (ex: 2026).
  cenarioId  (string?) — Id UUID do cenário de simulação (opcional).

Retorna: QuadroDividaDto serializado como JSON.
Policy exigida: Leitura
```

### `list_cenarios_simulacao`

```
Parâmetros:
  status   (string?) — Filtro: "Rascunho", "Ativo" ou "Arquivado".
  anoBase  (int?)    — Filtro por ano-base.

Retorna: lista de CenarioSimulacaoResumoDto serializada como JSON.
Policy exigida: Leitura
```

### `get_cenario_simulacao`

```
Parâmetros:
  id (string) — Id UUID do cenário.

Retorna: CenarioSimulacaoDto serializado como JSON, ou {"error": "..."} se não encontrado.
Policy exigida: Leitura
```

### Segurança das tools MCP

As tools MCP não passam pelo middleware `[Authorize]` do ASP.NET Core. Por isso, cada tool chama `EnsurePolicyAsync(Policies.Leitura)` manualmente antes de invocar o MediatR, usando o mesmo `IAuthorizationService` registrado em `Program.cs`. Se o contexto HTTP não tiver identidade autenticada, lança `UnauthorizedAccessException`.

---

## 6. Limitações do MVP

| Limitação | Descrição |
|---|---|
| Apenas o ano corrente (Q9) | O endpoint do Quadro da Dívida retorna 409 se o ano informado for diferente do ano corrente do servidor. |
| Conversão de moeda flat | A conversão de contratos em USD/EUR/JPY/CNY usa PTAX D-1 constante para todos os 12 meses. Para análise de cenários cambiais, use `POST /painel/simulador/cenario-cambial`. |
| Sem múltiplos anos | A projeção retorna sempre 12 meses do ano informado. Não há suporte a projeções multi-anuais. |
| Sem persistência de cronograma | Cronogramas das simulações são calculados on-the-fly a cada consulta ao Quadro da Dívida (com cache Redis de 60s). |
