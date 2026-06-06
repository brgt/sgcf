# TODO — S40 Cotação: Tenor, Campos de Domínio, PTAX Multimoeda e Erros RFC 7807

> Plano: `tasks/s40-cotacao-tenor-dominio/plan.md` · Spec: `docs/specs/cotacoes/SPEC_S40_TENOR_DOMINIO_PTAX.md` (v1.0) · Handover: `docs/api/S40_FE_HANDOVER.md`
> Marcar `[x]` ao concluir. Não avançar de fase sem o checkpoint verde.
> Sizing: **XS** = 1 arquivo · **S** = 1–2 · **M** = 3–5 · **L** = 5–8

---

## Fase 0 — Fundação (domínio + persistência)

- [ ] **T1 [S]** Enums e VO de domínio
  - **Arquivos:** `Sgcf.Domain/Cotacoes/UnidadePrazo.cs`, `TipoIndexador.cs`, `IndexadorBase.cs`
  - **Aceite:**
    - `UnidadePrazo { Dias = 1, Meses = 2 }`
    - `TipoIndexador { CdiPercentual, CdiMaisSpread, Prefixado, Tlp, Ipca, Selic, Sofr, Euribor }`
    - `IndexadorBase` (record) com `Tipo?`, `PercentualCdi?`, `SpreadAa?`, `TaxaPrefixadaAa?`; método/propriedade de coerência tipo↔campo (`EhCoerente`) puro
  - **Verify:** `dotnet test --filter "FullyQualifiedName~IndexadorBase"` (coerência) verde

- [ ] **T2 [M]** Entidade `Cotacao` — tenor, campos de domínio e invariantes
  - **Arquivos:** `Sgcf.Domain/Cotacoes/Cotacao.cs` (+ ajuste de call-site no handler e testes de domínio)
  - **Aceite:**
    - Novas propriedades (setter privado): `PrazoMaximoValor`, `PrazoMaximoUnidade`, `MoedaAlvo`, `CarenciaMeses`, `IndexadorBase`, `FinalidadeBndes`, `BancoRepassadorPretendido`, `PercentualCoberturaFgi`, `PtaxUsada`
    - Derivação pura: `Meses → Dias = valor × 30`; `Dias → Dias = valor`
    - `Criar` recebe par de tenor + `DadosDominioCotacao` (AD-01); deriva e persiste os três campos de prazo
    - Invariantes: `moedaAlvo` forçada `Brl` em Nce/CapitalDeGiro/Fgi; cambiais exigem `moedaAlvo ≠ Brl` + `ptaxUsada`; `carenciaMeses` ignorada fora das aplicáveis; `percentualCoberturaFgi` apenas Fgi
    - `EditarCamposBasicos` aceita tenor e campos editáveis, mantendo restrição de `Rascunho`
    - PTAX invariante migra de `ptaxUsadaUsdBrl` para `ptaxUsada` + `moedaAlvo`
  - **Verify:** `dotnet test --filter "FullyQualifiedName~Cotacao&Category!=Slow"` cobre derivação (60 Meses→1800; 180 Dias→180), defaults por modalidade, invariantes e validações duras

- [ ] **T3 [M]** Persistência EF + migração
  - **Arquivos:** `Sgcf.Infrastructure/Persistence/Configurations/CotacaoConfiguration.cs`, `Migrations/<ts>_S40_CotacaoTenorEDominio.cs`
  - **Aceite:**
    - Mapear: `prazo_maximo_valor int`, `prazo_maximo_unidade text`, `moeda_alvo text`, `carencia_meses int`, `indexador_tipo/percentual_cdi/spread_aa/taxa_prefixada_aa`, `finalidade_bndes text`, `banco_repassador_pretendido text`, `percentual_cobertura_fgi numeric`, `ptax_usada numeric(12,6)`
    - `UnidadePrazo`/`Moeda`/`TipoIndexador` via converters de string (padrão `SgcfConverters`)
    - Migração aditiva: colunas nullable → backfill (`unidade='Dias'`, `valor=prazo_maximo_dias`, `ptax_usada=ptax_usada_usd_brl`) → constraints (`unidade IN ('Dias','Meses')`, `valor>=1`, `carencia>=0`, `cobertura 0..100`) → `NOT NULL` no tenor com default `'Dias'`
    - `Down` reverte de forma limpa
  - **Verify:** `dotnet ef migrations add` sem _diff_ inesperado no snapshot; `dotnet ef database update` aplica em base com linhas; coluna confirmada

