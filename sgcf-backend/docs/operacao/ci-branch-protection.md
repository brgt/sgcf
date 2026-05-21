# Branch Protection — Procedimento de Configuração

**Audiência:** Administradores do repositório GitHub.
**Frequência:** Configurar uma vez ao habilitar a multi-tenancy; revisar a cada nova suite de testes crítica.

---

## 1. Objetivo

Garantir que nenhuma mudança em `main` possa ser mesclada sem:

1. Suite de testes cross-tenant verde (impede vazamento LGPD).
2. Build + testes regulares verdes.
3. Aprovação por revisor humano.
4. Revisão obrigatória por `security-team` em paths críticos (via `CODEOWNERS`).

---

## 2. Pré-requisitos

- Permissão `admin` no repositório GitHub.
- Workflow `ci-cross-tenant.yml` já mergeado em `main` ao menos uma vez (para o GitHub reconhecer o nome do status check).
- `CODEOWNERS` mergeado em `main`.

---

## 3. Configuração via interface web

1. Acessar `https://github.com/<org>/<repo>/settings/branches`.
2. Clicar em **Add branch protection rule**.
3. **Branch name pattern:** `main`.
4. Marcar opções:

### Require a pull request before merging

- ✅ **Require a pull request before merging**
  - ✅ **Require approvals:** `1`
  - ✅ **Dismiss stale pull request approvals when new commits are pushed**
  - ✅ **Require review from Code Owners**

### Require status checks to pass before merging

- ✅ **Require status checks to pass before merging**
  - ✅ **Require branches to be up to date before merging**
  - **Required status checks** (digite e selecione):
    - `Cross-Tenant Isolation Tests`
    - Outros que existirem (build, unit-tests, etc.)

### Outras opções

- ✅ **Require conversation resolution before merging**
- ✅ **Require signed commits** (recomendado, opcional)
- ✅ **Require linear history** (recomendado)
- ✅ **Do not allow bypassing the above settings**
- ✅ **Restrict who can push to matching branches:**
  - Adicionar: lista de leads e CI bots.

5. **Save changes**.

---

## 4. Configuração via `gh` CLI

Para automação ou auditoria, mesma config aplicada via API:

```bash
# Pré-requisito: gh CLI autenticado com permissão admin
gh auth status

# Aplicar branch protection em main
gh api -X PUT \
  -H "Accept: application/vnd.github+json" \
  /repos/<org>/<repo>/branches/main/protection \
  -F required_status_checks[strict]=true \
  -F required_status_checks[contexts][]='Cross-Tenant Isolation Tests' \
  -F enforce_admins=true \
  -F required_pull_request_reviews[required_approving_review_count]=1 \
  -F required_pull_request_reviews[dismiss_stale_reviews]=true \
  -F required_pull_request_reviews[require_code_owner_reviews]=true \
  -F required_conversation_resolution=true \
  -F allow_force_pushes=false \
  -F allow_deletions=false \
  -F restrictions=null
```

Trocar `<org>` e `<repo>` pelos valores reais. Resposta `200 OK` indica sucesso.

---

## 5. Validação

Após configurar:

1. Criar PR de teste tocando algum arquivo em `/sgcf-backend/`.
2. Verificar que:
   - Aba `Checks` mostra "Cross-Tenant Isolation Tests" como **required**.
   - Botão "Merge" fica desabilitado até checks verdes.
   - Sem aprovação, botão "Merge" fica desabilitado.
   - Mudança em `/src/Sgcf.Domain/Tenancy/` exige revisor de `security-team`.

3. Forçar falha temporária na suite (ex.: introduzir teste falhante em branch local) e confirmar que merge é bloqueado.

---

## 6. Exceções e bypass

**Política:** sem exceções para `main`. `enforce_admins=true` garante que mesmo admins do repositório precisam passar pelos checks.

Em caso de incidente de produção que exige hotfix urgente:

1. Criar branch `hotfix/<descrição>`.
2. PR normal, com suite cross-tenant verde.
3. Caso a suite esteja quebrada por motivo não relacionado, **corrigir a suite antes do hotfix** — não criar via merge-fora-do-fluxo.

Se for absolutamente necessário bypassar (situação extrema, p. ex.: vazamento de credenciais), o admin temporariamente desativa a regra, faz o merge, e **reativa imediatamente após**. Auditoria do GitHub registra a operação.

---

## 7. Revisão periódica

Trimestralmente:

- Verificar lista de required status checks (pode ter crescido).
- Validar que `CODEOWNERS` reflete o time atual.
- Confirmar que `security-team` ainda existe e tem os membros corretos.
- Revisar histórico de bypass (deve estar vazio).

---

## 8. Referências

- GitHub branch protection: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches
- CODEOWNERS syntax: https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners
- API: https://docs.github.com/en/rest/branches/branch-protection
