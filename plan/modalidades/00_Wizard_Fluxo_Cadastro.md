# 00 — Wizard de Cadastro de Contrato (fluxo proposto)

**Pergunta do PO atendida**: Q4 — *"Após a primeira tela de identificação, ele pergunta qual é a modalidade do contrato. O correto não seria perguntar isso antes da identificação para então informar a quantidade de parcelas?"*

**Resposta direta**: **O PO está correto.** Embora `Modalidade` já seja coletada no Step 1 do wizard atual (`ContratoCreatePage.vue:455-462`), ela está misturada com outros 9 campos. A proposta é tornar `Modalidade` um **passo dedicado anterior à identificação**, para que os campos subsequentes sejam condicionais à modalidade escolhida (incluindo a captura de periodicidade e quantidade de parcelas).

---

## 1. Resumo executivo

O fluxo atual tem 3 passos lineares com modalidade embutida no Step 1. Isso impede que campos como `Periodicidade`, `EstruturaAmortizacao`, `IndexadorId`, `PercentualExigidoGarantia` apareçam condicionalmente. Propõe-se:

- **Step 0 — Modalidade** (novo, mínimo, sem outros campos): cards visuais.
- **Step 1 — Identificação**: campos comuns + campos condicionais já calibrados à modalidade.
- **Step 2 — Detalhes específicos**: blocos polimórficos por modalidade.
- **Step 3 — Pré-visualização do cronograma**: usuário valida parcelas geradas.
- **Step 4 — Revisão e confirmação**: resumo final + termo de aceite.

O wizard será orquestrado por um componente Vue3 `<WizardStepResolver :modalidade>` que monta o fluxo dinamicamente.

---

## 2. Fluxo atual (resumo)

Conforme `ContratoCreatePage.vue:404-580`:

```
Step 1 — Dados Básicos
    ├── Número externo
    ├── Banco
    ├── Modalidade           ← decisivo, porém embutido
    ├── Moeda
    ├── Valor principal
    ├── Data contratação
    ├── Data vencimento      ← único, problemático (ver 00_Cronograma)
    ├── Taxa a.a.
    ├── Base de cálculo
    ├── Contrato-mãe (cond.)
    └── Observações

Step 2 — Detalhes (modal específico)
Step 3 — Revisão
```

Problemas:

1. Usuário só descobre quais campos importam após preencher metade.
2. Quando muda modalidade no Step 1, campos não se rearranjam coerentemente.
3. Não há onde colocar `Periodicidade` (cronograma), `Indexador` (taxa pós-fixada), `PercentualExigidoGarantia` etc.
4. Sem etapa de pré-visualização de cronograma — usuário só descobre se o cronograma "ficou certo" após criar.

---

## 3. Fluxo proposto (5 passos)

### 3.1 Step 0 — Modalidade

**Único objetivo**: escolher a modalidade. Sem outros campos.

```
+--------------------------------------------------------------------+
|  Qual o tipo do contrato?                                          |
|                                                                    |
|   +-------------+  +-------------+  +-------------+  +-----------+ |
|   |  FINIMP     |  |  4131       |  |  FGI        |  |  NCE/CCE  | |
|   |  Importação |  |  Lei 4.131  |  |  BNDES      |  |  Exportação| |
|   |  USD/EUR/.. |  |  USD        |  |  BRL        |  |  BRL      | |
|   +-------------+  +-------------+  +-------------+  +-----------+ |
|                                                                    |
|   +-------------+  +-------------+  +-------------+                |
|   |  REFINIMP   |  |  Balcão     |  |  NDF        |                |
|   |  Refin.     |  |  Caixa      |  |  Hedge      |                |
|   +-------------+  +-------------+  +-------------+                |
|                                                                    |
|   [Continuar →]                                                    |
+--------------------------------------------------------------------+
```

Tooltip em cada card descreve a modalidade. Cards desabilitados se o banco selecionado posteriormente não suportar (mas como banco é Step 1, este filtro ocorre na seleção de banco depois).

### 3.2 Step 1 — Identificação

Campos comuns a todas as modalidades:

```
+--------------------------------------------------------------------+
|  Identificação do contrato (FINIMP)                                |
+--------------------------------------------------------------------+
|  Banco:              [ BB         ▾]                               |
|  Número externo:     [ 416-AGE15405]                              |
|  Data contratação:   [ 14/05/2026 ]                                |
|  Moeda:              [ USD        ▾]                               |
|  Valor principal:    [ 1.000.000,00 ]                              |
|                                                                    |
|  ──── Estrutura de pagamento ─────                                 |
|  Periodicidade:      [ Semestral  ▾]   (cond. à modalidade)        |
|  Estrutura:          [ Bullet c/ juros periódicos ▾]               |
|  Data 1º vencimento: [ 14/11/2026 ]                                |
|  Qtd. parcelas:      [ 4 ]                                         |
|  Ancoragem do dia:   [ Mesmo dia da contratação ▾]                 |
|  Convenção dia útil: [ Following ▾]                                |
|                                                                    |
|  ──── Taxa ─────                                                   |
|  Tipo de taxa:       [ Fixa ▾]   ← se PosFixada, revela indexador  |
|  Taxa a.a. / Spread: [ 5,75% ]                                     |
|  Base de cálculo:    [ 360 ▾]                                      |
|                                                                    |
|  Observações:        [ ............................. ]            |
|                                                                    |
|  [← Voltar]                                          [Continuar →] |
+--------------------------------------------------------------------+
```

