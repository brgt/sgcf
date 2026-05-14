# 00 — Liberação Proporcional de Garantia

**Pergunta do PO atendida**: Q8 — _"Para contratos de longo prazo, o banco libera a garantia conforme o saldo final. Por exemplo, 30% de garantia sobre a operação: ao quitar uma parcela o saldo se altera e o excedente é liberado. Como o sistema prevê?"_

**Resposta direta**: A entidade `Garantia` existe (`Garantia.cs:1-80`) com campo `PercentualPrincipal`, mas **não há regra automática** de recálculo nem entidade `EventoLiberacaoGarantia`. Este documento define como construir o fluxo.

---

## 1. Resumo executivo

Em contratos de longo prazo (FINIMP > 360d, 4131 plurianuais, FGI), o banco exige garantia proporcional ao saldo devedor remanescente. Quando o tomador amortiza, o saldo cai e a garantia excedente deve ser liberada. O processo deve:

1. Manter um percentual contratual de garantia exigida (`PercentualExigido`).
2. Recalcular a garantia exigida a cada evento de pagamento.
3. Gerar `EventoLiberacaoGarantia` quando há excedente.
4. Suportar fluxos de aprovação (manual, automático, automático com janela).
5. Refletir a liberação na garantia (status, valor remanescente, data efetiva).

O modelo atual cobre o cadastro estático da garantia, mas não tem **dinâmica de liberação**.

---

## 2. Cenário de negócio

### 2.1 Padrões observados por modalidade

| Modalidade         | Garantia típica                     | Política de liberação                                            |
| ------------------ | ----------------------------------- | ---------------------------------------------------------------- |
| **FINIMP**         | CDB cativo 30% do principal         | Liberação automática proporcional após cada amortização          |
| **4131**           | CDB 30% do principal (ou step-down) | Step-down agendado anualmente ou proporcional                    |
| **FGI BNDES**      | Aval FGI 80% + 20% pessoal          | Geralmente sem liberação parcial; libera só na quitação total    |
| **Balcão (Caixa)** | Cessão fiduciária de duplicatas     | Cada duplicata é "garantia individual" — libera ao ser liquidada |
| **REFINIMP**       | Herda do contrato-mãe               | Ajusta na proporção do refinanciamento                           |
| **NCE/CCE**        | Aval/duplicatas                     | Conforme contrato                                                |

### 2.2 Três cenários práticos

**Cenário A — FINIMP CDB 30%**

- Principal: USD 1.000.000; cotação contratada R$ 5,00 → saldo BRL inicial R$ 5.000.000.
- CDB cativo exigido: 30% × R$ 5.000.000 = R$ 1.500.000.
- Após pagamento de USD 250.000 (cotação efetiva R$ 5,10), saldo cai para USD 750.000.
- Saldo BRL atualizado (cotação corrente R$ 5,15) ≈ R$ 3.862.500.
- Garantia exigida = 30% × R$ 3.862.500 ≈ R$ 1.158.750.
- Excedente liberável = R$ 1.500.000 − R$ 1.158.750 = R$ 341.250.
- Gera `EventoLiberacaoGarantia` com valor R$ 341.250.

**Cenário B — 4131 SBLC 100% com step-down**

- Principal: USD 5.000.000; SBLC inicial USD 5.000.000.
- Contrato prevê step-down: 100% no ano 1, 75% no ano 2, 50% no ano 3.
- Eventos de liberação são **programados**, não derivados do saldo.
- Após cada quitação trimestral, sistema também recalcula e, se saldo já está abaixo do step-down vigente, alerta sobre redução adicional possível.

**Cenário C — FGI Aval 80% + 20% pessoal**

- Aval FGI cobre 80%; aval pessoal/avalistas cobre 20%.
- Não há liberação parcial em geral — política é `LiberacaoNoFimDoContrato`.
- Sistema gera `EventoLiberacaoGarantia` somente quando contrato é quitado totalmente.

### 2.3 Tipos de garantia já modelados

A `Garantia` polimórfica cobre (ver `plan/Anexo_B_Modalidades_e_Modelo_Dados.md:742-766`):

