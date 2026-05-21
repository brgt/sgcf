# Integração Front-end — Fluxos Completos

Este documento apresenta quatro fluxos de uso com exemplos de código. Todos os exemplos assumem que o token JWT foi obtido previamente e está disponível na variável `token`.

---

## Convenções dos exemplos

```typescript
const BASE_URL = "https://api.sgcf.example/api/v1";

function headers(idempotencyKey?: string): Record<string, string> {
  const h: Record<string, string> = {
    "Content-Type": "application/json",
    "Authorization": `Bearer ${token}`,
  };
  if (idempotencyKey) {
    h["Idempotency-Key"] = idempotencyKey;
  }
  return h;
}

function uuid(): string {
  return crypto.randomUUID(); // UUID v4
}
```

---

## Fluxo 1 — Criar cenário, adicionar simulações, ativar e consultar o Quadro da Dívida

**Caso de uso:** A tesouraria cria o cenário "Realista 2026", adiciona duas captações hipotéticas, ativa o cenário e consulta o Quadro da Dívida com o overlay aplicado.

### Passo 1.1 — Criar cenário (POST /simulacoes/cenarios)

```typescript
const criarCenario = await fetch(`${BASE_URL}/simulacoes/cenarios`, {
  method: "POST",
  headers: headers(uuid()),
  body: JSON.stringify({
    nome: "Realista 2026",
    anoBase: 2026,
    descricao: "Captações previstas para expansão da unidade SP",
  }),
});

// Espere: 201 Created
const cenario = await criarCenario.json();
// cenario.id   → Guid do cenário
// cenario.status → "Rascunho"
const cenarioId = cenario.id;
```

### Passo 1.2 — Adicionar primeira simulação (POST /simulacoes/cenarios/{id}/simulacoes)

```typescript
const adicionarSim1 = await fetch(
  `${BASE_URL}/simulacoes/cenarios/${cenarioId}/simulacoes`,
  {
    method: "POST",
    headers: headers(uuid()),
    body: JSON.stringify({
      bancoId: "3fa85f64-5717-4562-b3fc-2c963f66afa6", // Itaú
      modalidade: "CapitalDeGiro",
      moeda: "Brl",
      valorPrincipal: 10000000.00,
      dataContratacaoPrevista: "2026-07-01",
      dataPrimeiroVencimento: "2026-08-01",
      tipoTaxa: "CdiSpread",
      taxaAa: null,
      spreadAa: 2.50,
      baseCalculo: "Dias252",
      estruturaAmortizacao: "Sac",
      periodicidade: "Mensal",
      quantidadeParcelas: 24,
      anchorDiaMes: "DiaContratacao",
      anchorDiaFixo: null,
      garantiaExigidaPrevista: null,
      observacoes: "Capital de giro — expansão SP",
    }),
  }
);

// Espere: 201 Created — retorna CenarioSimulacaoDto com simulacoes[0]
const cenarioComSim1 = await adicionarSim1.json();
```

### Passo 1.3 — Adicionar segunda simulação

```typescript
await fetch(`${BASE_URL}/simulacoes/cenarios/${cenarioId}/simulacoes`, {
  method: "POST",
  headers: headers(uuid()),
  body: JSON.stringify({
    bancoId: "8a1b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d", // Santander
    modalidade: "Nce",
    moeda: "Brl",
    valorPrincipal: 5000000.00,
    dataContratacaoPrevista: "2026-10-01",
    dataPrimeiroVencimento: "2026-11-01",
    tipoTaxa: "Fixa",
    taxaAa: 14.80,
    spreadAa: null,
    baseCalculo: "Dias252",
    estruturaAmortizacao: "Price",
    periodicidade: "Mensal",
    quantidadeParcelas: 12,
    anchorDiaMes: "DiaFixo",
    anchorDiaFixo: 15,
  }),
});
// Espere: 201 Created
```

### Passo 1.4 — Ativar o cenário (POST /simulacoes/cenarios/{id}/ativar)

```typescript
const ativar = await fetch(
  `${BASE_URL}/simulacoes/cenarios/${cenarioId}/ativar`,
  { method: "POST", headers: headers() }
);

// Espere: 200 OK — status passa para "Ativo"
const cenarioAtivo = await ativar.json();
// cenarioAtivo.status → "Ativo"
```

### Passo 1.5 — Consultar Quadro da Dívida com cenário (GET /painel/quadro-divida)

