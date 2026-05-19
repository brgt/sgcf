# Golden Dataset — Quadro da Divida 2026

## Premissas

- Saldo inicial em 01/01/2026: R$ 10.000.000,00 distribuídos entre 3 bancos
  - BB (001): R$ 5.000.000,00
  - Bradesco (002): R$ 3.000.000,00
  - Itau (003): R$ 2.000.000,00
- 4 amortizações de principal pré-programadas ao longo do ano:
  - Jan/2026 — BB: R$ 500.000,00
  - Fev/2026 — Bradesco: R$ 300.000,00
  - Jun/2026 — Itau: R$ 1.000.000,00
  - Set/2026 — BB: R$ 1.000.000,00
- 0 captações simuladas (cenário base, somente amortizações reais)
- Sem cambial (todos os valores em BRL)
- Sem juros (apenas amortização de principal afeta o saldo do quadro — AD-6)

## Evolucao mensal do saldo total

| Mes        | Saldo Inicio     | Amortizacao  | Saldo Fim        |
|------------|-----------------|--------------|-----------------|
| Jan/2026   | R$ 10.000.000   | R$ 500.000   | R$  9.500.000   |
| Fev/2026   | R$  9.500.000   | R$ 300.000   | R$  9.200.000   |
| Mar-Mai    | R$  9.200.000   | —            | R$  9.200.000   |
| Jun/2026   | R$  9.200.000   | R$ 1.000.000 | R$  8.200.000   |
| Jul-Ago    | R$  8.200.000   | —            | R$  8.200.000   |
| Set/2026   | R$  8.200.000   | R$ 1.000.000 | R$  7.200.000   |
| Out-Dez    | R$  7.200.000   | —            | R$  7.200.000   |

## Resultado esperado

- Saldo final em 31/12/2026: R$ 7.200.000,00
- Total amortizado no ano: R$ 2.800.000,00
- Variacao anual: -28,00%

## Tolerancia

R$ 1,00 por arredondamento de centavos acumulado (HalfUp a 6 casas intermediarias,
2 casas no saldo final). Valores inteiros neste cenario eliminam risco de acumulo.

## Origem dos numeros

Cenario sintetico representativo — nao vinculado a planilha real. Os valores foram
escolhidos para serem divisíveis sem fracao decimal, o que elimina ambiguidade de
arredondamento e torna o golden determinístico sem tolerancia efetiva.

Quando a planilha `documentos/Endividamento.xlsx` for usada como fonte canonica,
este dataset pode ser substituido por uma copia direta da aba Quadro_da_Divida do
mes de janeiro/2026 para validacao contra dados reais.

## Como rodar

```bash
dotnet test tests/Sgcf.GoldenDataset/ --filter "FullyQualifiedName~QuadroDivida2026" --nologo
```