| Tipo                   | Liberável proporcionalmente?            |
| ---------------------- | --------------------------------------- |
| `CDB_CATIVO`           | Sim, ao saldo devedor                   |
| `SBLC`                 | Sim, geralmente por step-down agendado  |
| `AVAL`                 | Não (ou na quitação total)              |
| `ALIENACAO_FIDUCIARIA` | Não típico em curto prazo               |
| `DUPLICATAS`           | Sim, automático por duplicata liquidada |
| `RECEBIVEIS_CARTAO`    | Sim, conforme fluxo                     |
| `BOLETO_BANCARIO`      | Sim, conforme fluxo                     |
| `FGI`                  | Não (na quitação total)                 |

---

## 3. Estado atual no sistema

### 3.1 O que existe

- `Sgcf.Domain/Contratos/Garantia.cs:1-80` — entidade master com:
  - `ContratoId`, `Tipo: TipoGarantia`
  - `ValorBrl: Money` (sempre BRL)
  - `PercentualPrincipal: Percentual?` (interpretado hoje como "qual % do principal representa esta garantia" — diferente de `PercentualExigido`)
  - `DataConstituicao`, `DataLiberacaoPrevista`, `DataLiberacaoEfetiva`
  - `Status: StatusGarantia { Ativa, Liberada, Executada, Cancelada }`
- 8 entidades polimórficas por tipo (CDB, SBLC, etc.)

### 3.2 Limitações

- Sem `PercentualExigido` separado do `PercentualPrincipal`.
- Sem entidade de evento de liberação (histórico de liberações parciais).
- Sem trigger automático em pagamento.
- `DataLiberacaoEfetiva` é única — não suporta liberações parciais múltiplas.
- Sem política de liberação configurável por contrato.

---

## 4. GAPs identificados

| #   | GAP                                                                   | Severidade | Quem afeta                     |
| --- | --------------------------------------------------------------------- | ---------- | ------------------------------ |
| G1  | Não há campo `PercentualExigido` separado                             | Alta       | FINIMP CDB, SBLC step-down     |
| G2  | Não há entidade `EventoLiberacaoGarantia` para histórico              | Alta       | Auditoria, liberações parciais |
| G3  | Pagamento de parcela não dispara recálculo de garantia                | Alta       | FINIMP, 4131                   |
| G4  | Sem `PoliticaLiberacao` configurável                                  | Média      | Todas                          |
| G5  | Sem agendamento de step-down (4131 SBLC)                              | Média      | 4131                           |
| G6  | Sem fluxo de aprovação (manual vs automático)                         | Média      | Governança                     |
| G7  | Status `Liberada` é binário; precisa suportar "parcialmente liberada" | Média      | Todas que liberam parcialmente |
| G8  | Sem reflexo na cotação de FX (saldo BRL muda com câmbio)              | Média      | FINIMP, 4131                   |

---

## 5. Proposta de estrutura

### 5.1 Campos novos em `Garantia`

| Campo                    | Tipo               | Descrição                                                                           |
| ------------------------ | ------------------ | ----------------------------------------------------------------------------------- |
| `PercentualExigido`      | `decimal(7,6)`     | Percentual do saldo devedor que deve ser coberto por esta garantia. Ex.: 0.30 = 30% |
| `PoliticaLiberacao`      | `enum`             | `Manual`, `AutomaticaProporcional`, `StepDownAgendado`, `SomenteNaQuitacaoTotal`    |
| `ValorLiberadoAcumulado` | `Money`            | Soma das liberações parciais                                                        |
| `ValorRemanescente`      | `Money` (computed) | `ValorBrl - ValorLiberadoAcumulado`                                                 |
| `JanelaAprovacaoDias`    | `int?`             | Para política `AutomaticaProporcional` — espera N dias antes de executar            |

### 5.2 Novo enum

```csharp
public enum PoliticaLiberacaoGarantia
{
    Manual,                       // só libera quando operador clicar "liberar"
    AutomaticaProporcional,       // recalcula a cada pagamento; libera excedente
    StepDownAgendado,             // segue datas/percentuais pré-cadastrados
    SomenteNaQuitacaoTotal        // libera 100% só após último pagamento
}

public enum StatusEventoLiberacao
{
    Pendente,
    AprovadoAutomaticamente,
    AguardandoAprovacaoManual,
    Aprovado,
    Recusado,
    Executado,
    Cancelado
}
```