```typescript
const quadro = await fetch(
  `${BASE_URL}/painel/quadro-divida?ano=2026&cenarioId=${cenarioId}`,
  { method: "GET", headers: headers() }
);

// Espere: 200 OK
const quadroDto = await quadro.json();

// Quadro com dados reais + captações do cenário
console.log(quadroDto.cenarioAplicado.nome); // "Realista 2026"
console.log(quadroDto.projecao.meses[6].totalCaptacaoMes); // R$ 10.000.000 (mês 7)
console.log(quadroDto.alertas); // [] ou alertas de tetão
```

**Alternativa:** Use o atalho `GET /simulacoes/cenarios/{id}/quadro-divida` que infere o `anoBase` automaticamente:

```typescript
const quadroAlt = await fetch(
  `${BASE_URL}/simulacoes/cenarios/${cenarioId}/quadro-divida`,
  { method: "GET", headers: headers() }
);
```

---

## Fluxo 2 — Duplicar cenário ativo, editar rascunho e comparar com o original

**Caso de uso:** O analista quer testar o cenário "Pessimista 2026" partindo do "Realista 2026" já ativo, alterando os valores sem perder o original.

### Passo 2.1 — Duplicar cenário (POST /simulacoes/cenarios/{id}/duplicar)

```typescript
const duplicar = await fetch(
  `${BASE_URL}/simulacoes/cenarios/${cenarioIdAtivo}/duplicar`,
  { method: "POST", headers: headers(uuid()) }
);

// Espere: 201 Created
const cenarioCopia = await duplicar.json();
// cenarioCopia.nome   → "Realista 2026 (cópia)"
// cenarioCopia.status → "Rascunho"
const cenarioCopiaId = cenarioCopia.id;
```

### Passo 2.2 — Renomear o rascunho (PATCH /simulacoes/cenarios/{id})

```typescript
await fetch(`${BASE_URL}/simulacoes/cenarios/${cenarioCopiaId}`, {
  method: "PATCH",
  headers: headers(),
  body: JSON.stringify({
    nome: "Pessimista 2026",
    descricao: "Cenário conservador: apenas metade das captações previstas",
    anoBase: null, // não altera
  }),
});
// Espere: 200 OK
```

### Passo 2.3 — Obter o cenário completo para editar as simulações

```typescript
const detalhes = await fetch(
  `${BASE_URL}/simulacoes/cenarios/${cenarioCopiaId}`,
  { method: "GET", headers: headers() }
);
const cenarioDetalhado = await detalhes.json();
const simId = cenarioDetalhado.simulacoes[0].id;
```

### Passo 2.4 — Atualizar o valor da primeira simulação (PATCH)

```typescript
const sim = cenarioDetalhado.simulacoes[0];

await fetch(
  `${BASE_URL}/simulacoes/cenarios/${cenarioCopiaId}/simulacoes/${simId}`,
  {
    method: "PATCH",
    headers: headers(),
    body: JSON.stringify({
      modalidade: sim.modalidade,
      moeda: sim.moeda,
      valorPrincipal: 5000000.00, // Metade do original
      dataContratacaoPrevista: sim.dataContratacaoPrevista,
      dataPrimeiroVencimento: sim.dataPrimeiroVencimento,
      tipoTaxa: sim.tipoTaxa,
      taxaAa: sim.taxaAa,
      spreadAa: sim.spreadAa,
      baseCalculo: sim.baseCalculo,
      estruturaAmortizacao: sim.estruturaAmortizacao,
      periodicidade: sim.periodicidade,
      quantidadeParcelas: sim.quantidadeParcelas,
      anchorDiaMes: sim.anchorDiaMes,
      anchorDiaFixo: sim.anchorDiaFixo,
    }),
  }
);
// Espere: 200 OK — version da simulação incrementado
```

### Passo 2.5 — Comparar os dois cenários (POST /simulacoes/comparar)

```typescript
const comparar = await fetch(`${BASE_URL}/simulacoes/comparar`, {
  method: "POST",
  headers: headers(),
  body: JSON.stringify({
    ano: 2026,
    cenarioIds: [cenarioIdAtivo, cenarioCopiaId], // [baseline, comparado]
  }),
});

// Espere: 200 OK
const resultado = await comparar.json();

// Primeiro cenário é o baseline (ehBaseline: true)
const baseline = resultado.cenarios[0];
const pessimista = resultado.cenarios[1];

// Delta do mês 7 (julho, índice 6)
const deltaMes7 = pessimista.deltasMensais[6];
console.log(deltaMes7.saldoFimDelta);        // diferença de saldo em BRL
console.log(deltaMes7.saldoFimDeltaPercentual); // % de diferença
console.log(pessimista.deltaAnual.totalCaptacaoAnoDelta); // diferença anual de captação
```

