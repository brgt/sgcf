# SPEC — Garantias Alternativas (Grupos "OU") no SGCF

**Empresa:** Proxys Comércio Eletrônico
**Sponsor / Product Owner:** Welysson Soares
**Versão:** v0.1 — 02/junho/2026
**Status:** Proposta — aguardando aprovação para entrar em vigor
**Escopo:** Feature incremental sobre o backend SGCF (.NET) — módulo de Garantias Exigidas
**Documento âncora:** `SPEC.md` (mestre). Este documento detalha apenas a feature de garantias alternativas e referencia o modelo existente.

> **Contexto de origem.** Bancos liberam linhas de crédito (ex.: FINIMP) condicionando a garantia a **alternativas mutuamente substituíveis**. Exemplo real: o Santander liberou FINIMP exigindo **depósito em CDB (cash collateral) OU recebíveis via boletos bancários**. O mutuário satisfaz a exigência cumprindo **uma** das alternativas (ou uma combinação delas), não todas.

---

## 0. Diagnóstico do estado atual (por que esta feature é necessária)

O modelo atual só representa duas situações por item de garantia exigida:

| Estado atual | Campo | Comportamento no enforcement |
|---|---|---|
| Obrigatória | `Obrigatoria = true` | **Cada** item deve ser coberto pelo seu tipo (lógica **AND**) — `AvaliarCobertura` em `ConverterEmContratoCommand.cs:456` |
| Opcional | `Obrigatoria = false` | **Ignorada** — filtrada em `ConverterEmContratoCommand.cs:219`; sem qualquer validação |

`CalculadorValorGarantiaExigida.Calcular` (`CalculadorValorGarantiaExigida.cs:46`) **soma** todos os itens. Não existe noção de "grupo de alternativas".

**Consequência:** o caso Santander (CDB OU Recebíveis) não é modelável:

- Ambas `Obrigatoria=true` → o sistema exige **as duas** (ex.: 200%). ❌
- Ambas `Obrigatoria=false` → o sistema **não exige nenhuma** (sem cobertura mínima). ❌
- Não há terceiro estado: "satisfaça uma destas". ❌

---

## 1. Objetivo

### 1.1 O que estamos construindo

Adicionar ao módulo de Garantias Exigidas o conceito de **Grupo de Alternativas** (satisfação "OU"): um conjunto de itens de garantia, de tipos distintos, em que a exigência do banco é considerada **cumprida** quando o contrato cobre a cota do grupo por **uma** das alternativas **ou pela combinação** delas.

### 1.2 Por que

Permitir o cadastro fiel das políticas de garantia praticadas pelos bancos e que o *enforcement* de conversão em contrato, o cálculo de valor exigido em cotações e os indicadores do painel reflitam corretamente a regra "uma OU outra". Hoje a planilha/operador trata isso informalmente, fora do sistema de registro.

### 1.3 Sucesso (feature-level)

A feature é bem-sucedida quando:

1. É possível cadastrar, em uma revisão de garantias de um `LimiteBanco`, um grupo "CDB **OU** Recebíveis" e o sistema o persiste como grupo.
2. A conversão em contrato é **liberada** quando o contrato cobre a cota do grupo por qualquer alternativa (ou combinação) e **bloqueada** quando nenhuma cobre.
3. A API expõe, por contrato e por limite, **qual tipo de garantia** está em uso e **a qual grupo de alternativas** o item pertence.
4. Dados e políticas pré-existentes continuam funcionando sem alteração (compatibilidade retroativa total).
5. Cobertura de testes do enforcement de grupos ≥ 95%; golden dataset estendido com casos de grupo.

### 1.4 Usuários-alvo

Tesouraria (cadastro de políticas de limite e conversão de cotações), Gerente Financeiro (revisão de políticas) e a camada de agentes/Painel (consumo via API). Ver `SPEC.md` §2.

---

## 2. Decisões de produto (confirmadas) e pendências

### 2.1 Confirmadas (sessão de 02/jun/2026)

