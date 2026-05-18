# SPEC — Modalidade NCE no Módulo de Cotações

**Versão alvo:** v0.8.0
**Status:** Pendente de implementação
**Pré-requisito:** Onda 0 (foundation) — F0.1 (PTAX nullable) é essencial; F0.2 (`CalcularCetNce` stub); F0.3 (`IConversorModalidade` registrado)
**Plano operacional:** `tasks/cotacoes-modalidades/nce/plan.md`
**SPEC base:** `docs/specs/cotacoes/SPEC.md`
**Decisões travadas:** MD-3 (Strategy `IConversorModalidade`), MD-5 (campos de modalidade no command, não no agregado), Onda 0 (PTAX nullable e `CalcularCetNce`)

---

## 1. Objetivo

Habilitar o módulo de Cotações a registrar, comparar, aceitar e converter em contrato propostas da modalidade **NCE (Nota de Crédito à Exportação)** — operação de crédito **doméstica em BRL** lastreada em recebíveis de exportação futura. A entrega reutiliza a estrutura genérica de `Cotacao`/`Proposta` introduzida no MVP FINIMP, sem mexer no agregado `Proposta`, e conecta-se a:

- **Entidade já existente:** `NceDetail` (`src/Sgcf.Domain/Contratos/NceDetail.cs`)
- **Fluxo já existente:** criação direta de Contrato NCE via `CreateContratoCommand` (`src/Sgcf.Application/Contratos/Commands/CreateContratoCommand.cs:137-142, 310-324`)
- **Validações já existentes em Contratos:** rejeição de IRRF/IOF câmbio para NCE em `GerarCronogramaCommand` (`src/Sgcf.Application/Contratos/Commands/GerarCronogramaCommand.cs:75-86`)

### 1.1. Não-objetivos

- Alterar fórmula tributária da operação NCE (isenção IRRF/IOF câmbio já travada no domínio de Contratos).
- Introduzir novos campos no agregado `Proposta` (MD-5 trava esta decisão).
- Mudar enum `Periodicidade` ou seu suporte no motor `Sgcf.Domain.Cronograma`.
- Suportar NCE com moeda diferente de BRL ou com NDF (rejeições explícitas — §8).
- Modificar a interface pública do MVP FINIMP (PTAX continua aceito; só deixa de ser exigido para BRL).
- Implementar parser de PDF ou ingestão automática da proposta NCE.

---

## 2. Conceito de Negócio

### 2.1. NCE/CCE como instrumento de crédito à exportação

**NCE — Nota de Crédito à Exportação** é título de crédito doméstico, emitido por exportador brasileiro ou empresa equiparada, lastreado em recebíveis futuros de exportação. **CCE — Cédula de Crédito à Exportação** tem natureza equivalente. O incentivo regulatório (Lei 6.313/1975, Decreto-Lei 413/1969) torna a operação **isenta de IRRF** e **não sujeita a IOF câmbio** (não há operação de câmbio: o desembolso e o pagamento ocorrem em BRL).

O banco financia o exportador em BRL contra obrigação de recebíveis em moeda estrangeira que serão internalizados em momento futuro. Para o tomador, é uma linha de capital de giro a custo geralmente inferior ao Working Capital tradicional por causa da isenção fiscal.

### 2.2. Diferenças vs FINIMP

| Característica | FINIMP | NCE |
| --- | --- | --- |
| Moeda do principal | USD/EUR/CNY/JPY | **BRL sempre** |
| PTAX D-1 obrigatória | Sim | **Não** (Onda 0 — F0.1) |
| NDF | Permitido (banco pode exigir) | **Nunca aplicável** |
| IRRF | Aplicável sobre juros | **Isento** (incentivo à exportação) |
| IOF câmbio | Aplicável na liquidação | **Não aplicável** (sem câmbio) |
| IOF crédito | Não aplicável (operação cambial) | **Aplicável** (operação interna de crédito) |
| Periodicidade de juros | Bullet (MVP) | **Bullet, Mensal, Bimestral, Trimestral, Semestral, Anual** |
| Garantias típicas | CDB cativo, aval | Aval dos sócios + duplicatas de exportação + cessão fiduciária |
| `*Detail` na conversão | `FinimpDetail` | `NceDetail` |
| Validação de banco | Banco aceita FINIMP | Banco aceita NCE (limite por modalidade) |

