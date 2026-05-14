# Plano — Análise das Modalidades de Contrato (Resposta às 8 perguntas do PO)

**Data**: 2026-05-14
**Autor**: Claude (atuando como analista de sistemas sênior, domínio: empréstimos no Brasil)
**Escopo aprovado**: apenas documentos analíticos (sem código). Implementação fica para sprint futura.
**Local dos entregáveis**: `plan/modalidades/*.md`

---

## 1. Objetivo

Produzir documentos analíticos, **separados por tipo de operação**, que respondam de forma rastreável às 8 perguntas levantadas pelo PO sobre o cadastro de contratos. Cada documento descreve:

1. Cenário de negócio (linguagem do PO)
2. Como o sistema atual modela (refs `sgcf-backend/...:linha`)
3. GAPs identificados
4. Proposta de estrutura (modelo de dados, regras, validações, UI)
5. Critérios de aceite que permitam um dev implementar sem dúvidas

---

## 2. Mapeamento Pergunta → Documento

| # | Pergunta do PO | Natureza | Documento(s) primário(s) |
|---|----------------|----------|--------------------------|
| 1 | Data de vencimento única vs cronograma de parcelas | Transversal | `00_Cronograma_Estrutura.md` + FINIMP/FGI/4131 |
| 2 | Vencimento em feriado/fim-de-semana → próximo dia útil | Transversal (todas modalidades) | `00_DiasUteis_Calendario.md` |
| 3 | FINIMP 720 dias com quitações semestrais | Modalidade | `FINIMP.md` |
| 4 | Ordem do wizard: modalidade antes da identificação | UI/UX transversal | `00_Wizard_Fluxo_Cadastro.md` |
| 5 | NDF com strikes diferentes por vencimento | Modalidade derivativo | `NDF.md` (referenciado por `4131.md`) |
| 6 | 4131 com quitações trimestral/semestral/mensal | Modalidade | `4131.md` |
| 7 | FGI pós-fixada com CDI + spread | Modalidade + transversal | `00_TaxasPosFixadas_Indexadores.md` + `FGI.md` |
| 8 | Liberação proporcional de garantia conforme amortização | Transversal (contratos longos) | `00_LiberacaoGarantia.md` |

---

## 3. Estado atual do sistema (resumo da investigação)

Levantamento feito em `sgcf-backend/src/` e `sgcf-frontend/src/`:

| Item | Status | Onde |
|------|--------|------|
| Wizard de cadastro | 3 steps (Dados Básicos → Detalhes → Revisão); modalidade já é selecionada no Step 1 | `ContratoCreatePage.vue:404-419,455-462` |
| `Data de Vencimento` | Campo único `<input type="date">` no Step 1 | `ContratoCreatePage.vue:514-530` |
| `EventoCronograma` | Existe; status Previsto/Pago/Atrasado; sem regra de dia útil | `Domain/Cronograma/EventoCronograma.cs:1-84` |
| `Garantia` | Existe com 8 tipos polimórficos; tem `PercentualPrincipal` mas sem lógica de liberação proporcional | `Domain/Contratos/Garantia.cs:1-80` |
| `InstrumentoHedge` | Existe com `StrikeForward`/`StrikePut`/`StrikeCall` — **valores únicos por hedge**, sem suporte a strikes por vencimento | `Domain/Hedge/InstrumentoHedge.cs:1-111` |
| Calendário de feriados / dia útil | **NÃO IMPLEMENTADO** | — |
| `Indexador`/CDI/`TaxaReferencia` (pós-fixada) | **NÃO IMPLEMENTADO** | `CalculadorJuros` usa só `Percentual` + `BaseCalculo` |
| Periodicidade configurável (mensal/trimestral/etc.) | Existe modelo `CRONOGRAMA_PAGAMENTO` em `plan/Anexo_B`, mas geração via Strategy ainda em backlog | `plan/Anexo_B...md:321-331,364` |

Conclusão: dos 8 cenários, **6 têm GAPs** vs. estado atual e **2 estão parcialmente cobertos** (cronograma e garantia).

---

## 4. Dependency Graph (ordem de escrita)