### 5.3 Nova entidade `EventoLiberacaoGarantia`

| Campo                  | Tipo                         | Descrição                                                                     |
| ---------------------- | ---------------------------- | ----------------------------------------------------------------------------- |
| `Id`                   | `Guid`                       | PK                                                                            |
| `GarantiaId`           | `Guid`                       | FK                                                                            |
| `ContratoId`           | `Guid`                       | FK (denormalizado para consulta)                                              |
| `Origem`               | `enum OrigemEventoLiberacao` | `PagamentoParcela`, `StepDown`, `QuitacaoTotal`, `Manual`, `RecalculoCambial` |
| `EventoCronogramaId`   | `Guid?`                      | Quando origem é pagamento, aponta para o evento                               |
| `DataEvento`           | `Instant`                    | Quando foi gerado                                                             |
| `SaldoDevedorAntes`    | `Money` (BRL)                | Saldo antes do pagamento que disparou                                         |
| `SaldoDevedorApos`     | `Money` (BRL)                | Após o pagamento                                                              |
| `GarantiaExigidaAntes` | `Money`                      | `PercentualExigido × SaldoAntes`                                              |
| `GarantiaExigidaApos`  | `Money`                      | `PercentualExigido × SaldoApos`                                               |
| `ValorLiberar`         | `Money`                      | Diferença (sempre ≥ 0)                                                        |
| `Status`               | `StatusEventoLiberacao`      | —                                                                             |
| `AprovadoPor`          | `UserId?`                    | Quem aprovou (se manual)                                                      |
| `DataAprovacao`        | `Instant?`                   | —                                                                             |
| `DataExecucao`         | `Instant?`                   | Quando o banco efetivamente devolveu                                          |
| `Observacoes`          | `string(500)?`               | —                                                                             |

### 5.4 Nova entidade `AgendaStepDown` (para SBLC etc.)

| Campo                   | Tipo           | Descrição                                  |
| ----------------------- | -------------- | ------------------------------------------ |
| `Id`                    | `Guid`         | PK                                         |
| `GarantiaId`            | `Guid`         | FK                                         |
| `DataPrevista`          | `LocalDate`    | Quando o step-down deve ocorrer            |
| `PercentualAlvoPosStep` | `decimal(7,6)` | Ex.: 0.75 (passa a exigir 75% após o step) |
| `Executado`             | `bool`         | —                                          |
| `EventoLiberacaoId`     | `Guid?`        | Liga ao evento gerado                      |

### 5.5 Algoritmo de liberação automática

```pseudo
ao registrar PagamentoEventoCronograma(p):
    contrato = p.Contrato
    saldoAntes = contrato.SaldoDevedorBrl()
    aplicar(p)                              // saldo cai
    saldoApos = contrato.SaldoDevedorBrl()  // recalculado

    para cada g em contrato.Garantias com Status = Ativa:
        se g.PoliticaLiberacao == AutomaticaProporcional:
            exigidoAntes = g.PercentualExigido * saldoAntes
            exigidoApos  = g.PercentualExigido * saldoApos
            excedente    = max(0, g.ValorRemanescente - exigidoApos)

            se excedente > 0:
                criar EventoLiberacaoGarantia(
                    GarantiaId = g.Id,
                    Origem = PagamentoParcela,
                    EventoCronogramaId = p.EventoCronogramaId,
                    SaldoDevedorAntes = saldoAntes,
                    SaldoDevedorApos  = saldoApos,
                    GarantiaExigidaAntes = exigidoAntes,
                    GarantiaExigidaApos  = exigidoApos,
                    ValorLiberar = excedente,
                    Status = se g.JanelaAprovacaoDias > 0 então AguardandoAprovacaoManual senão AprovadoAutomaticamente
                )
```

### 5.6 Recálculo cambial

Para garantia em BRL contra principal em moeda estrangeira, o saldo BRL exigido oscila com câmbio. Estratégia:

- **Cotação travada**: usa cotação contratada (`Contrato.TaxaFx`) — sem recálculo cambial. Recomendado para FINIMP CDB padrão (BB).
- **Cotação corrente**: usa cotação de mercado D-1 — recalcula diariamente e pode gerar `EventoLiberacaoGarantia` por flutuação. Recomendado para SBLC sem call-margin.

