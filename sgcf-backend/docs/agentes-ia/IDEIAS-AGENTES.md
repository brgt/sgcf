# Ideias de Agentes de IA — SGCF

> Documento vivo de oportunidades de integração de agentes de IA ao SGCF.
> Status: **idealização** — nenhuma implementação iniciada.
> Última atualização: 2026-05-16.

---

## 1. Princípio Norteador

Antes de qualquer ideia, fica registrado o princípio que governa o uso de IA no SGCF:

1. **IA é assistente, não decisor.** Toda decisão financeira passa pela camada determinística do domínio (cálculo de CET, ranking, economia, baixa de pagamento). A IA acelera a captura de dados, enriquece a explicação e sinaliza atenção — mas não substitui regra de negócio auditável.
2. **Domínio permanece puro.** `Sgcf.Domain` e `Sgcf.Application` nunca chamam IA. Agentes vivem em `Sgcf.Mcp` (Model Context Protocol) e `Sgcf.A2a` (Agent-to-Agent).
3. **Human-in-the-loop em operações sensíveis.** Toda sugestão de IA que vire ação no domínio passa por confirmação humana explícita.
4. **Auditável por padrão.** Toda interação MCP/A2A já é logada no `audit_log` com `source=mcp|a2a`. Prompts e respostas relevantes devem ser persistidos.
5. **Falha gracioso.** Se a IA estiver indisponível, o sistema continua funcionando — IA é camada opcional, nunca dependência crítica.

---

## 2. Mapa de Oportunidades por Módulo

### 2.1. Cotações (novo módulo planejado)

| Agente | Tipo | Valor | Complexidade |
|--------|------|-------|--------------|
| **Parser de Proposta Bancária** | Extração estruturada | Alto — elimina digitação manual de propostas em PDF/email | Média |
| **Explicador de Ranking** | Geração de texto | Médio — facilita justificativa de escolha em comitês | Baixa |
| **Detector de Cláusulas Atípicas** | Análise comparativa | Alto — sinaliza covenants, exigências ou comissões fora do padrão histórico do banco | Alta |
| **Recomendador Contextual** | Recomendação multi-critério | Alto — considera limite disponível, concentração de risco, relacionamento, exigência de NDF | Alta |
| **Refresh de Cotação Defasada** | Alerta inteligente | Médio — quando dólar/NDF varia >X% desde captura, sugere refresh | Baixa |

**Detalhamento do Parser de Proposta Bancária (prioridade 1):**

- **Entrada:** PDF ou email do banco (BB, Bradesco, Santander, Itaú).
- **Saída:** JSON estruturado preenchendo a `PropostaBanco` — taxa, IOF, spread, prazo, garantias exigidas, moeda, condições NDF.
- **Workflow:** operador faz upload → IA extrai → operador revisa/corrige → grava.
- **Vantagem:** propostas têm formato variado entre bancos; um parser estruturado economiza ~5–10 min por cotação.
- **Tecnologia sugerida:** Claude com structured output (JSON schema da `PropostaBanco`).

**Detalhamento do Recomendador Contextual (prioridade 2):**

- Recebe: lista de propostas comparáveis + portfolio atual + limites por banco + histórico de relacionamento.
- Produz: ranking enriquecido com explicação textual ("Recomendo Santander porque, embora o CET seja 0,3 p.p. superior ao BB, há concentração de 65% da dívida USD no BB; alocar no Santander reduz risco concentrado e mantém limite do BB para próxima operação").
- Importante: a decisão final é humana; IA só explicita trade-offs.

---

### 2.2. Contratos

| Agente | Tipo | Valor |
|--------|------|-------|
| **Sumário Executivo do Contrato** | Geração de texto | Médio — gera resumo de 1 página de contratos longos para apresentação a comitês |
| **Detector de Divergência Cotação vs Fechamento** | Análise comparativa | Alto — alerta quando o que foi assinado difere materialmente da cotação aceita |
| **Q&A sobre Cláusulas (RAG)** | Conversational | Médio — operador pergunta "qual a multa de pré-pagamento?" e IA responde com base no contrato indexado |
| **Sugestor de Refinanciamento** | Recomendação | Alto — monitora contratos ativos e sinaliza oportunidades quando CDI/SELIC ou câmbio favorecem refinanciamento |

---

### 2.3. Painel / Tesouraria

| Agente | Tipo | Valor |
|--------|------|-------|
| **Narrador de KPIs** | Geração de texto sobre dados | Médio — gera narrativa mensal "dívida cresceu 8% por dois novos FINIMP em USD; exposição cambial subiu" |
| **Detector de Anomalias** | Análise estatística + LLM | Alto — sinaliza desvios em vencimentos, garantias, EBITDA |
| **Recomendador de Hedge** | Recomendação multi-fator | Alto — dado portfolio atual e cenário cambial, sugere proporção de hedge NDF |
| **Simulador Conversacional de Cenários** | Q&A | Médio — "e se o dólar subir 10%? E se o CDI cair 1pp?" — gera projeção narrativa baseada em queries determinísticas |