- [ ] **[CHECKPOINT A]** `dotnet build` OK; Domain/Application/Integration verdes; migração aplicada; backfill validado; zero mudança de comportamento observável na API

---

## Fase 1 — Tenor (vertical, core)

- [ ] **T4 [M]** Application: precedência de tenor + DTO + alertas mínimos
  - **Arquivos:** `Sgcf.Application/Cotacoes/Services/ResolvedorTenor.cs`, `AlertaDto.cs`, `CotacaoDto.cs`, `Commands/CriarCotacaoCommand.cs`, `Commands/AtualizarCotacaoCommand.cs`
  - **Aceite:**
    - `ResolvedorTenor` puro: aplica precedência §4.1 (valor+unidade > dias legado; default por modalidade; recálculo em inconsistência)
    - Commands recebem `prazoMaximoValor?`, `prazoMaximoUnidade?` (mantendo `prazoMaximoDias?` legado)
    - Validators: `valor<1`/não inteiro/unidade inválida → 400; POST sem prazo → 400; PATCH sem prazo não altera
    - `CotacaoDto` expõe `prazoMaximoDias`, `prazoMaximoValor`, `prazoMaximoUnidade`, `alertas[]`
    - Alerta `prazo-recalculado` emitido em inconsistência valor↔dias
  - **Verify:** `dotnet test --filter "FullyQualifiedName~Tenor|FullyQualifiedName~CriarCotacao|FullyQualifiedName~AtualizarCotacao"` verde

- [ ] **T5 [S]** Integração HTTP — tenor
  - **Arquivos:** `tests/Sgcf.Api.IntegrationTests/...CotacaoTenor...`
  - **Aceite:** critérios de tenor da Spec §11 (60 Meses→1800; 180 Dias→180; default Lei4131=Meses; legado só `prazoMaximoDias`; 400s; PATCH 24 Meses→720; recálculo)
  - **Verify:** `dotnet test tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~Tenor"`

- [ ] **[CHECKPOINT B]** POST/PATCH/GET tratam tenor; caminho legado intacto; suíte alvo verde

---

## Fase 2 — Erros RFC 7807 (breaking change)

- [ ] **T6 [M]** Exceções tipadas + handler central
  - **Arquivos:** `PtaxIndisponivelException.cs`, `ConflitoDeEstadoException.cs`, `Sgcf.Api/Middleware/GlobalExceptionHandler.cs`
  - **Aceite:**
    - Catálogo §5.2 com base `https://sgcf.nordware.io/errors/` (`ptax-indisponivel`, `conflito-de-estado`, `validacao`, `nao-encontrado`)
    - `PtaxIndisponivelException` expõe extensões `dataPtaxReferencia`, `moedaAlvo`
    - Handler testa subtipo PTAX antes do `InvalidOperationException` genérico
  - **Verify:** teste unitário do handler mapeia cada tipo; `dotnet test --filter "FullyQualifiedName~GlobalExceptionHandler"`

- [ ] **T7 [M]** Remover catches que sombreiam o handler
  - **Arquivos:** `Sgcf.Api/Controllers/CotacoesController.cs`
  - **Aceite:**
    - Remover `catch (InvalidOperationException) → Conflict(new { error })` (≈20 ocorrências)
    - Transições inválidas do domínio sobem como `ConflitoDeEstadoException` → 409 ProblemDetails
    - Nenhum endpoint de cotações retorna `{ error }`
  - **Verify:** `dotnet test tests/Sgcf.Api.IntegrationTests --filter "FullyQualifiedName~Cotacao"`; conferir corpo ProblemDetails nos 409

- [ ] **[CHECKPOINT C]** Breaking change validada; todos os 409 de cotações em ProblemDetails; varredura confirmando ausência de `{ error }`

---

## Fase 3 — PTAX multimoeda