| # | Decisão | Escolha |
|---|---|---|
| D1 | Cobertura do grupo | **Combinável (soma)** — alternativas do mesmo grupo somam cobertura |
| D2 | Valor exigido | **Por alternativa** — cada item mantém seu `PercentualSobreLimite`/`ValorFixoBrl` |
| D3 | Obrigatoriedade | **Grupo sempre obrigatório** — satisfazer o grupo é condição de liberação |
| D4 | Escopo desta entrega | **Completo** — enforcement + cálculo em cotação + exposição API/painel |

### 2.2 Pendência que requer validação de negócio (BLOQUEANTE para implementação)

> **RV-GA — Regra de combinação quando as alternativas têm valores exigidos diferentes.**
>
> D1 (combinável) + D2 (valor por alternativa) criam uma ambiguidade: se CDB exige R$ 100k e Recebíveis exige R$ 120k, e ambas podem ser combinadas, **qual é o alvo do grupo**?
>
> **Regra proposta (default): normalização por fração.** Cada garantia declarada no contrato conta como uma fração do alvo da **sua própria** alternativa. O grupo está coberto quando a **soma das frações ≥ 1,0**.
>
> Exemplo: CDB exige R$ 100k, Recebíveis exige R$ 120k. Contrato traz R$ 60k de CDB (60k/100k = 0,60) + R$ 48k de Recebíveis (48k/120k = 0,40). Soma = 1,00 → **coberto**.
>
> **Alternativa B (mais simples):** alvo do grupo = **menor** valor exigido entre as alternativas (a forma mais barata de satisfazer o banco); cobertura = soma dos valores BRL das garantias dos tipos do grupo ≥ alvo.
>
> **Ação:** confirmar RV-GA antes de iniciar a implementação. O restante deste spec assume a regra de fração; uma mudança aqui altera apenas o algoritmo de `AvaliarCoberturaGrupo` e os casos de teste correspondentes.

---

## 3. Requisitos funcionais

### 3.1 Cadastro (revisão de garantias do `LimiteBanco`)

- **RF-01** Um `GarantiaExigidaItem` pode ser associado a um **grupo de alternativas** por meio de um identificador de grupo (`GrupoAlternativaId`, nullable).
- **RF-02** Itens **sem** grupo (`GrupoAlternativaId = null`) preservam o comportamento atual (obrigatória → AND; opcional → ignorada).
- **RF-03** Um grupo de alternativas é composto por **2 ou mais** itens, de **tipos distintos** (mantém SR-06: sem tipo duplicado na revisão).
- **RF-04** Todo grupo de alternativas é de cumprimento **obrigatório** (D3). O flag `Obrigatoria` por item **não se aplica** a itens agrupados; quando `GrupoAlternativaId != null`, `Obrigatoria` é ignorado e normalizado para um valor canônico na escrita.
- **RF-05** Cada item agrupado mantém seu próprio `PercentualSobreLimite` **XOR** `ValorFixoBrl` (D2; regra AD-4 atual preservada).
- **RF-06** Um grupo pode receber rótulo opcional (`GrupoRotulo`, ex.: "Colateral mínimo FINIMP") para exibição.

### 3.2 Operacionalização (enforcement e cálculo)

- **RF-07** Na conversão em contrato, para cada **grupo de alternativas** vigente, o sistema avalia a cobertura combinada segundo **RV-GA**. Gera uma única `LacunaGarantia` por grupo não coberto (identificando o grupo, não um tipo isolado).
- **RF-08** Itens obrigatórios **sem** grupo continuam avaliados individualmente (comportamento atual inalterado).
- **RF-09** `CalculadorValorGarantiaExigida` passa a tratar grupos: a contribuição de um grupo ao "valor total exigido" é o alvo do grupo (ver RV-GA), **não** a soma das alternativas. Itens sem grupo continuam somando como hoje.
- **RF-10** A mensagem de erro `GarantiaExigidaNaoCobertaException` distingue lacuna de **item** (atual) de lacuna de **grupo** (nova), listando as alternativas aceitas no grupo.

