# SPEC — Modalidade Capital de Giro no Módulo de Cotações

**Versão alvo:** v0.9.0 (paralelo com FGI)
**Status:** Pendente de implementação
**Pré-requisito:** Onda 0 (`docs/specs/cotacoes/modalidades/onda-0.md`)
**Plano de execução:** `tasks/cotacoes-modalidades/capital-de-giro/plan.md`
**Plano mestre:** `tasks/cotacoes-modalidades/plan.md`
**Decisões trancadas:** MD-1..MD-10 + correção de domínio em 2026-05-18 (rename `BalcaoCaixa` → `CapitalDeGiro`; remoção de `TipoProduto`; separação clara FGI-modalidade × FGI-garantia)

---

## 1. Objetivo

Habilitar o módulo de Cotações para receber, comparar e converter propostas da modalidade **Capital de Giro** — linha de crédito BRL universal ofertada por qualquer banco comercial (Itaú, Bradesco, Santander, BB, Caixa, Sicredi, etc.) para necessidades de fluxo de caixa de pessoa jurídica. A entrega permite que o operador de Tesouraria registre cotações 100% em BRL, sem PTAX D-1 e sem NDF, e converta a proposta aceita em `Contrato` + `CapitalDeGiroDetail`.

### 1.1. Não-objetivos

- **Gerar cronograma automaticamente.** O cronograma de Capital de Giro varia conforme o banco emissor e a estrutura negociada. Quando o banco entrega cronograma fechado (carência irregular, parcelas heterogêneas), o operador importa via `POST /api/v1/contratos/{id}/cronograma/importar` (`ImportarCronogramaCommand` já existente). Quando o cronograma é regular (Price/SAC/Bullet com parcelas homogêneas), `GerarCronogramaCommand` calcula. Os dois caminhos coexistem.
- **Recálculo de CET após importação.** O CET calculado no momento da conversão (com base nas premissas da proposta) fica congelado em `EconomiaNegociacao` por `SPEC.md` §12.3.
- **Modelar FGI.** FGI é modalidade própria (`docs/specs/cotacoes/modalidades/fgi.md`) e também pode aparecer como garantia exigida no `LimiteBanco` (`GarantiaExigidaLimite.Tipo=Fgi`, v0.6.0). **Capital de Giro não tem flag FGI** (correção do design original `BalcaoCaixaDetail.TemFgi`).
- **Categorizar por "tipo de produto" interno do banco** (Proger, FCO, BNDES Auto, ConstrucCard, etc.). A modalidade é genérica; particularidades de produto bancário são opacas ao sistema.

---

## 2. Conceito de Negócio

### 2.1. Capital de Giro como produto universal

Capital de Giro é a linha de crédito BRL básica que qualquer banco comercial oferece a pessoa jurídica para cobrir necessidades de fluxo de caixa: pagamento de fornecedores, folha, impostos, capital de giro permanente. Não está atrelada a operação cambial (não é trade finance), não tem propósito específico declarado (diferente de NCE, que exige finalidade de exportação), e não tem registro BACEN externo (diferente de Lei 4131). É o "pão com manteiga" das linhas corporativas.

**Diferenças vs outras modalidades BRL:**

| Característica | Capital de Giro | NCE | FGI |
|---|---|---|---|
| Finalidade declarada | Livre | Crédito à exportação | Operação BNDES via banco |
| IRRF | Sujeito | Isento (incentivo) | Sujeito |
| IOF crédito | Sim | Sim | Sim |
| Tarifa periódica | Não | Não | Tarifa FGI anual sobre saldo |
| Documento específico | Contrato simples | Nota/Cédula NCE | Operação BNDES |
| Universalidade entre bancos | Total | Maior parte | Bancos repassadores BNDES |

### 2.2. Cronograma: misto regular + importado

Operacionalmente, o cronograma de Capital de Giro segue dois padrões:

- **Padrão regular** (~80% dos casos): Price/SAC/Bullet com periodicidade fixa, sem carência ou com carência homogênea. `GerarCronogramaCommand` calcula a partir das premissas da proposta.
- **Padrão fechado** (~20% dos casos): banco entrega cronograma pronto (carências irregulares, parcelas crescentes, valores antecipados negociados). Operador importa via `ImportarCronogramaCommand`.