Para FGI/4131 pós-fixada, a seção de Taxa revela:

```
  Tipo de taxa:       [ Pós-fixada ▾]
  Indexador:          [ CDI ▾]
  % do indexador:     [ 100,00% ]
  Spread a.a.:        [ 5,24% ]
  Base de cálculo:    [ 252 ▾] (default puxado do indexador)
```

### 3.3 Step 2 — Detalhes específicos

Bloco polimórfico controlado por `<WizardStepResolver>`. Exemplos:

**FINIMP**
```
  ROF — número:        [ ............ ]
  ROF — emissão:       [ DD/MM/AAAA ]
  Banco recebedor exterior: [ ........ ]
  IOF câmbio:          [ 0,38% ]
  CDB cativo? :        [✓] Sim     Percentual exigido: [ 30% ]
                       Política liberação: [ Automática proporcional ▾]
  Termo de aceite (anexar PDF): [ Selecionar ]
```

**4131**
```
  Registro BACEN:      [ ............ ]
  Tomador exterior:    [ ............ ]
  Indexador externo:   [ SOFR ▾]
  SBLC anexo:          [✓] Sim   Banco emissor: [ .......... ]
                       Política liberação: [ Step-down agendado ▾]
                       [+ Adicionar step-down]
```

**FGI**
```
  Número FGI/BNDES:    [ ............ ]
  % cobertura FGI:     [ 80% ]
  Taxa FGI a.a.:       [ 0,30% ]
  Aval pessoal? :      [✓] Sim
  Política liberação:  [ Somente na quitação total ▾]
```

**NDF (Hedge)**
```
  Tipo:                [ Collar ▾]
  Vínculo de contrato: [ Contrato C-416/2026 (FINIMP USD 1MM) ▾]
  Múltiplos strikes? : [✓] Sim
  [+ Adicionar strike por vencimento]
    Vencimento [14/11/2026]  Strike Put [4,80]  Strike Call [5,20]
    Vencimento [14/05/2027]  Strike Put [4,90]  Strike Call [5,50]
```

### 3.4 Step 3 — Pré-visualização do cronograma

Endpoint `POST /api/contratos/cronograma-preview` recebe os dados dos Steps 0–2 e retorna a tabela de parcelas:

```
+-------------------------------------------------------------------+
|  Cronograma previsto (4 parcelas, semestral)                      |
+-------------------------------------------------------------------+
| #  | Data         | Tipo      | Principal   | Juros prov. | Total |
|----|--------------|-----------|-------------|-------------|-------|
| 1  | 14/11/2026*  | Principal | 250.000,00  |             |       |
| 1  | 14/11/2026*  | Juros     |             | 28.750,00   |       |
| 2  | 14/05/2027   | Principal | 250.000,00  |             |       |
| 2  | 14/05/2027   | Juros     |             | 21.562,50   |       |
| 3  | 16/11/2027*  | Principal | 250.000,00  |             |       |
| 3  | 16/11/2027*  | Juros     |             | 14.375,00   |       |
| 4  | 15/05/2028*  | Principal | 250.000,00  |             |       |
| 4  | 15/05/2028*  | Juros     |             |  7.187,50   |       |
+-------------------------------------------------------------------+
* Data ajustada por convenção Following (era domingo ou feriado)

[Editar parcela individual]    [Voltar]    [Continuar →]
```

Permite ao usuário editar manualmente alguma data ou valor antes de confirmar — com auditoria.

### 3.5 Step 4 — Revisão e confirmação

Resumo de todos os blocos preenchidos, com seções recolhíveis. Botão final: `Criar contrato`. Em caso de modalidade com risco (REFINIMP, 4131 sem SBLC), exibe alerta antes do `Confirmar`.

---

## 4. Estado atual no sistema

| Aspecto | Estado |
|---------|--------|
| Quantidade de steps | 3 |
| Modalidade — onde | Step 1 (junto com 9 outros campos) |
| Campos condicionais | Limitados, controlados por if no template |
| Pré-visualização de cronograma | Não existe |
| Componente resolver polimórfico | Não existe |
| Voltar e mudar modalidade | Permite, mas mantém valores → potencial inconsistência |

Referência: `sgcf-frontend/src/pages/ContratoCreatePage.vue:404-772`.

---

## 5. GAPs identificados

| # | GAP | Severidade |
|---|-----|-----------|
| G1 | Modalidade misturada com outros campos no Step 1 | Alta |
| G2 | Não há etapa de pré-visualização de cronograma | Alta |
| G3 | Não há componente polimórfico por modalidade | Média |
| G4 | Mudança de modalidade preserva estado anterior incorretamente | Média |
| G5 | Campos novos (periodicidade, indexador, garantia) não têm lar | Alta |
| G6 | Wizard não persiste rascunho — usuário perde dados se sai | Média |
| G7 | Sem indicador visual de quantos steps faltam por modalidade | Baixa |