### 3.3 Exposição na API

- **RF-11** `GarantiaExigidaItemDto` passa a expor `GrupoAlternativaId` e `GrupoRotulo` (nullable).
- **RF-12** `GET /limites-banco/{id}/revisoes-garantias` retorna os itens com seus agrupamentos.
- **RF-13** O snapshot por contrato (S34, `GarantiaExigidaSnapshotItemDto`) passa a preservar `GrupoAlternativaId`/`GrupoRotulo` no momento da conversão.
- **RF-14** `GET /contratos/{id}/garantias/indicadores` (`IndicadoresGarantiaDto`) reflete a cobertura considerando grupos OU (não dupla contagem do alvo).
- **RF-15** Nenhum endpoint existente muda de rota ou de contrato de forma incompatível; os novos campos são **aditivos e opcionais**.

---

## 4. Modelo de domínio e dados

### 4.1 Alterações de entidade (`Sgcf.Domain`)

`GarantiaExigidaItem` (`src/Sgcf.Domain/Cotacoes/GarantiaExigidaItem.cs`):

- Novo: `Guid? GrupoAlternativaId { get; private set; }`
- Novo: `string? GrupoRotulo { get; private set; }`
- `Criar`/`Atualizar` recebem os dois campos; quando `GrupoAlternativaId != null`, normalizam `Obrigatoria` (RF-04).

`GarantiaExigidaItemSpec` (`src/Sgcf.Domain/Cotacoes/GarantiaExigidaItemSpec.cs`): acrescenta `GrupoAlternativaId` e `GrupoRotulo`.

`GarantiaExigidaRevisao` (`src/Sgcf.Domain/Cotacoes/GarantiaExigidaRevisao.cs`): nova validação de agregado (ver invariantes GA-xx) garantindo consistência dos grupos.

### 4.2 Invariantes novas (prefixo GA)

| Código | Invariante |
|---|---|
| **GA-01** | `GrupoAlternativaId` é nullable; null = item independente (comportamento legado). |
| **GA-02** | Um grupo (mesmo `GrupoAlternativaId`) contém **≥ 2** itens. Grupo com 1 item é inválido. |
| **GA-03** | Dentro de um grupo, todos os itens têm **tipos distintos** (consequência de SR-06 no nível da revisão). |
| **GA-04** | Itens agrupados são sempre tratados como obrigatórios no enforcement (D3); `Obrigatoria` é normalizado. |
| **GA-05** | `GrupoRotulo` ≤ 120 caracteres; consistente entre os itens do mesmo grupo (último valor escrito vence, validado no agregado). |
| **GA-06** | Imutabilidade após `VigenciaFim` (SR-05) aplica-se também aos campos de grupo. |
| **GA-07** | Um `Tipo` pertence a **no máximo um** grupo dentro da mesma revisão. |

### 4.3 Persistência (`Sgcf.Infrastructure`)

- **Migration aditiva** (PostgreSQL): adicionar colunas `grupo_alternativa_id uuid NULL` e `grupo_rotulo varchar(120) NULL` em `sgcf.garantia_exigida_item`.
- Índice parcial `(revisao_id, grupo_alternativa_id) WHERE grupo_alternativa_id IS NOT NULL` para a avaliação de cobertura por grupo.
- `GarantiaExigidaItemConfiguration` atualizado (mapeamento das novas colunas).
- **Migration de snapshot S34**: adicionar as mesmas colunas à tabela de snapshot de garantias do contrato.
- **Compatibilidade:** todas as linhas existentes recebem `NULL` → comportamento idêntico ao atual. Nenhum backfill obrigatório.

### 4.4 Algoritmo de cobertura de grupo (regra de fração — RV-GA default)

```
para cada grupo G (itens com mesmo GrupoAlternativaId):
    fracaoTotal = 0
    para cada alternativa A em G:
        alvoA      = valorExigido(A, valorPrincipalBrl)   // % ou valor fixo
        cobertoA   = soma das garantias do contrato cujo Tipo == A.Tipo  (em BRL)
        fracaoTotal += min(cobertoA / alvoA, 1.0)          // uma alternativa não "transborda" para outra
    se fracaoTotal < 1.0:
        registrar LacunaGarantia(grupo = G, alternativasAceitas = tipos(G), fracaoCoberta = fracaoTotal)
```