### 2.3. Isenção IRRF e impacto no CET

A isenção de IRRF é **estrutural**: não existe alíquota a recolher no fluxo de juros NCE. Como `CalculadoraCet` no MVP FINIMP **não modela IRRF** explicitamente (o cálculo retorna CET bruto pré-imposto), nenhuma exceção precisa ser codificada no cálculo. O que importa para a SPEC NCE é **garantir documentalmente** (e em property test — §9.3) que qualquer refator futuro de `CalculadoraCet` que venha a introduzir IRRF para outras modalidades **não pode** vazar para o caminho `CalcularCetNce`.

O **IOF crédito** (operação interna), ao contrário, é parte explícita do custo do tomador e **entra no CET** via `Proposta.IofPercentual` aplicado sobre o principal — exatamente a mesma semântica de `IofPercentual` em FINIMP, embora a base regulatória seja outra (IOF crédito vs IOF câmbio). Documentar isso em §8 é crítico para evitar confusão do operador.

---

## 3. Modelo de Dados

### 3.1. `Cotacao` — PtaxUsadaUsdBrl null para NCE

A entrega de Onda 0 (F0.1) torna `Cotacao.PtaxUsadaUsdBrl` `decimal?` e introduz invariante no factory `Cotacao.Criar`:

```csharp
// Em Sgcf.Domain.Cotacoes.Cotacao
if (!ExigeMoedaEstrangeira(modalidade) && ptaxUsadaUsdBrl is not null)
    throw new ArgumentException(
        $"PTAX não se aplica à modalidade {modalidade} (operação em BRL).",
        nameof(ptaxUsadaUsdBrl));

internal static bool ExigeMoedaEstrangeira(ModalidadeContrato m) =>
    m == ModalidadeContrato.Finimp ||
    m == ModalidadeContrato.Refinimp ||
    m == ModalidadeContrato.Lei4131;
```

Para NCE, a cotação é criada com `PtaxUsadaUsdBrl = null` e **sem** busca a `ICotacaoFxRepository`. O campo `DataPtaxReferencia` também é `null` para NCE (alinhado a `PtaxUsadaUsdBrl`).

**Snapshot em `EconomiaNegociacao`:** o JSON serializado de uma cotação NCE conterá `"PtaxUsada": null` (ou ausência do campo, dependendo da política de serialização — Onda 0 §3.6 obriga round-trip estável).

### 3.2. `Proposta` — sem campos novos

**Decisão MD-5 (travada):** nenhum campo novo é adicionado ao agregado `Proposta`. A proposta NCE reutiliza os campos existentes com restrições:

| Campo | Regra para NCE |
| --- | --- |
| `MoedaOriginal` | **DEVE ser `Moeda.Brl`** — handler rejeita outras |
| `ExigeNdf` | **DEVE ser `false`** — handler rejeita `true` |
| `CustoNdfAaPercentual` | DEVE ser `null` (não há NDF) |
| `IofPercentual` | Representa IOF crédito (alíquota da operação interna); pode ser `0` ou positivo |
| `PeriodicidadeJuros` | Aceita `Bullet`, `Mensal`, `Bimestral`, `Trimestral`, `Semestral`, `Anual` |
| `EstruturaAmortizacao` | Bullet ou Price (Sac fora do escopo MVP NCE) |
| `GarantiaExigida` | String descritiva (ex.: "Aval dos sócios + duplicatas de exportação") |
| `GarantiaEhCdbCativo` | Geralmente `false` para NCE; aceito `true` se o banco realmente exigir CDB |
| `SpreadAaPercentual` | Aceito (ex.: CDI + spread quando indexada) |

### 3.3. `NceDetail` — entidade já existente

Definida em `src/Sgcf.Domain/Contratos/NceDetail.cs`:

```csharp
public sealed class NceDetail : Entity
{
    public Guid ContratoId { get; private set; }
    public string? NceNumero { get; private set; }
    public LocalDate? DataEmissao { get; private set; }
    public string? BancoMandatario { get; private set; }
    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    public static NceDetail Criar(
        Guid contratoId,
        string? nceNumero,
        LocalDate? dataEmissao,
        string? bancoMandatario,
        IClock clock) { ... }
}
```