- [ ] **T8 [M]** `moedaAlvo` end-to-end + generalização da PTAX
  - **Arquivos:** `Commands/CriarCotacaoCommand.cs` (handler), `Commands/RefreshCotacaoMercadoCommand.cs`, `CotacaoDto.cs`, testes
  - **Aceite:**
    - Handler chama `ResolverFxAsync(moedaAlvo, TipoCotacao.PtaxD1, dataAbertura, ct)`
    - Indisponibilidade lança `PtaxIndisponivelException(moedaAlvo, dataReferencia)`
    - Refinimp herda `moedaAlvo` de `mae.Moeda` (read-only; divergência → alerta `moeda-herdada-do-contrato-mae`)
    - `ptaxUsada` canônico; `ptaxUsadaUsdBrl` espelhado só para USD; refresh atualiza ambos coerentemente
    - Nce/CapitalDeGiro/Fgi: `moedaAlvo=Brl`, sem PTAX
  - **Verify:** `dotnet test --filter "FullyQualifiedName~Ptax|FullyQualifiedName~MoedaAlvo"`; seed de PTAX EUR/BRL na integração

- [ ] **[CHECKPOINT D]** Lei4131 `Eur` resolve EUR/BRL; `ptaxUsada` preenchido, `ptaxUsadaUsdBrl=null`; 409 PTAX traz `type` + `dataPtaxReferencia` + `moedaAlvo`

---

## Fase 4 — Campos de domínio

- [ ] **T9 [M]** Carência + indexador
  - **Arquivos:** `Commands/*` (campos), `Services/GeradorAlertasCotacao.cs`, `CotacaoDto.cs`, testes
  - **Aceite:**
    - `carenciaMeses`: `<0` → 400; modalidade não aplicável → ignora + alerta `carencia-ignorada`
    - `indexadorBase`: serializa/desserializa; coerência tipo↔campo → alerta `indexador-incoerente` (não bloqueia)
  - **Verify:** `dotnet test --filter "FullyQualifiedName~Carencia|FullyQualifiedName~Indexador"`

- [ ] **T10 [M]** Estruturantes FGI
  - **Arquivos:** `Commands/*` (campos), validators, `CotacaoDto.cs`, testes
  - **Aceite:**
    - `percentualCoberturaFgi` faixa `0..100` (fora → 400); apenas Fgi
    - `finalidadeBndes`/`bancoRepassadorPretendido` string livre + validação suave
    - Coexistência com `FgiInputs.PercentualCoberto` (conversão) preservada
  - **Verify:** `dotnet test --filter "FullyQualifiedName~Fgi"`

- [ ] **[CHECKPOINT E]** Campos de domínio persistem/validam/retornam; alertas suaves corretos

---

## Fase 5 — Consolidação e release

- [ ] **T11 [S]** Faixas de prazo + consolidação de alertas
  - **Arquivos:** `Services/GeradorAlertasCotacao.cs`, testes
  - **Aceite:** alerta `prazo-fora-da-faixa-esperada` por faixas provisórias §4.4; auditar todos os `codigo` de alerta num só lugar
  - **Verify:** `dotnet test --filter "FullyQualifiedName~Alerta"`

- [ ] **T12 [S]** Versão + handover + suíte completa
  - **Arquivos:** configuração OpenAPI/Swagger (versão `0.12.0`), `docs/api/S40_FE_HANDOVER.md` (sincronizar exemplos finais), changelog
  - **Aceite:** contrato `0.12.0`; changelog §13.1 publicado; handover reflete contratos finais
  - **Verify:** `dotnet test` (suíte completa) verde

- [ ] **[CHECKPOINT F — go/no-go]** Suíte completa verde; todos os critérios de aceite §11 cobertos; breaking change comunicada ao FE; follow-up de produção (RLS no backfill) registrado

---

## Pendências de produto (não bloqueiam S40)

- [ ] Listas oficiais FGI (bancos repassadores) e BNDES (`finalidadeBndes`) → promover a enum.
- [ ] Tetos rígidos de prazo por modalidade (se aplicável).
- [ ] Migração do `type` de garantia `sgcf.io` → `sgcf.nordware.io` (coordenar com FE).
- [ ] Follow-up de produção: papel de conexão das migrações vs. `FORCE ROW LEVEL SECURITY` no backfill.