```
Phase 1 — Fundações transversais (escrever PRIMEIRO):
  T1  00_DiasUteis_Calendario.md            (Q2)
  T2  00_Cronograma_Estrutura.md            (Q1, Q3, Q6)
  T3  00_TaxasPosFixadas_Indexadores.md     (Q7)
  T4  00_LiberacaoGarantia.md               (Q8)
  T5  00_Wizard_Fluxo_Cadastro.md           (Q4)

  >>> Checkpoint 1: PO revisa as 5 fundações

Phase 2 — Documentos por modalidade (consolidam as transversais):
  T6  FINIMP.md   (consolida Q1, Q3, Q8)
  T7  4131.md     (consolida Q1, Q5, Q6, Q8)
  T8  FGI.md      (consolida Q1, Q7, Q8)
  T9  NDF.md      (consolida Q5)

  >>> Checkpoint 2: PO revisa modalidades

Phase 3 — Fechamento:
  T10 README.md   (índice + mapa pergunta→doc)
  T11 Patch em SPEC.md §X com link para o pacote
```

**Justificativa da ordem**: os documentos por modalidade (Phase 2) referenciam os transversais (Phase 1). Escrever transversais primeiro evita duplicação e mantém uma única fonte da verdade por regra.

---

## 5. Slicing vertical

Cada documento é uma fatia vertical completa (cenário → status atual → GAP → proposta → aceite). Não há slicing horizontal por camada (dados/regra/UI separados) — cada doc trata as três camadas de ponta a ponta.

---

## 6. Template obrigatório de cada documento

```markdown
# [Nome do tópico/modalidade]

## 1. Resumo executivo (≤ 200 palavras)
## 2. Cenário de negócio
   2.1 Casos práticos (com exemplos numéricos)
   2.2 Regras dos bancos / mercado (citar bancos específicos quando aplicável)
## 3. Estado atual no sistema
   3.1 O que existe (com refs file:line)
   3.2 Limitações observadas
## 4. GAPs identificados
   Tabela: GAP | Severidade | Quem afeta
## 5. Proposta de estrutura
   5.1 Modelo de dados (entidades + campos novos/alterados)
   5.2 Regras de negócio e validações
   5.3 Impacto no wizard / UI
   5.4 Impacto em APIs / contratos REST
## 6. Critérios de aceite (checklist binário)
## 7. Pontos em aberto / decisões para o PO
## 8. Referências (PDFs em CONTRATOS_MODELOS/, normas BACEN, ANBIMA, etc.)
```

---

## 7. Critérios de aceite globais

- [ ] As 8 perguntas estão respondidas e rastreáveis em pelo menos um documento
- [ ] Cada documento cita pelo menos uma referência ao código atual (`file:line`) ou justifica ausência ("não implementado")
- [ ] Cada documento lista GAPs explícitos vs. estado atual
- [ ] Proposta de modelo de dados é completa (todos os campos novos nomeados)
- [ ] Linguagem PT-BR formal, voz ativa, sem contrações nem emojis
- [ ] Cada documento ≤ 600 linhas (preferencialmente ≤ 400)
- [ ] `README.md` final contém matriz pergunta-resposta clicável

---

## 8. Verificação ao final

1. Listar `plan/modalidades/` e contar 11 arquivos (`README` + 5 transversais + 4 modalidades + 1 patch nota no SPEC)
2. Grep nas 8 perguntas (palavras-chave) e confirmar resposta em cada doc apontado pela matriz §2
3. Apresentar resumo de 1 página ao PO com link para cada doc

---

## 9. Fora de escopo (explícito)

- Implementação de código (entidades, migrations, UI) — fica para sprint futura
- Atualização do `Anexo_B_Modalidades_e_Modelo_Dados.md` (será referenciado, não reescrito)
- Decisões finais sobre escolha de biblioteca de feriados (NodaTime calendar vs BACEN API vs ANBIMA) — listadas como ponto em aberto

---

## 10. Riscos

| Risco | Mitigação |
|-------|-----------|
| PO discorda da modelagem proposta em algum doc | Checkpoint 1 e 2 são obrigatórios antes de avançar |
| Documentos crescem demais e perdem foco | Limite de 600 linhas por doc; conteúdo cross-cutting fica nos `00_*.md` |
| Conflito com `plan/Anexo_B` existente | Cada doc novo referencia o Anexo B; nunca duplica conteúdo |