Todos os três campos de negócio são **opcionais** — alinhado ao padrão atual de criação direta via `CreateContratoCommand` (linha 313: `reqNce ?? new NceDetailRequest(null, null, null)`). A conversão Cotação → Contrato segue o mesmo padrão: campos opcionais no command de conversão.

---

## 4. Fluxo Funcional

### 4.1. Criar cotação NCE

1. Operador chama `POST /api/v1/cotacoes` com `modalidade: "Nce"`.
2. Handler `CriarCotacaoCommandHandler`:
   - Verifica `Cotacao.ExigeMoedaEstrangeira(Nce) == false`.
   - **Não** busca PTAX no `ICotacaoFxRepository`.
   - Cria `Cotacao` com `PtaxUsadaUsdBrl = null` e `DataPtaxReferencia = null`.
3. Cotação criada em status `Rascunho`.

### 4.2. Registrar proposta NCE

1. Operador chama `POST /api/v1/cotacoes/{id}/propostas` informando `moedaOriginal: "Brl"`, `exigeNdf: false`, `periodicidadeJuros` desejada.
2. Handler `RegistrarPropostaCommandHandler`:
   - Carrega `cotacao`.
   - Se `cotacao.Modalidade == Nce`:
     - Rejeita `MoedaOriginal != Brl` com `ArgumentException`.
     - Rejeita `ExigeNdf == true` com `ArgumentException`.
   - Calcula CET via `CalculadoraCet.CalcularCet(...)` — a fachada dispatcheia para `CalcularCetNce`.
3. Proposta criada em status `Recebida` com `CetCalculadoAaPercentual` populado.

### 4.3. Comparar propostas

`GET /api/v1/cotacoes/{id}/comparativo` retorna ranking idêntico ao MVP FINIMP — três métricas (taxa nominal, CET, custo total equivalente em BRL). Para NCE, a métrica "custo total equivalente" é trivial: o principal já está em BRL, dispensando conversão PTAX.

O comparativo NCE expõe explicitamente o campo `iofCreditoBrl` (renomeação semântica do `IofPercentual` no contexto NCE) para evitar confusão com IOF câmbio.

### 4.4. Aceitar proposta

`POST /api/v1/cotacoes/{id}/propostas/{propostaId}/aceitar` — sem mudanças vs. FINIMP. Grava `AceitaPor` (sub do JWT) e move cotação para `Aceita`.

### 4.5. Converter em contrato

1. Operador chama `POST /api/v1/cotacoes/{id}/converter-em-contrato` com payload opcional `nceNumero`, `dataEmissao`, `bancoMandatario`.
2. Handler `ConverterEmContratoCommandHandler`:
   - Cria `Contrato` com `modalidade = Nce`, `moeda = Brl`, `valorPrincipal = propostaAceita.ValorOferecidoMoedaOriginal`.
   - Resolve `IConversorModalidade` via DI mapeada por `Nce`.
   - Invoca `ConversorNce.CriarDetailAsync(ctx, ct)` que retorna `(NceDetail, null)`.
   - Persiste via `contratoRepo.AddDetail(nceDetail)` (método polimórfico — Onda 0 F0.3).
   - Atualiza `LimiteBanco` na modalidade NCE.
   - Cria `EconomiaNegociacao` com snapshot imutável (sem PTAX).
3. Cotação move para `Convertida`.

---

## 5. API

### 5.1. `POST /api/v1/cotacoes` — criar cotação NCE

**Request:**

```json
{
  "modalidade": "Nce",
  "valorAlvoBRL": 5000000.00,
  "prazoMaximoDias": 360,
  "dataAbertura": "2026-06-01",
  "observacoes": "Linha NCE para capital de giro contra recebíveis de exportação Q3/Q4."
}
```

**Notas:**

- Campos `dataPtaxReferencia` e `ptaxUsadaUsdBrl` **não** são aceitos no payload (não há campo equivalente para NCE no DTO; se enviados como `null`, são ignorados pelo handler — qualquer valor não-nulo retorna 400 via validator de domínio).

**Response 201:**

```json
{
  "id": "01939a8c-...-uuid7",
  "codigoInterno": "COT-2026-00023",
  "modalidade": "Nce",
  "valorAlvoBRL": 5000000.00,
  "prazoMaximoDias": 360,
  "dataAbertura": "2026-06-01",
  "dataPtaxReferencia": null,
  "ptaxUsadaUsdBrl": null,
  "status": "Rascunho"
}
```