---

## Fluxo 3 — Configurar tetão e visualizar alertas no Quadro da Dívida

**Caso de uso:** O CFO configura o limite de movimentação mensal em R$ 50 milhões. A tesouraria consulta o Quadro da Dívida com o cenário ativo e vê alertas para meses que excedem o limite.

### Passo 3.1 — Configurar o tetão (PATCH /parametros-sistema/tetao-mensal)

```typescript
// Requer policy "Admin"
await fetch(`${BASE_URL}/parametros-sistema/tetao-mensal`, {
  method: "PATCH",
  headers: headers(),
  body: JSON.stringify({ valor: 50000000.00 }),
});
// Espere: 200 OK — { "tetaoMensalCapacidadeBrl": 50000000.00 }
```

### Passo 3.2 — Verificar a configuração (GET /parametros-sistema)

```typescript
const params = await fetch(`${BASE_URL}/parametros-sistema`, {
  method: "GET",
  headers: headers(),
});
const parametros = await params.json();
// parametros.tetaoMensalCapacidadeBrl → 50000000.00
```

### Passo 3.3 — Consultar Quadro com cenário e verificar alertas

```typescript
const quadro = await fetch(
  `${BASE_URL}/painel/quadro-divida?ano=2026&cenarioId=${cenarioIdAtivo}`,
  { method: "GET", headers: headers() }
);
const quadroDto = await quadro.json();

if (quadroDto.alertas.length > 0) {
  // Renderize como banners de aviso na interface
  quadroDto.alertas.forEach((alerta: string) => {
    console.warn(alerta);
    // Exemplo: "Mês 09/2026: movimentação R$ 62.500.000,00 excede tetão configurado R$ 50.000.000,00."
  });
}
```

### Passo 3.4 — Remover o tetão (PATCH com valor null)

```typescript
await fetch(`${BASE_URL}/parametros-sistema/tetao-mensal`, {
  method: "PATCH",
  headers: headers(),
  body: JSON.stringify({ valor: null }),
});
// Espere: 200 OK — { "tetaoMensalCapacidadeBrl": null }
```

---

## Fluxo 4 — Pré-visualizar cronograma hipotético antes de salvar

**Caso de uso:** O analista preenche um formulário de nova simulação e, antes de salvar, clica em "Pré-visualizar" para ver o fluxo financeiro mês a mês. Nenhum dado é persistido.

### Passo 4.1 — Pré-visualizar cronograma (POST /simulacoes/cronograma-hipotetico)

```typescript
const preview = await fetch(`${BASE_URL}/simulacoes/cronograma-hipotetico`, {
  method: "POST",
  headers: headers(),
  // Sem Idempotency-Key (endpoint stateless — cada chamada recalcula)
  body: JSON.stringify({
    simulacao: {
      bancoId: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      modalidade: "CapitalDeGiro",
      moeda: "Brl",
      valorPrincipal: 8000000.00,
      dataContratacaoPrevista: "2026-08-15",
      dataPrimeiroVencimento: "2026-09-15",
      tipoTaxa: "Fixa",
      taxaAa: 15.50,
      spreadAa: null,
      baseCalculo: "Dias252",
      estruturaAmortizacao: "Price",
      periodicidade: "Mensal",
      quantidadeParcelas: 18,
      anchorDiaMes: "DiaContratacao",
      anchorDiaFixo: null,
    },
    cdiReferenciaAaPercentual: null, // não necessário para taxa Fixa
  }),
});

// Espere: 200 OK
const cronograma = await preview.json();

// Exibir sumário
console.log(`Taxa efetiva: ${cronograma.taxaEfetivaAaPercentual}% a.a.`);
console.log(`Total de parcelas: ${cronograma.quantidadeEventos}`);
console.log(`Principal total: R$ ${cronograma.principalTotal.toLocaleString("pt-BR", { minimumFractionDigits: 2 })}`);
console.log(`Juros total: R$ ${cronograma.jurosTotal.toLocaleString("pt-BR", { minimumFractionDigits: 2 })}`);

// Renderizar tabela de cronograma
cronograma.eventos.forEach((evento: any) => {
  console.log(
    `${String(evento.numero).padStart(3)} | ${evento.tipo.padEnd(10)} | ` +
    `${evento.data} | R$ ${evento.valor.toLocaleString("pt-BR", { minimumFractionDigits: 2 })} | ` +
    `Saldo: ${evento.saldoDevedorApos != null
      ? `R$ ${evento.saldoDevedorApos.toLocaleString("pt-BR", { minimumFractionDigits: 2 })}`
      : "—"}`
  );
});
```