A modalidade `CapitalDeGiro` é elegível para ambos os caminhos. **Mudança em relação ao design anterior:** `ImportarCronogramaCommand` hoje exige `ModalidadeContrato.BalcaoCaixa` (linha 38 do command). Esta SPEC manda renomear o gate para `CapitalDeGiro` e manter o comportamento.

### 2.3. CET estimado

O CET registrado na cotação é uma **estimativa** baseada nas premissas da proposta (TaxaAa, PrazoDias, IOF crédito, Periodicidade). Quando o cronograma é importado depois com prazos/datas reais ligeiramente distintos, o CET real pode divergir. `EconomiaNegociacao` é imutável; portanto, congela a estimativa. Métricas de divergência ficam fora do MVP.

---

## 3. Modelo de Dados

### 3.1. Agregado `Cotacao`

Sem novos campos. A Onda 0 (F0.1) já fez `PtaxUsadaUsdBrl` nullable. Para `Modalidade = CapitalDeGiro`:

| Campo                    | Valor para Capital de Giro                                              |
|--------------------------|--------------------------------------------------------------------------|
| `Modalidade`             | `ModalidadeContrato.CapitalDeGiro` (valor int=5, antes `BalcaoCaixa`)   |
| `PtaxUsadaUsdBrl`        | `null` — invariante da Onda 0 §3.2 rejeita PTAX em modalidades BRL puras |
| `DataPtaxReferencia`     | `null` (consistente)                                                     |
| `ValorAlvoBRL`           | Em BRL                                                                   |

### 3.2. Agregado `Proposta`

**Sem campos novos** (MD-5). Restrições validadas pelo `RegistrarPropostaCommand` quando `cotacao.Modalidade = CapitalDeGiro`:

| Campo               | Restrição                                                                |
|---------------------|--------------------------------------------------------------------------|
| `MoedaOriginal`     | Obrigatoriamente `Moeda.Brl`. Outro → 400.                              |
| `ExigeNdf`          | Obrigatoriamente `false`. NDF não se aplica.                            |
| `CustoNdfAaPercentual` | Deve ser `null`.                                                      |
| `IofPercentual`     | Aceito (IOF crédito doméstico — distinto de IOF câmbio).                |
| `EstruturaAmortizacao`, `PeriodicidadeJuros`, `PrazoDias` | Aceitos como premissas para CET. |

### 3.3. `CapitalDeGiroDetail` (rename de `BalcaoCaixaDetail`)

**Refactor de código necessário** (fora do escopo desta SPEC — sequenciado para a Onda 3b):

| Antes | Depois |
|---|---|
| `Sgcf.Domain.Contratos.BalcaoCaixaDetail` | `Sgcf.Domain.Contratos.CapitalDeGiroDetail` |
| `ModalidadeContrato.BalcaoCaixa = 5` | `ModalidadeContrato.CapitalDeGiro = 5` (int preservado) |
| Coluna física `balcao_caixa_detail` | **Mantida** como `balcao_caixa_detail` (compatibilidade de dados; mapeada via `ToTable("balcao_caixa_detail")`); evolução opcional renomeia em migration separada |
| `BalcaoCaixaDetailConfiguration` | `CapitalDeGiroDetailConfiguration` |
| `DbContext.BalcaoCaixaDetails` | `DbContext.CapitalDeGiroDetails` |

**Campos removidos** (decisão "Eliminar TipoProduto"):

| Campo | Por quê remover |
|---|---|
| `TipoProduto` (string, Proger/FCO/BndesAutomatico) | Particularidade interna do banco; sistema agnóstico |
| (TemFgi — ver §3.4) | FGI não é anexo a Capital de Giro nesta SPEC |

**Campos mantidos:**

| Campo | Tipo | Origem na conversão |
|---|---|---|
| `ContratoId` | `Guid` | Contrato recém-criado |
| `NumeroOperacao` | `string?` | Input do `ConverterEmContratoCommand` |
| `CreatedAt`, `UpdatedAt` | `Instant` | Clock |