> Nota: o `min(.,1.0)` por alternativa evita que excesso de uma cubra outra de forma não pretendida; a soma das frações ≥ 1,0 é o critério de cobertura combinada (D1). Se RV-GA mudar para a Alternativa B, substitui-se este bloco pelo cálculo de alvo único.

---

## 5. Comandos e operação (build / test / run)

Mesmo fluxo do `CLAUDE.md` do backend:

```bash
# Subir dependências (Postgres + Redis)
docker compose -f sgcf-backend/infra/dev/docker-compose.yml up -d

# Aplicar a nova migration
dotnet ef database update --project sgcf-backend/src/Sgcf.Infrastructure --startup-project sgcf-backend/src/Sgcf.Api

# Gerar a migration (durante o desenvolvimento)
dotnet ef migrations add S36_GarantiasAlternativas --project sgcf-backend/src/Sgcf.Infrastructure --startup-project sgcf-backend/src/Sgcf.Api

# Feedback rápido
dotnet test --filter "Category!=Slow"

# Suíte completa + golden dataset
dotnet test
dotnet test sgcf-backend/tests/Sgcf.GoldenDataset/Sgcf.GoldenDataset.csproj
```

---

## 6. Estrutura do projeto (arquivos tocados)

| Camada | Arquivo | Mudança |
|---|---|---|
| Domain | `Cotacoes/GarantiaExigidaItem.cs` | +`GrupoAlternativaId`, +`GrupoRotulo`, normalização de `Obrigatoria` |
| Domain | `Cotacoes/GarantiaExigidaItemSpec.cs` | +campos de grupo |
| Domain | `Cotacoes/GarantiaExigidaRevisao.cs` | validação GA-02/03/05/07 |
| Application | `Cotacoes/CalculadorValorGarantiaExigida.cs` | tratar alvo de grupo (RF-09) |
| Application | `Cotacoes/Commands/ConverterEmContratoCommand.cs` | `AvaliarCobertura` → cobertura por grupo (RF-07/08/10) |
| Application | `Cotacoes/Exceptions/GarantiaExigidaNaoCobertaException.cs` | lacuna de grupo |
| Application | `Cotacoes/GarantiaExigidaItemDto.cs` | +campos de grupo |
| Application | `Contratos/GarantiaExigidaSnapshotItemDto.cs` | +campos de grupo |
| Application | `Cotacoes/CriarGarantiaExigidaItemRequest.cs` | +campos de grupo |
| Infrastructure | `Persistence/Configurations/GarantiaExigidaItemConfiguration.cs` | mapear colunas |
| Infrastructure | `Persistence/Migrations/*` | migration aditiva + snapshot S34 |
| Api | `Controllers/LimitesBancoController.cs` | passar campos de grupo no PATCH de garantias |
| Api | (docs) `docs/api/limites-banco.md` | documentar campos novos |

> Nenhum arquivo em `Sgcf.Mcp`/`Sgcf.A2a` deve importar `Sgcf.Infrastructure` (regra de camadas do `CLAUDE.md`).

---

## 7. Estilo de código (herdado do projeto)

- Dinheiro: sempre `Money` (nunca `decimal` cru). Nomes de domínio em português.
- Datas: NodaTime + `IClock` injetado; nunca `DateTime.Now`/`UtcNow` em domínio/aplicação.
- Arredondamento: `MidpointRounding.AwayFromZero` (já encapsulado em `Money`).
- Cálculos financeiros: funções **puras**, sem I/O.
- EF Core e migrations: somente em `Sgcf.Infrastructure`; value objects como owned entities, setters privados.

## 8. Estratégia de testes