### 5.2. `POST /api/v1/cotacoes/{id}/propostas` — registrar proposta NCE

**Request:**

```json
{
  "bancoId": "01939a8c-...-uuid7",
  "moedaOriginal": "Brl",
  "valorOferecidoMoedaOriginal": 5000000.00,
  "taxaAaPercentual": 12.50,
  "iofPercentual": 0.38,
  "spreadAaPercentual": 0.00,
  "prazoDias": 360,
  "estruturaAmortizacao": "Bullet",
  "periodicidadeJuros": "Trimestral",
  "exigeNdf": false,
  "custoNdfAaPercentual": null,
  "garantiaExigida": "Aval dos sócios + duplicatas de exportação",
  "valorGarantiaExigidaBRL": 0.00,
  "garantiaEhCdbCativo": false,
  "rendimentoCdbAaPercentual": null
}
```

**Validações específicas NCE (handler):**

- `moedaOriginal == "Brl"` — caso contrário 400 com mensagem: `"Proposta NCE deve ser em BRL — modalidade não suporta conversão cambial."`
- `exigeNdf == false` — caso contrário 400 com mensagem: `"Proposta NCE não aceita NDF — operação em BRL sem exposição cambial."`

### 5.3. `POST /api/v1/cotacoes/{id}/converter-em-contrato` — converter NCE

**Request (campos NCE opcionais):**

```json
{
  "numeroExterno": "NCE-BB-2026-00045",
  "dataContratacao": "2026-06-05",
  "nceNumero": "NCE-2026-00045",
  "dataEmissao": "2026-06-05",
  "bancoMandatario": "Banco do Brasil S.A."
}
```

**Notas:**

- `nceNumero`, `dataEmissao`, `bancoMandatario` são opcionais. Se ausentes, `NceDetail` é persistido com os três campos `null` (mesmo comportamento atual de `CreateContratoCommand` — linha 313).
- Campos FINIMP do command (`rofNumero`, `exportadorNome`, etc.) permanecem aceitos mas são **ignorados** quando `cotacao.Modalidade == Nce`. Isto preserva compatibilidade do payload existente.

**Response 200:**

```json
{
  "contratoId": "01939a8c-...-uuid7",
  "modalidade": "Nce",
  "moeda": "Brl",
  "valorPrincipal": 5000000.00,
  "nceDetail": {
    "nceNumero": "NCE-2026-00045",
    "dataEmissao": "2026-06-05",
    "bancoMandatario": "Banco do Brasil S.A."
  }
}
```

### 5.4. Comparativo: CET e detalhe IOF crédito

`GET /api/v1/cotacoes/{id}/comparativo` retorna por proposta:

```json
{
  "propostaId": "01939a8c-...",
  "bancoApelido": "BB",
  "taxaNominalAaPercentual": 12.50,
  "cetAaPercentual": 13.18,
  "custoTotalEquivalenteBRL": 5658750.00,
  "iofCreditoBrl": 19000.00,
  "periodicidadeJuros": "Trimestral"
}
```

O campo `iofCreditoBrl` é exposto **somente** para propostas NCE (e demais BRL puros — FGI, Capital de Giro); para FINIMP, o equivalente é o IOF câmbio e o nome do campo permanece `iofBrl` na resposta — diferença lexical intencional para a UX do operador.

---

## 6. Conversor (IConversorModalidade)

A Onda 0 F0.3 introduz a interface `IConversorModalidade` registrada no DI. A modalidade NCE entrega a implementação concreta:

```csharp
// src/Sgcf.Application/Cotacoes/Conversores/ConversorNce.cs
public sealed class ConversorNce : IConversorModalidade
{
    public ModalidadeContrato Modalidade => ModalidadeContrato.Nce;

    public Task<(Entity Principal, Entity? Secundario)> CriarDetailAsync(
        ConverterEmContratoContext ctx, CancellationToken ct)
    {
        LocalDate? dataEmissao = ctx.Command.DataEmissao.HasValue
            ? new LocalDate(
                ctx.Command.DataEmissao.Value.Year,
                ctx.Command.DataEmissao.Value.Month,
                ctx.Command.DataEmissao.Value.Day)
            : (LocalDate?)null;

        NceDetail detail = NceDetail.Criar(
            ctx.ContratoCriado.Id,
            ctx.Command.NceNumero,
            dataEmissao,
            ctx.Command.BancoMandatario,
            ctx.Clock);

        return Task.FromResult<(Entity, Entity?)>((detail, null));
    }
}
```