Migration adicional opcional (Onda 3b): coluna `tipo_produto` é dropada da tabela `balcao_caixa_detail` se já existir (verificar snapshot).

### 3.4. FGI fora desta modalidade

Esta SPEC **remove a flag `TemFgi`** do design original. Motivação (correção de 2026-05-18):

- **FGI é programa BNDES** ofertado via bancos. Quando o operador toma um contrato BNDES com garantia FGI, a modalidade correta é `ModalidadeContrato.Fgi` (`fgi.md`), não Capital de Giro.
- **Capital de Giro com garantia FGI exigida pelo banco como condição da linha** continua representado via `GarantiaExigidaLimite.Tipo=Fgi` no cadastro do limite (v0.6.0). Isso já cobre o caso "o banco exige FGI como garantia desta linha de capital de giro".
- A flag `BalcaoCaixaDetail.TemFgi` era ambígua e duplicava o caminho via `GarantiaExigidaLimite`. Removida.

---

## 4. Fluxo Funcional

```
Operador inicia                  Banco propõe                Operador compara
cotação Capital de Giro    ─→    taxa BRL                ─→  e aceita
    │                            (sem NDF/PTAX)              │
    │                                                          │
    ▼                                                          ▼
POST /cotacoes                                          POST /converter-em-contrato
{modalidade=CapitalDeGiro,                              {numeroOperacao?, ...}
 valorAlvoBrl, prazoMaximoDias}                                │
    │                                                          ▼
    ▼                                                  ConversorCapitalDeGiro
Cotacao.Criar(...)                                     cria CapitalDeGiroDetail
PTAX=null (Onda 0 valida)                                      │
                                                              ▼
                                                       Contrato + Detail persistidos
                                                              │
                                                              ▼
                                                  [opcional] POST /contratos/{id}/cronograma/importar
                                                       ou GerarCronogramaCommand
                                                       (caminho regular)
```

### 4.1. Criar cotação

`POST /api/v1/cotacoes`

```json
{
  "codigoInterno": null,
  "modalidade": "CapitalDeGiro",
  "valorAlvoBrl": 1500000.00,
  "prazoMaximoDias": 180,
  "dataAbertura": "2026-06-01",
  "observacoes": "Capital de giro pré-junho"
}
```

Handler segue o caminho normal; Onda 0 garante que PTAX não é buscada (modalidade BRL pura).

### 4.2. Registrar proposta

`POST /api/v1/cotacoes/{id}/propostas`

```json
{
  "bancoId": "...",
  "moedaOriginal": "Brl",
  "valorOferecidoMoedaOriginal": 1500000.00,
  "taxaAaPercentual": 14.5,
  "iofPercentual": 0.38,
  "spreadAaPercentual": 1.2,
  "prazoDias": 180,
  "estruturaAmortizacao": "Bullet",
  "periodicidadeJuros": "Mensal",
  "exigeNdf": false
}
```

Validações específicas (handler):

- Rejeita `moedaOriginal != "Brl"` (400)
- Rejeita `exigeNdf = true` (400)
- Rejeita `custoNdfAaPercentual` não-nulo (400)

### 4.3. Comparar e aceitar

Sem peculiaridades — mesma máquina de estados de FINIMP. CET calculado via `CalculadoraCet.CalcularCetCapitalDeGiro` (renomeação de `CalcularCetBalcaoCaixa` da Onda 0).

### 4.4. Converter em contrato

`POST /api/v1/cotacoes/{id}/converter-em-contrato`

```json
{
  "numeroExternoContrato": "CG-2026-0042",
  "codigoInternoContrato": null,
  "dataContratacao": "2026-06-05",
  "dataVencimento": "2026-12-05",
  "taxaAa": 14.5,
  "observacoes": null,
  "numeroOperacao": "OP-7788-1234"
}
```

`ConversorCapitalDeGiro` cria `CapitalDeGiroDetail` com `NumeroOperacao`. Retorna `(CapitalDeGiroDetail, null)`. Não há detail secundário (mudança em relação ao design anterior — agora `BalcaoCaixaDetail+FgiDetail` deixou de ser caso de uso desta modalidade).

### 4.5. Cronograma