### Passo 4.2 — Pré-visualizar com taxa CDI+Spread

Para operações indexadas ao CDI, informe `cdiReferenciaAaPercentual`:

```typescript
const previewCdi = await fetch(`${BASE_URL}/simulacoes/cronograma-hipotetico`, {
  method: "POST",
  headers: headers(),
  body: JSON.stringify({
    simulacao: {
      bancoId: "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      modalidade: "CapitalDeGiro",
      moeda: "Brl",
      valorPrincipal: 5000000.00,
      dataContratacaoPrevista: "2026-09-01",
      dataPrimeiroVencimento: "2026-10-01",
      tipoTaxa: "CdiSpread",
      taxaAa: null,
      spreadAa: 1.80,
      baseCalculo: "Dias252",
      estruturaAmortizacao: "Sac",
      periodicidade: "Mensal",
      quantidadeParcelas: 12,
      anchorDiaMes: "DiaContratacao",
    },
    cdiReferenciaAaPercentual: 10.75, // CDI vigente
  }),
});
```

---

## Exemplos curl equivalentes

### Criar cenário

```bash
curl -X POST "https://api.sgcf.example/api/v1/simulacoes/cenarios" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{"nome":"Realista 2026","anoBase":2026}'
```

### Consultar Quadro da Dívida com cenário

```bash
curl "https://api.sgcf.example/api/v1/painel/quadro-divida?ano=2026&cenarioId=7b3e4f2a-1c5d-4e8b-9a0f-2c6d8e1f3b5a" \
  -H "Authorization: Bearer $TOKEN"
```

### Configurar tetão

```bash
curl -X PATCH "https://api.sgcf.example/api/v1/parametros-sistema/tetao-mensal" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"valor":50000000.00}'
```

### Comparar cenários

```bash
curl -X POST "https://api.sgcf.example/api/v1/simulacoes/comparar" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "ano": 2026,
    "cenarioIds": [
      "7b3e4f2a-1c5d-4e8b-9a0f-2c6d8e1f3b5a",
      "9c4d5e3b-2f6e-5f9c-0b1g-3d7e9f2g4c6b"
    ]
  }'
```

---

## Tratamento de erros recomendado

### Mapeamento de status codes

```typescript
async function chamarApi(url: string, options: RequestInit) {
  const response = await fetch(url, options);

  switch (response.status) {
    case 200:
    case 201:
    case 204:
      return response;

    case 400: {
      const body = await response.json();
      // body.error OU body.detail com mensagem descritiva
      throw new Error(`Validação: ${body.error || body.detail}`);
    }

    case 401:
      throw new Error("Sessão expirada. Faça login novamente.");

    case 403:
      throw new Error("Sem permissão para esta operação.");

    case 404:
      throw new Error("Recurso não encontrado.");

    case 409: {
      const body = await response.json();
      // Conflito de estado ou violação de invariante
      throw new Error(`Conflito: ${body.error || body.detail}`);
    }

    default:
      throw new Error(`Erro inesperado: ${response.status}`);
  }
}
```

### Erros de idempotência (400)

Se o front-end enviar uma `Idempotency-Key` com formato inválido, a resposta será:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Idempotency-Key inválida.",
  "status": 400,
  "detail": "O header Idempotency-Key deve ser um UUID v4 (...) ou uma string alfanumérica de 1 a 64 caracteres (...)."
}
```

Use sempre `crypto.randomUUID()` para gerar a key e evite este erro.

### Erros de invariante de domínio (400 / 409)

Erros 400 geralmente indicam campos inválidos. Exemplos de mensagens retornadas no campo `error`:

- `"ValorPrincipal (principal) deve ser maior que zero."`
- `"DataPrimeiroVencimento (vencimento) deve ser posterior à DataContratacaoPrevista."`
- `"TipoTaxa Fixa exige que TaxaAa (taxa) seja informada."`
- `"Modalidade Finimp (modalidade) não aceita operações em BRL."`

Erros 409 geralmente indicam violação do lifecycle ou incompatibilidade. Exemplos:

- `"Operação 'AtualizarSimulacao' não é permitida em cenário Arquivado."`
- `"AnoBase não pode ser alterado em cenário com status Ativo."`
- `"Cenário já está Ativo."`
- `"Todos os cenários devem ter o mesmo AnoBase para comparação."`