Campo novo no `Contrato`: `BaseCambioGarantia: enum { Contratada, Corrente }`.

### 5.7 Step-down agendado

Job diário verifica `AgendaStepDown` com `DataPrevista <= hoje` e `Executado = false`:

- Ajusta `Garantia.PercentualExigido` para `PercentualAlvoPosStep`.
- Gera `EventoLiberacaoGarantia` com `Origem = StepDown` para liberar o excedente.

### 5.8 Quitação total

Quando último pagamento zera o saldo:

- Para todas as garantias `Ativa`, gera `EventoLiberacaoGarantia` com `Origem = QuitacaoTotal`, valor = `ValorRemanescente`.
- Quando executado, muda `Garantia.Status` para `Liberada`.

---

## 6. Impacto no wizard

- Step 2 (Detalhes específicos) ganha sub-seção "Política de garantia":
  - `PercentualExigido` (% do saldo devedor)
  - `PoliticaLiberacao` (dropdown)
  - Para `StepDownAgendado`: editor de cronograma com `DataPrevista` + `PercentualAlvoPosStep`
  - `BaseCambioGarantia` (quando moeda estrangeira)
  - `JanelaAprovacaoDias` (opcional)

---

## 7. Impacto em APIs

- `POST /api/contratos/{id}/garantias` — payload ganha campos de §5.1.
- `POST /api/contratos/{id}/garantias/{garantiaId}/step-down` — cria/edita agenda.
- `GET /api/contratos/{id}/garantias/{garantiaId}/eventos-liberacao` — histórico.
- `POST /api/eventos-liberacao/{id}/aprovar` — aprovação manual.
- `POST /api/eventos-liberacao/{id}/executar` — registra execução pelo banco.
- Webhook/notificação quando `EventoLiberacaoGarantia` é criado com status `AguardandoAprovacaoManual`.

---

## 8. Critérios de aceite

- [ ] Campos `PercentualExigido` e `PoliticaLiberacao` migrados em `Garantia`
- [ ] Entidade `EventoLiberacaoGarantia` criada com 6 status
- [ ] Entidade `AgendaStepDown` criada
- [ ] Pagamento de evento de cronograma dispara recálculo de todas as garantias com `AutomaticaProporcional`
- [ ] Cenário A (FINIMP CDB 30%) coberto por teste de integração: pagamento de 25% → libera 25% do CDB
- [ ] Cenário B (4131 SBLC step-down) coberto por teste com agenda
- [ ] Cenário C (FGI quitação total) coberto
- [ ] UI lista eventos pendentes de aprovação manual em dashboard administrativo
- [ ] Job diário de step-down agendado funcionando
- [ ] Logs de auditoria em todas as transições de status

---

## 9. Pontos em aberto

| #   | Decisão                                                                               | Recomendação                                                                                                                                             |
| --- | ------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D1  | Liberação automática deve ser executada de imediato ou aguardar confirmação do banco? | Criar evento `AprovadoAutomaticamente` mas só mover `Garantia.ValorLiberadoAcumulado` quando operador marca `Executado` (refletindo a operação no banco) |
| D2  | Notificar tomador automaticamente sobre liberação?                                    | Sim, e-mail + webhook com valor liberável                                                                                                                |
| D3  | Permitir "antecipar" liberação contra pagamentos futuros previstos?                   | Não — só sobre saldo realizado                                                                                                                           |
| D4  | Reverter liberação se pagamento for estornado?                                        | Sim, mediante `EventoLiberacaoGarantia.Status = Cancelado` e ajuste de `ValorLiberadoAcumulado`                                                          |

---

## 10. Referências

- `Sgcf.Domain/Contratos/Garantia.cs`
- `plan/Anexo_B_Modalidades_e_Modelo_Dados.md:742-766` (catálogo de garantias)
- `plan/Anexo_B_Modalidades_e_Modelo_Dados.md:860-912` (validações de cobertura)
- `plan/Anexo_C_Regras_Antecipacao_Pagamento.md` (interação com antecipações)
- ISP 98 / URDG 758 (regras internacionais de SBLC)
- Resolução BNDES sobre FGI
- `00_Cronograma_Estrutura.md` — gatilho `pagamentoEventoCronograma`