**Caminho regular** (default): `GerarCronogramaCommand` calcula a partir da proposta. Modalidade `CapitalDeGiro` é aceita (branch existente em `GerarCronogramaCommand.cs` linha 88 — antes condicional a `BalcaoCaixa` — segue valendo via rename).

**Caminho importado** (opcional): `POST /api/v1/contratos/{id}/cronograma/importar`. `ImportarCronogramaCommand.cs` linha 38 atualizada para aceitar `ModalidadeContrato.CapitalDeGiro` (rename direto).

---

## 5. API

### 5.1. `POST /api/v1/cotacoes`

Sem mudança no contrato. Aceita `modalidade = "CapitalDeGiro"`. PTAX rejeitada (Onda 0).

### 5.2. `POST /api/v1/cotacoes/{id}/propostas`

Sem mudança no contrato. Validações específicas no handler (§4.2).

### 5.3. `POST /api/v1/cotacoes/{id}/converter-em-contrato`

Estende inputs opcionais:

| Campo            | Tipo      | Obrigatório?       | Origem |
|------------------|-----------|--------------------|--------|
| `numeroOperacao` | `string?` | Opcional           | Documento bancário interno (opcional) |

Removidos do contrato anterior: `tipoProduto`, `temFgi`, `numeroOperacaoFgi`, `taxaFgiAaPercentual`, `percentualCoberto`. Esses campos não pertencem a Capital de Giro.

### 5.4. Respostas

- `201 Created` — Contrato criado, `ContratoDto` com `capitalDeGiroDetail` populado
- `400 Bad Request` — Validações de proposta/contrato
- `409 Conflict` — Cotação não tem proposta aceita, banco sem `LimiteBanco` para modalidade, transição inválida
- `404 Not Found` — Cotação inexistente

---

## 6. Conversor (`IConversorModalidade`)

Implementação `ConversorCapitalDeGiro` em `src/Sgcf.Application/Cotacoes/Conversores/`:

```csharp
public sealed class ConversorCapitalDeGiro : IConversorModalidade
{
    public ModalidadeContrato Modalidade => ModalidadeContrato.CapitalDeGiro;

    public Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx, CancellationToken ct)
    {
        CapitalDeGiroDetail detail = CapitalDeGiroDetail.Criar(
            ctx.ContratoCriado.Id,
            ctx.Command.NumeroOperacao,
            ctx.Clock);

        return Task.FromResult<(Entity, Entity?)>((detail, null));
    }
}
```

Registro DI em `DependencyInjection.cs` (substituindo o stub da Onda 0).

---

## 7. CET (estimativa)

`CalculadoraCet.CalcularCetCapitalDeGiro` (rename de `CalcularCetBalcaoCaixa`):

```
Inputs:
  - TaxaAaPercentual (decimal)
  - PrazoDias (int)
  - IofCreditoPercentual (decimal)
  - Periodicidade (Periodicidade)
  - SpreadAaPercentual (decimal, geralmente 0)

Saída:
  - CET a.a. em base 360

Fórmula (pure function):
  1. Calcular juros nominais sobre o principal, anualizados na base 360
  2. Adicionar IOF crédito sobre principal (alíquota informada)
  3. Periodicidade afeta o cronograma (gerado depois) mas não o CET anualizado
  4. SEM PTAX, SEM NDF, SEM IRRF (Capital de Giro é livre, sujeito a IR mas não retido na fonte sobre o tomador)
```

**Golden case:** Capital de Giro BRL, 180 dias, Bullet, taxa 14,5% a.a., IOF 0,38%, mensal de pagamento de juros.

---

## 8. Edge Cases

### EC-1 — Cotação com PTAX por engano

Operador envia `dataPtaxReferencia` ou força PTAX no payload. Onda 0 invariante rejeita: "PTAX não se aplica à modalidade CapitalDeGiro (operação em BRL)" (400).

### EC-2 — Proposta com `exigeNdf=true`

Handler `RegistrarPropostaCommand` retorna 400: "NDF não se aplica à modalidade Capital de Giro."

### EC-3 — Proposta com `moedaOriginal=USD`

Handler retorna 400: "Capital de Giro exige proposta em BRL."

### EC-4 — Tentativa de informar `tipoProduto` ou `temFgi` no payload