---

### 2.4. Cronograma e Pagamentos

| Agente | Tipo | Valor |
|--------|------|-------|
| **Otimizador de Antecipação** | Recomendação | Alto — dado caixa disponível, sugere qual parcela antecipar para maximizar economia (já temos o motor de simulação; IA escolhe melhor combinação) |
| **Previsor de Risco de Inadimplência** | Análise preditiva | Médio — dado fluxo de caixa projetado, sinaliza meses com risco de aperto |
| **Auto-categorização de Lançamentos** | Classificação | Médio — sugere conta contábil para novos lançamentos baseado em descrição |

---

### 2.5. Auditoria e Compliance

| Agente | Tipo | Valor |
|--------|------|-------|
| **Resumo Executivo de Atividade** | Sumarização | Médio — relatório diário/semanal "X contratos criados, Y aprovações pendentes, Z eventos suspeitos" |
| **Detector de Padrões Suspeitos** | Análise comportamental | Alto — acessos fora de horário, ações em massa, alterações repetidas no mesmo registro |
| **Auditor Conversacional** | Q&A sobre log | Médio — "quem modificou o contrato X na semana passada?" responde diretamente |

---

### 2.6. Plano de Contas

| Agente | Tipo | Valor |
|--------|------|-------|
| **Reconciliador Inteligente** | Matching aproximado | Alto — concilia lançamentos do sistema com extrato bancário mesmo com pequenas divergências de data/valor |
| **Sugestor de Conta** | Classificação | Médio — completa formulário de lançamento sugerindo conta contábil mais provável |

---

### 2.7. Operações Cross-Domínio

| Agente | Tipo | Valor |
|--------|------|-------|
| **CFO Assistant (A2A)** | Orquestrador multi-domínio | Alto — agente sênior que coordena outros agentes: "prepare relatório executivo do mês" → aciona Painel, Cronograma, Auditoria |
| **Onboarding de Novo Operador** | Conversational tutor | Baixo — ajuda novo usuário a navegar telas e entender conceitos |

---

## 3. Tipologia dos Agentes

Resumindo os padrões recorrentes:

1. **Parsers/Extractors** — transformam dado não-estruturado (PDF, email, planilha) em estrutura do domínio. Sempre com revisão humana.
2. **Explicadores** — geram narrativa textual sobre dados já calculados. Não inventam números.
3. **Detectores de Anomalia** — sinalizam atenção; não tomam ação.
4. **Recomendadores Contextuais** — apresentam opções ranqueadas com justificativa; decisão final é humana.
5. **Conversacionais (RAG)** — respondem perguntas sobre o estado do sistema; queries determinísticas por baixo.
6. **Orquestradores (A2A)** — agentes sênior que coordenam outros agentes para tarefas complexas.

---

## 4. Arquitetura de Integração

### 4.1. Camadas

```
┌─────────────────────────────────────────────────┐
│  Cliente (UI, ChatGPT plugin, Claude, etc.)     │
└──────────────────┬──────────────────────────────┘
                   │
        ┌──────────┴──────────┐
        │                     │
        ▼                     ▼
┌───────────────┐    ┌────────────────┐
│  Sgcf.Mcp     │    │  Sgcf.A2a      │   ← Camada de agentes
│  (tools/MCP)  │    │  (agent proto) │
└───────┬───────┘    └────────┬───────┘
        │                     │
        └──────────┬──────────┘
                   ▼
         ┌──────────────────┐
         │ Sgcf.Application │   ← Casos de uso (MediatR)
         └────────┬─────────┘
                  ▼
         ┌──────────────────┐
         │   Sgcf.Domain    │   ← Regras puras
         └──────────────────┘
```

- **Sgcf.Mcp** expõe ferramentas (tools) que agentes externos consomem.
- **Sgcf.A2a** permite que agentes do SGCF conversem com outros agentes (CFO Assistant orquestra subagentes).
- Ambos só dependem de `Sgcf.Application` — não acessam `Sgcf.Infrastructure` diretamente.
- O domínio nunca sabe que IA existe.

### 4.2. Padrões obrigatórios

- **Toda chamada de agente registra no `audit_log`** com `source=mcp|a2a`, prompt resumido, tool chamada, decisão tomada.
- **Toda ação irreversível requer confirmação humana** — agente sugere, humano clica "aplicar".
- **Idempotência:** chamadas repetidas com mesmo input produzem mesmo efeito (importante para retry).
- **Rate limit por agente/usuário** — controle de custo.
- **Versionamento de prompts** — prompts em arquivos versionados (`prompts/v1/parser_proposta.md`); resposta inclui versão usada.