---

## 6. Proposta técnica

### 6.1 Estrutura Vue3

```
ContratoCreatePage.vue (orquestrador)
├── StepModalidade.vue           (Step 0)
├── StepIdentificacao.vue        (Step 1, monta seções condicionais)
│   ├── BlocoIdentificacaoBase.vue
│   ├── BlocoEstruturaPagamento.vue
│   └── BlocoTaxa.vue
├── StepDetalhes.vue             (Step 2)
│   └── <WizardStepResolver :modalidade="modalidade">
│       ├── DetalheFinimp.vue
│       ├── Detalhe4131.vue
│       ├── DetalheFgi.vue
│       ├── DetalheNce.vue
│       ├── DetalheBalcao.vue
│       ├── DetalheRefinimp.vue
│       └── DetalheNdf.vue
├── StepCronograma.vue           (Step 3 — pré-visualização)
└── StepRevisao.vue              (Step 4)
```

`<WizardStepResolver>` recebe `modalidade` e despacha para o componente correto via map.

### 6.2 Estado global do wizard

Composable `useContratoWizardStore()` (Pinia) com:

```typescript
state: {
  modalidade: ModalidadeContrato | null,
  identificacao: { ... },
  detalhes: Record<string, unknown>,   // payload polimórfico
  cronogramaPreview: EventoCronogramaDto[],
  edicoesManuais: EventoCronogramaDto[],
  rascunhoId: string | null            // persistência local
}
```

Rascunho salvo automaticamente em `localStorage` a cada navegação entre steps, com TTL de 7 dias.

### 6.3 Comportamento ao mudar modalidade

Voltar do Step 1 para o Step 0 e alterar modalidade:
- Sistema avisa: "Alterar a modalidade descartará os campos específicos preenchidos. Continuar?"
- Se confirmar: preserva campos comuns (banco, número externo, data contratação, valor, moeda, observações) e zera o restante.

### 6.4 Validação progressiva

Cada step só permite avançar se seus campos forem válidos. Erros aparecem inline com mensagens em PT-BR. Botão "Continuar" desabilitado até passar na validação.

### 6.5 Persistência do rascunho

- Salva em `localStorage` a cada navegação.
- Banner no topo da página se houver rascunho recente: "Continuar contrato em rascunho? [Sim] [Descartar]".
- TTL de 7 dias.

---

## 7. Impacto em APIs

- `POST /api/contratos/cronograma-preview` — novo endpoint, retorna `EventoCronogramaDto[]` sem persistir.
- `POST /api/contratos` — payload reorganizado em três blocos: `identificacao`, `detalhes`, `cronogramaCustomizado` (opcional).
- `GET /api/contratos/modalidades` — retorna catálogo (já pode existir).
- `GET /api/contratos/modalidades/{cod}/campos-suportados` — devolve lista de campos por modalidade (uso da UI para montagem do Resolver).

---

## 8. Critérios de aceite

- [ ] Wizard com 5 steps (0 a 4) implementado conforme §3
- [ ] Componente `<WizardStepResolver>` dispatch por modalidade
- [ ] Endpoint `cronograma-preview` cobre 5 estruturas (Bullet, Price, SAC, BulletComJurosPeriodicos, Customizada)
- [ ] Mudança de modalidade em Step 0 mantém campos comuns e descarta específicos com confirmação
- [ ] Rascunho persiste em `localStorage` com TTL 7 dias
- [ ] Validação inline em PT-BR
- [ ] Cobertura de teste E2E (Cypress/Playwright) cobrindo um cadastro completo de FINIMP e um de FGI
- [ ] Acessibilidade: navegação por teclado funciona em todos os steps; foco gerenciado nas trocas de step
- [ ] Telemetria: evento por step concluído (para identificar drop-off no funil)

---

## 9. Pontos em aberto

| # | Decisão | Recomendação |
|---|---------|--------------|
| D1 | Permitir pular Step 3 (pré-visualização)? | Não — força revisão por ser etapa crítica |
| D2 | Step 4 — pedir aceite com assinatura digital? | Apenas em produção quando integração ICP-Brasil estiver pronta |
| D3 | Multi-tenant: filtrar modalidades por organização | Sim, via configuração por org |
| D4 | Cadastro em massa (CSV) | Sim, fora do wizard — endpoint dedicado |

---

## 10. Referências

- `sgcf-frontend/src/pages/ContratoCreatePage.vue:404-772`
- `00_Cronograma_Estrutura.md` (Step 1 — Estrutura de pagamento)
- `00_TaxasPosFixadas_Indexadores.md` (Step 1 — Taxa)
- `00_LiberacaoGarantia.md` (Step 2 — Política de garantia)
- `00_DiasUteis_Calendario.md` (Step 1 — Convenção de dia útil)
- Nielsen Norman Group — "Wizard Design Patterns" (2020)