**Retorna `(NceDetail, null)`** — NCE é modalidade simples (não tem detail secundário). Nota: após a correção de 2026-05-18, **nenhum conversor do MVP retorna `Secundario != null`** — a interface continua suportando o caso por contrato, mas todas as modalidades atuais retornam `null` na segunda posição. Registrado em DI:

```csharp
services.AddScoped<IConversorModalidade, ConversorNce>();
```

---

## 7. CET

### 7.1. Método `CalcularCetNce` (Onda 0 stub → implementação NCE)

A Onda 0 F0.2 deixou o método `CalcularCetNce` como `NotImplementedException`. Esta SPEC entrega a implementação:

```csharp
internal static decimal CalcularCetNce(
    Proposta proposta,
    LocalDate dataDesembolso,
    decimal? taxaAaPercentualOverride = null)
{
    if (proposta.MoedaOriginal != Moeda.Brl)
        throw new ArgumentException(
            $"CalcularCetNce exige MoedaOriginal=Brl (recebido: {proposta.MoedaOriginal}).",
            nameof(proposta));

    if (proposta.ExigeNdf)
        throw new ArgumentException(
            "CalcularCetNce não aceita ExigeNdf=true (NCE é operação em BRL sem hedge cambial).",
            nameof(proposta));

    decimal taxaAa = (taxaAaPercentualOverride ?? proposta.TaxaAaPercentual) + proposta.SpreadAaPercentual;
    decimal iofCreditoBrl = proposta.ValorOferecidoMoedaOriginal.Valor * (proposta.IofPercentual / 100m);

    // 1) Projetar cronograma hipotético com periodicidade da proposta (motor Cronograma existente).
    // 2) Adicionar IOF crédito como saída no t=0 (sobre principal).
    // 3) Calcular TIR do fluxo em BRL → anualização base 360 (decisão Onda 0 §4.4).
    // 4) Aplicar rendimento CDB cativo (se houver) como entrada na carteira do tomador.
    // SEM IRRF — isenção estrutural NCE.
    // SEM IOF câmbio — operação interna.
    // SEM custo NDF — NCE não tem hedge.

    return TirAnualizada360(fluxoCaixaBrl);
}
```

**Inputs explicitamente NÃO recebidos:**

- PTAX — método não tem parâmetro `ptaxUsdBrl` (assinatura Onda 0 F0.2).
- NDF — proposta tem `ExigeNdf=false` (invariante).
- IRRF — não modelado em `CalculadoraCet` no MVP; documentado em SPEC.

### 7.2. Periodicidade de juros e CET

A periodicidade afeta o **cronograma do contrato** (gerado pós-conversão por `GerarCronogramaCommand`), mas a **anualização do CET** segue base 360 — pré-acordada na Onda 0 §4.4. O CET é a TIR anualizada do fluxo de caixa, e propostas com mesma taxa nominal e mesma estrutura Bullet apresentam CETs próximos independentemente da periodicidade dos juros (a diferença vem do timing de saída de caixa do tomador).

### 7.3. Fórmula resumida

Para NCE Bullet com juros periódicos (Mensal/Trimestral/Semestral/Anual):

```
Fluxo t=0: +ValorOferecido - IofCreditoBrl   (entrada líquida ao tomador)
Fluxo t=N_periodo_k: -(ValorOferecido × taxaPeriodo)   para cada período de juros
Fluxo t=Prazo: -ValorOferecido                (devolução do principal no vencimento Bullet)

CET = TIR(fluxo, base=360)
```

Para Price com NCE: o motor `Sgcf.Domain.Cronograma` gera parcelas iguais; `IofPercentual` continua aplicado integralmente em t=0.

### 7.4. Golden case (referência)

**Cenário NCE BRL trimestral:**

```
Principal:              R$ 5.000.000,00
Taxa a.a.:              12,00%
Spread a.a.:            0,00%
IOF crédito:            0,38%
Prazo:                  360 dias
Estrutura:              Bullet
Periodicidade juros:    Trimestral
PTAX:                   (não aplicável)
```