- **Unit (Domain):** GA-01..GA-07 (criação/validação de grupos; imutabilidade após encerramento).
- **Unit (Application):** `CalculadorValorGarantiaExigida` com grupos (RF-09) e `AvaliarCoberturaGrupo` (algoritmo §4.4), incluindo a tabela de cenários abaixo.
- **Integração (Testcontainers, `Category=Slow`):** conversão em contrato com banco `sgcf_garantias_alt_e2e`, cobrindo liberar/bloquear.
- **Golden dataset:** novos casos JSON para grupo "CDB OU Recebíveis" (cobertura por uma, por combinação, e não-cobertura).
- **Migration test:** linhas legadas (`grupo_alternativa_id = NULL`) preservam comportamento; round-trip do snapshot S34.

### 8.1 Critérios de aceite (cenários canônicos — assume RV-GA de fração)

Grupo "CDB (100% do principal) OU Recebíveis (120% do principal)", principal = R$ 100k:

| # | Garantias do contrato | Frações | Resultado |
|---|---|---|---|
| AC-1 | CDB R$ 100k | 1,00 | **Liberado** |
| AC-2 | Recebíveis R$ 120k | 1,00 | **Liberado** |
| AC-3 | CDB R$ 60k + Recebíveis R$ 48k | 0,60 + 0,40 = 1,00 | **Liberado** |
| AC-4 | CDB R$ 50k + Recebíveis R$ 48k | 0,50 + 0,40 = 0,90 | **Bloqueado** (lacuna de grupo, 0,90) |
| AC-5 | CDB R$ 100k + Aval (item obrigatório **sem** grupo, exigido) | grupo 1,00; Aval ausente | **Bloqueado** (lacuna do item Aval) |
| AC-6 | Nenhuma garantia | 0,00 | **Bloqueado** |
| AC-7 | Política legada (itens sem grupo) | — | comportamento atual inalterado |

---

## 9. Boundaries

**Sempre fazer**
- Manter compatibilidade retroativa: itens sem grupo = comportamento atual; novos campos aditivos e nullable.
- Migration **aditiva** e reversível; sem perda de dados; testar com base existente.
- Respeitar invariantes existentes (SR-01..SR-08, SC-01..SC-07, AD-4) e as novas GA-01..GA-07.
- Estender o golden dataset com casos de grupo (sem alterar expected outputs existentes sem sign-off).

**Perguntar antes**
- **Confirmar RV-GA** (regra de combinação) antes de codar o algoritmo — é a única decisão bloqueante em aberto.
- Qualquer mudança em rota/contrato de endpoint existente (preferir aditivo).
- Alterar invariante existente (ex.: relaxar SR-06) — exige justificativa e sign-off.

**Nunca fazer**
- Sobrescrever o `SPEC.md` mestre.
- Introduzir `decimal` cru para dinheiro ou `DateTime.Now` em domínio/aplicação.
- Importar `Sgcf.Infrastructure` em `Sgcf.Mcp`/`Sgcf.A2a`.
- Alterar expected outputs do golden dataset sem aprovação de negócio.
- Fazer backfill destrutivo de políticas históricas.

---

## 10. Fora de escopo (nesta entrega)

- UI/front-end para montagem visual de grupos (apenas contrato de API é entregue).
- Grupos aninhados ou expressões lógicas complexas (ex.: "(A OU B) E C") — apenas grupos planos "OU".
- Reprecificação automática de cotações já emitidas com base na nova regra.
- Migração/normalização de políticas legadas para grupos (decisão de negócio futura).

---

## 11. Referências

- `SPEC.md` (mestre) §3.3, §3.4, §4.1 — modelo de revisões e garantias exigidas.
- `sgcf-backend/CLAUDE.md` — regras não-negociáveis (Money, datas, camadas, testes).
- Código atual: `GarantiaExigidaItem.cs`, `GarantiaExigidaRevisao.cs`, `CalculadorValorGarantiaExigida.cs`, `ConverterEmContratoCommand.cs` (`AvaliarCobertura`), `GarantiaDto.cs`, `GarantiaExigidaItemDto.cs`.