---

## 5. Priorização Sugerida (Roadmap)

### Onda 1 — Quick wins (alto valor, baixo risco)
1. **Parser de Proposta Bancária** (Cotações) — elimina digitação repetitiva.
2. **Auto-categorização de Lançamentos** (Plano de Contas) — reduz erros.
3. **Resumo Executivo de Auditoria** — útil sem risco operacional.

### Onda 2 — Habilita decisões melhores
4. **Explicador de Ranking** (Cotações).
5. **Narrador de KPIs** (Painel).
6. **Sugestor de Refinanciamento** (Contratos).

### Onda 3 — Inteligência de portfolio
7. **Recomendador Contextual** (Cotações com critérios qualitativos).
8. **Recomendador de Hedge** (Painel).
9. **Detector de Cláusulas Atípicas** (Contratos/Cotações).
10. **Otimizador de Antecipação** (Cronograma).

### Onda 4 — Orquestração
11. **CFO Assistant (A2A)** — orquestra os agentes acima.
12. **Q&A Conversacional Cross-Domínio**.

---

## 6. Considerações Operacionais

### 6.1. Custo
- Estimar tokens por agente por operação.
- Cache agressivo para conteúdo estável (catálogo de contas, histórico bancário).
- Modelos menores (Haiku) para tarefas estruturadas (parsing, classificação); modelos maiores (Sonnet/Opus) para análise contextual.

### 6.2. Latência
- Operações síncronas (parser ao subir PDF): aceitar até 5s.
- Operações de fundo (narrador de KPIs mensal): job assíncrono.
- Conversacional: streaming de resposta.

### 6.3. Privacidade e LGPD
- Não enviar PII de clientes/funcionários ao LLM externo sem anonimização.
- Dados financeiros agregados podem ser processados; documentos específicos exigem revisão.
- Considerar self-hosted ou regional para dados sensíveis.

### 6.4. Confiabilidade
- Toda saída estruturada validada contra JSON Schema antes de persistir.
- Toda recomendação acompanha "nível de confiança" — operador vê se IA está incerta.
- Toda alucinação detectável (número fora de faixa, conta inexistente) deve falhar com erro claro, não com chute aceito.

### 6.5. Observabilidade
- Métricas por agente: tempo de resposta, taxa de aceitação de sugestão pelo humano, taxa de erro de validação.
- A taxa de aceitação é o **KPI primário** — se humanos sempre rejeitam, o agente não está ajudando.

---

## 7. Anti-padrões (o que evitar)

- ❌ **IA calculando CET, IOF, juros ou economia.** Isso é matemática fechada e auditável; IA introduz incerteza onde não pode haver.
- ❌ **IA fechando contrato sem revisão humana.** Compliance e auditoria não aceitam.
- ❌ **IA consumindo `Sgcf.Infrastructure` diretamente.** Quebra arquitetura.
- ❌ **Prompts hardcoded no código.** Versionados em arquivos, com testes de regressão.
- ❌ **Acoplar funcionalidade crítica a um provedor específico.** Abstrair atrás de interface (`ILlmClient`).
- ❌ **Confiar em saída sem validação.** Toda resposta passa por validação estrutural antes de virar dado do domínio.

---

## 8. Próximos Passos (quando começarmos)

1. Escolher primeiro agente para piloto — recomendação: **Parser de Proposta Bancária** (alto valor, baixo risco, escopo bem definido).
2. Escrever SPEC dedicada do agente piloto (entradas, saídas, schema, métricas de sucesso).
3. Implementar abstração `ILlmClient` em `Sgcf.Application` (interface) e adapter em `Sgcf.Infrastructure`.
4. Implementar tool MCP correspondente em `Sgcf.Mcp`.
5. Definir métricas de aceitação e monitorar.

---

## 9. Histórico de Decisões

| Data | Decisão | Razão |
|------|---------|-------|
| 2026-05-16 | Documento criado | Capturar ideias de IA antes da implementação do módulo Cotações, para que módulo seja desenhado considerando pontos futuros de extensão |
| 2026-05-16 | IA fica fora do MVP do módulo Cotações | Decisão do PO: priorizar fluxo manual auditável; IA entra como Onda 1 após MVP estabilizado |

---

## 10. Glossário Rápido

- **MCP** (Model Context Protocol): protocolo padrão para expor ferramentas a LLMs.
- **A2A** (Agent-to-Agent): comunicação entre agentes para tarefas multi-passo.
- **RAG** (Retrieval-Augmented Generation): LLM consulta base de conhecimento antes de responder.
- **CET**: Custo Efetivo Total — métrica regulada de custo de operação financeira.
- **PTAX**: taxa de câmbio de referência do BACEN.
- **NDF** (Non-Deliverable Forward): instrumento de hedge cambial.