CET esperado (validação humana obrigatória — Task 4.1 do plano): faixa indicativa ≈ 12,50%-13,20% a.a. dependendo de convenção de dias úteis e timing de IOF. **Valor exato a ser homologado** com planilha PO.

---

## 8. Edge Cases (OBRIGATÓRIO)

| # | Cenário | Comportamento esperado | Onde valida |
| --- | --- | --- | --- |
| EC-1 | Cotação NCE criada com PTAX informada por engano | 400 — `ArgumentException` da factory `Cotacao.Criar` com mensagem: `"PTAX não se aplica à modalidade Nce (operação em BRL)."` | Domain (Onda 0 F0.1) |
| EC-2 | Proposta NCE com `ExigeNdf = true` | 400 — `ArgumentException` do handler: `"Proposta NCE não aceita NDF — operação em BRL sem exposição cambial."` | Application handler |
| EC-3 | Proposta NCE com `MoedaOriginal = "Usd"` | 400 — `ArgumentException` do handler: `"Proposta NCE deve ser em BRL — modalidade não suporta conversão cambial."` | Application handler |
| EC-4 | Proposta NCE Bullet com prazo > 365 dias e `PeriodicidadeJuros = Bullet` | 200 com aviso informativo em `alertas`: `"NCE Bullet com prazo > 1 ano: juros acumulam ao vencimento; verifique se foi intenção do banco."` | Application handler (alerta, não bloqueio) |
| EC-5 | Proposta NCE com `PeriodicidadeJuros = Anual` e `PrazoDias < 360` | 400 — `ArgumentException`: `"Periodicidade Anual exige prazo >= 360 dias (não há período de juros completo a aplicar)."` | Application handler |
| EC-6 | Conversão de cotação NCE para banco sem `LimiteBanco` cadastrado para modalidade NCE | 409 — `InvalidOperationException`: `"Banco {apelido} não possui limite operacional cadastrado para a modalidade Nce."` | Handler de conversão (regra existente do MVP) |
| EC-7 | Conversão de cotação NCE com `LimiteBanco` insuficiente | 409 — mensagem padrão do MVP de limite excedido | Handler de conversão (regra existente) |
| EC-8 | Operador confunde IOF crédito com IOF câmbio | Resposta da API expõe `iofCreditoBrl` (não `iofCambioBrl`) para NCE; documentação `docs/api/cotacoes.md` esclarece a diferença | API + Doc |
| EC-9 | Conversão de cotação NCE com `nceNumero` ausente | 200 — `NceDetail` persistido com `NceNumero = null` (alinha com `CreateContratoCommand.cs:313`) | Conversor |
| EC-10 | Refresh de mercado em cotação NCE (`POST /refresh-mercado`) | 200 com no-op: handler detecta `Modalidade == Nce`, retorna sem mudar estado e emite alerta `"Refresh de mercado não se aplica a NCE (operação em BRL)."` | Handler `RefreshCotacaoMercadoCommand` |
| EC-11 | Proposta NCE com `GarantiaEhCdbCativo = true` e `RendimentoCdbAaPercentual` ausente | 400 — herda regra existente do MVP (`Proposta` invariante §3.3 regra 5 da SPEC base) | Domain |
| EC-12 | Tentativa de gerar cronograma para Contrato NCE convertido com `aliqIrrfPct != 0` | 400 — `ArgumentException` já existente em `GerarCronogramaCommand.cs:79` | Contratos (regra externa, fora do escopo desta SPEC) |
| EC-13 | Snapshot histórico `EconomiaNegociacao` de cotação FINIMP existente (com PTAX) deserializa após migration | Round-trip JSON estável (Onda 0 §3.6) | Migration + serializer |
| EC-14 | Property test: PTAX qualquer (1m, 5m, 1000m) passada via `CalcularCet` fachada para proposta NCE não muda CET | CET invariante para NCE em relação à PTAX | Property test |
| EC-15 | Property test: introduzir IRRF futuro em outras modalidades não pode vazar para `CalcularCetNce` | CET de NCE invariante a parâmetro IRRF hipotético | Property test |

---

## 9. Testes

### 9.1. Domain (`tests/Sgcf.Domain.Tests/`)

- `Cotacoes/CotacaoTests.cs` (estende):
  - `Criar_NCE_sem_PTAX_sucesso`
  - `Criar_NCE_com_PTAX_lanca_excecao`
  - `Criar_NCE_com_DataPtaxReferencia_lanca_excecao`