Como esses campos foram removidos do contrato, o ASP.NET Core os ignora (binding silencioso). Mas o validador pode emitir warning informativo (não bloquear): `"O campo 'tipoProduto' não é aceito para Capital de Giro a partir de v0.9.0; será ignorado."`

### EC-5 — Banco sem `LimiteBanco` para CapitalDeGiro

`AdicionarBancoNaCotacao` retorna 409: "Banco '...' não possui limite cadastrado para a modalidade CapitalDeGiro. Cadastre o limite operacional antes."

### EC-6 — Cronograma importado depois com taxa diferente

`EconomiaNegociacao` imutável (`SPEC.md` §12.3) — CET congela na estimativa.

### EC-7 — Capital de Giro com garantia FGI exigida

Modelado via `GarantiaExigidaLimite.Tipo=Fgi` no cadastro do `LimiteBanco` (v0.6.0). A modalidade continua `CapitalDeGiro`. Nenhum `FgiDetail` é criado para essa cotação — `FgiDetail` é só de contratos modalidade FGI.

### EC-8 — Estrutura de amortização não suportada

`Estrutura = "Carencia"` ou outras estruturas não cobertas no MVP: `GerarCronogramaCommand` rejeita (comportamento atual preservado). Operador usa caminho importado.

### EC-9 — Carência negociada

Caso comum em Capital de Giro (carência 3-6 meses). MVP:
- Caminho regular: se a estrutura suporta carência homogênea, `GerarCronogramaCommand` gera.
- Caminho importado: operador importa cronograma com a carência específica.

### EC-10 — Conversão sem `numeroOperacao`

Aceito. Campo é opcional. `CapitalDeGiroDetail.NumeroOperacao` fica null.

### EC-11 — Múltiplos contratos Capital de Giro com mesmo banco (mesmo limite)

Esperado e suportado. `LimiteBanco.RegistrarUso` decrementa a cada conversão; rejeita se exceder.

### EC-12 — `valorAlvoBrl` > `LimiteBanco.ValorDisponivelBrl`

Comportamento padrão: rejeição em `AdicionarBancoNaCotacao`.

### EC-13 — Conversão com IOF zero

Aceito (caso de operações isentas). CET reflete sem IOF.

### EC-14 — Periodicidade `Anual` com prazo < 1 ano

`GerarCronogramaCommand` rejeita (juros precisariam ser pagos antes do vencimento mas a periodicidade não comporta). Sem mudança do comportamento atual.

---

## 9. Testes

### 9.1. Domain (small)

Localização: `tests/Sgcf.Domain.Tests/Contratos/CapitalDeGiroDetailTests.cs` (rename de `BalcaoCaixaDetailTests`).

- `Criar_com_inputs_validos_retorna_entidade`
- `Criar_com_contratoId_vazio_lanca_excecao`
- `Atualizar_atualiza_UpdatedAt`

### 9.2. Application (medium)

`tests/Sgcf.Application.Tests/Cotacoes/`:

- `ConversorCapitalDeGiroTests.cs` — cria `CapitalDeGiroDetail`, retorna `(detail, null)`
- `CalculadoraCetCapitalDeGiroTests.cs` — CET base 360 sem PTAX/NDF/IRRF, com IOF crédito
- `RegistrarPropostaCommandHandlerTests` (estender): rejeitar `exigeNdf=true`, `moedaOriginal=USD`, `custoNdfAaPercentual not null` em modalidade CapitalDeGiro

### 9.3. Integration (large)

`tests/Sgcf.Api.IntegrationTests/Cotacoes/CapitalDeGiroFluxoTests.cs`:

- Fluxo E2E completo: cotação → 2 propostas → comparação → aceitação → conversão → gerar cronograma regular
- Variação: conversão → importar cronograma fechado (uso conjunto de `ConverterEmContratoCommand` + `ImportarCronogramaCommand`)
- Rejeições: PTAX em payload, proposta USD, NDF=true

### 9.4. Golden dataset

`tests/Sgcf.GoldenDataset/data/cotacoes/capital-de-giro-bullet-180d/`:

