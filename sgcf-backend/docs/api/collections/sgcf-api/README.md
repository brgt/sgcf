# SGCF API — Bruno Collection

Coleção de requests HTTP para validar e operar a SGCF API.

## Como usar

1. Instale o [Bruno](https://www.usebruno.com/) (open-source, free, versionável em git).
2. Abra o Bruno → **Open Collection** → selecione esta pasta `sgcf-api/`.
3. Selecione o environment **Dev** no canto superior direito.
4. Em **Dev**, ajuste `token` para um JWT válido (em desenvolvimento, qualquer string Bearer é aceita).

## Variáveis de Environment

| Variável | Descrição |
|----------|-----------|
| `baseUrl` | URL base da API (sem barra no final) |
| `token` | JWT Bearer para autenticação |
| `bancoId` | Preenchido após criar/listar um banco |
| `contratoId` | Preenchido após criar/listar um contrato |
| `garantiaId` | Preenchido após criar uma garantia |
| `hedgeId` | Preenchido após criar um hedge |
| `planoContaId` | Preenchido após criar/listar uma conta contábil |
| `parametroCotacaoId` | Preenchido após criar/listar um parâmetro |

Os IDs são salvos automaticamente pelos scripts pós-resposta dos requests `POST` e `GET` (listagem).

## Ordem recomendada de execução (Fase 1 — validação)

Siga esta ordem ao validar a API pela primeira vez:

```
00-Health/
  └─ Health Check                     (sanidade da API)

01-Bancos/
  └─ Criar Banco                      (cadastrar bancos dos contratos)

02-Plano-Contas/
  └─ Criar Conta                      (contas básicas se ainda não houver seed)

03-Parametros-Cotacao/
  └─ Criar Parametro                  (PTAX, SPOT, etc.)

04-Contratos/
  └─ Criar Contrato FINIMP            (criar primeiro contrato)
  └─ Gerar Cronograma                 (calcular parcelas)
  └─ Tabela Completa                  (validar demonstrativo)

05-Garantias/
  └─ Adicionar Garantia CDB           (vincular garantia ao contrato)

06-Hedges/
  └─ Adicionar Hedge ao Contrato      (vincular hedge)
  └─ Calcular MTM                     (validar cálculo)

07-Painel/
  └─ Divida Consolidada               (validar consolidação)
  └─ KPIs Executivos                  (validar KPIs)

08-Simulador/
  └─ Cenario Cambial                  (validar simulação)
```

## Convenções dos arquivos

- Arquivos `.bru` são prefixados com número de sequência (`1 - `, `2 - `).
- Requests de listagem (`GET /`) executam um script para extrair o primeiro ID e salvar como variável do environment.
- Payloads de exemplo usam dados realistas (FINIMP USD, LEI4131 com SBLC, etc.).

## Seed de dados (opcional)

Para semear o ambiente dev com bancos + plano de contas + parâmetros sem rodar manualmente:

```bash
# Usando REST Client do VS Code:
docs/api/collections/seed-dev.http
```