- `Cotacoes/CalculadoraCetNceTests.cs` (novo):
  - `CalcularCetNce_principal_taxa_spread_iof_credito_resultado_esperado`
  - `CalcularCetNce_periodicidade_trimestral_vs_bullet_diferenca_dentro_de_tolerancia`
  - `CalcularCetNce_rejeita_MoedaOriginal_USD`
  - `CalcularCetNce_rejeita_ExigeNdf_true`
  - `CalcularCetNce_zero_IOF_iguala_taxa_nominal_para_Bullet_360d`

### 9.2. Application (`tests/Sgcf.Application.Tests/`)

- `Cotacoes/CriarCotacaoCommandHandlerTests.cs` (estende):
  - `Handle_NCE_nao_busca_PTAX_e_PtaxUsada_fica_null`
  - `Handle_NCE_DataPtaxReferencia_fica_null`
- `Cotacoes/RegistrarPropostaCommandHandlerTests.cs` (estende):
  - `Handle_NCE_com_moeda_USD_lanca_ArgumentException`
  - `Handle_NCE_com_ExigeNdf_true_lanca_ArgumentException`
  - `Handle_NCE_BRL_sucesso`
  - `Handle_NCE_Anual_prazo_inferior_360_lanca_excecao` (EC-5)
- `Cotacoes/Conversores/ConversorNceTests.cs` (novo):
  - `CriarDetailAsync_com_campos_completos_retorna_NceDetail_populado`
  - `CriarDetailAsync_com_campos_nulos_retorna_NceDetail_com_todos_null`
  - `Secundario_eh_sempre_null`
- `Cotacoes/ConverterEmContratoCommandHandlerTests.cs` (estende):
  - `Handle_NCE_dispatcheia_para_ConversorNce`
  - `Handle_NCE_persiste_NceDetail_via_AddDetail`

### 9.3. Integration (`tests/Sgcf.Api.IntegrationTests/`)

- `Cotacoes/CotacaoNceFluxoTests.cs` (novo):
  - `Fluxo_E2E_criar_NCE_registrar_proposta_aceitar_converter_sucesso`
  - `Criar_NCE_com_PTAX_no_payload_retorna_400`
  - `Registrar_proposta_NCE_em_USD_retorna_400`
  - `Registrar_proposta_NCE_com_NDF_retorna_400`
  - `Converter_NCE_em_contrato_sem_campos_NceDetail_persiste_nulls`
  - `Converter_NCE_em_contrato_com_LimiteBanco_NCE_inexistente_retorna_409`
  - `Refresh_mercado_em_cotacao_NCE_eh_noop_com_alerta` (EC-10)

### 9.4. Golden Dataset (`tests/Sgcf.GoldenDataset/`)

- `data/cotacoes/nce-brl-trimestral.json` (novo):
  - Input: NCE BRL R$ 5M, 360d, 12% a.a., 0,38% IOF crédito, juros trimestrais, Bullet
  - Expected output: CET validado por planilha PO (Task 4.1 do plano)
- Tolerância: ≤ 0,01% absoluto sobre CET (alinhado a cenários FINIMP existentes)

### 9.5. Property tests (`tests/Sgcf.Domain.Tests/Cotacoes/CalculadoraCetPropertyTests.cs`)

- `CET_NCE_invariante_a_PTAX_hipotetica` (EC-14)
- `CET_NCE_estritamente_igual_a_FINIMP_BRL_com_PTAX_1` — quando ambas as modalidades aceitam o mesmo input BRL
- `CET_NCE_invariante_a_parametro_IRRF_hipotetico` (EC-15) — guard rail contra refactors futuros

---

## 10. Boundaries — Always / Ask First / Never

### 10.1. Always

- Validar `MoedaOriginal == Brl` e `ExigeNdf == false` antes de criar `Proposta` para cotação NCE.
- Reutilizar `NceDetail.Criar` existente (não duplicar entidade).
- Reutilizar `CalculadoraCet.CalcularCetNce` (Onda 0 F0.2) — não criar caminho paralelo.
- Reutilizar `IConversorModalidade` (Onda 0 F0.3) — registrar `ConversorNce` em DI.
- Documentar em resposta de API a diferença entre `iofCreditoBrl` (NCE) e `iofCambioBrl` (FINIMP).
- Anualizar CET em base 360 (Onda 0 §4.4).