- `input.json`: Capital de Giro BRL 1.500.000, 180 dias Bullet, Mensal, IOF 0,38%, taxa 14,5% a.a.
- `expectedOutput.json`: CET calculado, comparativo, snapshot da economia

Regression: rodar 3 cenários FINIMP existentes garante zero impacto.

---

## 10. Boundaries — Always / Ask First / Never

### Always

- Validar `MoedaOriginal=BRL` no handler de `RegistrarPropostaCommand` quando modalidade é Capital de Giro.
- Manter `ConversorCapitalDeGiro.CriarDetailAsync` como pure mapping (sem I/O externo).
- Manter compatibilidade da tabela `balcao_caixa_detail` (mapeada via `ToTable`).
- Atualizar Bruno collection `13-Cotacoes/` com requests de exemplo Capital de Giro.

### Ask First

- Renomear a tabela física `balcao_caixa_detail` para `capital_de_giro_detail`. Requer migration extra; pode esperar momento operacional propício.
- Reintroduzir conceito de "tipo de produto" caso surja necessidade real (tabela de configuração externa, não enum).
- Reintroduzir `TemFgi` flag (decisão expressa do PO em 2026-05-18 removeu — só voltaria com justificativa nova).

### Never

- Atrelar `CapitalDeGiroDetail` a marca específica de banco (Caixa, Itaú, etc.). É produto universal.
- Criar `FgiDetail` no `ConversorCapitalDeGiro`. FGI tem conversor próprio.
- Reintroduzir branches por produto interno do banco (Proger, FCO) no domínio.

---

## 11. Documentação a atualizar

- `docs/specs/cotacoes/SPEC.md` — referência cruzada para `modalidades/capital-de-giro.md`; nota de rename em `[v0.9.0+]`
- `docs/api/cotacoes.md` — exemplos de payload Capital de Giro; substituir referências a "Balcão Caixa"
- `docs/api/schemas.md` — `CapitalDeGiroDetail` no schema; remover `TipoProduto`
- `docs/api/limites-banco.md` — exemplos com `modalidade=CapitalDeGiro`
- Bruno collection `13-Cotacoes/` — requests novos com payload BRL
- `CHANGELOG.md` v0.9.0 — bloco `BREAKING` para o rename (impacto: API recusa `"BalcaoCaixa"` em favor de `"CapitalDeGiro"` — confirmar política de aliasing antes do release)

---

## 12. Plano de Implementação

Referência: `tasks/cotacoes-modalidades/capital-de-giro/plan.md` e `todo.md`.

Ordem sugerida (resumo):

1. **Fase R (rename de código — pré-requisito):** rename `BalcaoCaixa*` → `CapitalDeGiro*` em todos os arquivos C# e testes, mantendo `int=5` no enum e nome da tabela.
2. **Fase 1 (Domínio):** remover campo `TipoProduto` (e `TemFgi` se ainda existir) de `CapitalDeGiroDetail`; migration adicional opcional.
3. **Fase 2 (Application + API):** implementar `ConversorCapitalDeGiro` (substitui stub Onda 0); implementar `CalculadoraCet.CalcularCetCapitalDeGiro`; validações em `RegistrarPropostaCommand`.
4. **Fase 3 (Testes):** Domain + Application + Integration + golden.
5. **Fase 4 (Documentação):** docs/api/, SPEC, CHANGELOG, Bruno.

---

## 13. Relação com SPEC FGI

| Caso | Modalidade na Cotação | Detail criado | Responsabilidade da SPEC |
|---|---|---|---|
| Capital de Giro puro | `CapitalDeGiro` | `CapitalDeGiroDetail` | **Esta SPEC** |
| Capital de Giro com FGI **exigida pelo banco como garantia da linha** | `CapitalDeGiro` | `CapitalDeGiroDetail` (sem `FgiDetail`); FGI aparece em `GarantiaExigidaLimite` do limite do banco | **Esta SPEC** |
| Contrato com produto BNDES via banco (linha FGI direta) | `Fgi` | `FgiDetail` | `fgi.md` |

A separação é clara: **Capital de Giro nunca cria `FgiDetail`.** Quando o operador toma uma operação BNDES-FGI, a modalidade correta é `Fgi`, com seu próprio fluxo.