### 10.2. Ask First

- Mudar base de anualização de 360 para 252 (úteis BACEN). Default mantido em 360; refinamento pós-MVP NCE.
- Estender `Proposta.IofPercentual` para `IofAdicionalAd` (alíquota diária IOF crédito 0,0041% a.d.). Default: alíquota única.
- Adicionar IRRF ao `CalculadoraCet` para qualquer modalidade — exige revisão da garantia de isenção NCE (EC-15).
- Suportar Sac para NCE — fora do escopo MVP NCE; abrir discussão se demanda surgir.

### 10.3. Never

- Aceitar proposta NCE em moeda diferente de BRL.
- Aceitar proposta NCE com `ExigeNdf = true`.
- Buscar PTAX no `ICotacaoFxRepository` para cotação NCE.
- Persistir `PtaxUsadaUsdBrl != null` para cotação NCE.
- Aplicar IRRF ou IOF câmbio em qualquer cálculo NCE.
- Persistir `NceDetail` sem associação a `ContratoId` válido.

---

## 11. Documentação a Atualizar

| Documento | Mudança |
| --- | --- |
| `docs/specs/cotacoes/SPEC.md` | §11.2 (boundaries): mover NCE de "fora do MVP" para "incluído na v0.8.0". |
| `docs/api/cotacoes.md` | Adicionar exemplos de payload NCE para `POST /cotacoes`, `POST /propostas`, `POST /converter-em-contrato`. Esclarecer diferença `iofCreditoBrl` vs `iofCambioBrl`. |
| `docs/changelog/CHANGELOG.md` | Bloco `## [0.8.0] — <data>` com `ADDITIVE — Cotações — Modalidade NCE`. |
| `docs/agentes-ia/IDEIAS-AGENTES.md` | (opcional) registrar ideias específicas de extração de proposta NCE de PDF. |
| Bruno collection `docs/api/collections/sgcf-api/06-Cotacoes/` | 4 requests novos: criar/proposta/comparativo/converter NCE. |
| Bruno collection `docs/api/collections/sgcf-api/11-LimitesBanco/` | 1 request: criar limite NCE. |

---

## 12. Plano de Implementação

Referência principal: `tasks/cotacoes-modalidades/nce/plan.md`. Resumo das 5 fases:

| Fase | Conteúdo | Pré-requisito |
| --- | --- | --- |
| **Fase 1 — Domínio (já entregue na Onda 0)** | F0.1 PtaxNullable, F0.2 CalcularCetNce stub, F0.3 ConversorNce stub | Onda 0 mergeada |
| **Fase 2 — Application** | Implementar `CalcularCetNce` (deixa de ser stub); validações de moeda/NDF em `RegistrarPropostaCommand`; estender `ConverterEmContratoCommand` com campos NCE; implementar `ConversorNce` (deixa de ser stub) | Fase 1 |
| **Fase 3 — API + Bruno** | Atualizar `CotacoesController` para aceitar campos NCE no converter; 5 requests Bruno | Fase 2 |
| **Fase 4 — Tests** | Golden NCE trimestral (com validação humana do esperado); property tests EC-14/EC-15 | Fase 2 |
| **Fase 5 — Documentação** | SPEC §11.2, API doc, CHANGELOG v0.8.0 | Fase 3 |

**Caminho crítico:** implementar `CalcularCetNce` (Fase 2) → validar contra golden manual (Fase 4) → liberar para Fase 3. Documentação (Fase 5) paraleliza com Fase 3.

**Critérios de aceite globais:**

- [ ] Toda suite `dotnet test` verde, incluindo golden NCE.
- [ ] Build limpo (0 warnings, 0 errors).
- [ ] Fluxo Bruno NCE ponta a ponta validado por operador humano.
- [ ] Regressão FINIMP intacta (zero teste FINIMP existente quebra).
- [ ] CHANGELOG v0.8.0 publicado.
- [ ] SPEC base atualizada (NCE sai de §11.2 out-of-scope).

---

## 13. Histórico

| Data | Versão | Mudança |
| --- | --- | --- |
| 2026-05-18 | v0.1 | Draft inicial alinhado às decisões travadas Onda 0 + MD-3/MD-5 |
