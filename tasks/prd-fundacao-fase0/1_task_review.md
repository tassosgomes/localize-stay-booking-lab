# Revisão — Task 1.0: Convenções de solução compartilhadas (EN-01)

- **Modo:** focused, primeira revisão
- **Task:** `tasks/prd-fundacao-fase0/1_task.md` (status `validating`, `enabling/static`)
- **Branch:** `feature/prd-fundacao-fase0`
- **HEAD revisado:** `87a312568db20ed934fa2594669a1eb486542ee2`
- **Data:** 2026-09-12

## 1. Gate antes da revisão semântica

Contrato lido do front-matter da task (`verification_type: static`,
`gate_command: scripts/ai-flow/gate.sh --static`, evidência: `dotnet tool restore` 2x + `xmllint`).
Nenhum material semântico foi carregado antes do gate.

| Comando | Resultado |
|---|---|
| `scripts/ai-flow/gate.sh --static` (worktree da task) | `GATE: APROVADO` — arquivos alterados: 8 (.NET: 4, node: 0); format pulado; build ok (nada a compilar); testes n/a (static). Exit **0** |

Gate com exit 0 → revisão semântica prosseguiu. Não houve exit 1 (reprovação automática)
nem exit 2 (infra/uso).

## 2. Evidência específica reexecutada pelo validator

Rodado em worker fresco no worktree
`/home/tsgomes/github-tassosgomes/localize-stay-booking-lab-prd-fundacao-fase0`:

| Comando | Resultado |
|---|---|
| `dotnet tool restore` (1ª vez) | `Tool 'dotnet-ef' (version '10.0.12') was restored.` Exit **0** |
| `dotnet tool restore` (2ª vez, idempotência) | Idem. Exit **0** |
| `xmllint --noout Directory.Build.props Directory.Packages.props` | Sem saída de erro. Exit **0** |
| `dotnet --version` | `10.0.400` (confirma base do `LangVersion` e das versões centrais) |

Critérios de sucesso da task conferidos um a um: todos atendidos (seção 4).

## 3. Escopo revisado

- **Diff tracked desde o checkpoint:** apenas `tasks/prd-fundacao-fase0/1_task.md`
  (`status: pending` → `validating`, 1 linha). Nenhum código tracked modificado.
- **Untracked do escopo da task (os 4 artefatos exigidos, todos na raiz):**
  - `Directory.Build.props`
  - `Directory.Packages.props`
  - `.editorconfig`
  - `.config/dotnet-tools.json`
- **Untracked fora do escopo da task (infra do fluxo, ignorados na decisão):**
  `scripts/ai-flow/gate.sh`, `scripts/ai-flow/gate.contract.md`,
  `tasks/prd-fundacao-fase0/flow-state.json`. Não fazem parte da entrega nem a contaminam.
- **Skills pertinentes consultadas:** `dotnet-dependency-config` (versionamento central,
  convenção `dotnet-tools.json`, checklist do diff). Techspec/ADR-001 não precisaram ser
  abertos: nenhuma verificação exigiu trecho além do contrato da task.

## 4. Verificações critério a critério

1. **4 arquivos na raiz** — OK. Todos existem nos caminhos exigidos (seção 3).
2. **`Directory.Build.props`** (`Directory.Build.props:1-9`) — OK:
   `Nullable=enable` (l.3), `ImplicitUsings=enable` (l.4),
   `TreatWarningsAsErrors=true` (l.5), `LangVersion=14` explícita (l.7) com comentário
   amarrando a C# 14 / SDK .NET 10 (`dotnet --version` = 10.0.400, confirmado acima).
3. **`Directory.Packages.props` com `ManagePackageVersionsCentrally=true`**
   (`Directory.Packages.props:3`) — OK. Ponto único de verdade; nenhum serviço existe
   ainda para divergir.
4. **Pacotes centrais mínimos, cada um com versão central única**
   (`Directory.Packages.props:9-17`) — OK: `Microsoft.EntityFrameworkCore.Design` 10.0.12,
   `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `Swashbuckle.AspNetCore` 10.2.3, `xunit`
   2.9.3, `xunit.runner.visualstudio` 4.0.0, `Testcontainers.PostgreSql` 4.15.0,
   `Testcontainers.RabbitMq` 4.15.0, `Rmq.CloudEvents` 1.1.1. Inclui ainda
   `Microsoft.NET.Test.Sdk` 18.10.0 (extra razoável para o runner xUnit; ver
   recomendações, não bloqueante).
5. **`dotnet-ef` na mesma major do `Design`** (skill `dotnet-dependency-config`, regra EF
   Core) — OK: `.config/dotnet-tools.json:6` fixa `dotnet-ef` em `10.0.12`, major **10**,
   igual à major do `Microsoft.EntityFrameworkCore.Design` 10.0.12. Restauração provada
   2x com exit 0.
6. **`.editorconfig` cobre `[*.cs]`** (`.editorconfig:9-17`) — OK: `root = true`,
   seção `[*.cs]` com `indent_style`/`indent_size`, regras `csharp_style_var_*` e
   `dotnet_sort_system_directives_first`. Sintaxe válida, legível por IDE/linter.
7. **XML válido** — OK: `xmllint --noout` nos dois `.props`, exit 0.
8. **Artefatos do gate criados na própria task; nada de task futura exigido; evidência
   prova só este habilitador** — OK: os 4 arquivos são untracked novos desta task; nenhum
   serviço/projeto existe ainda, e a validação não depende de nenhum.
9. **Checklist do diff da skill pertinente** — OK no aplicável: pacote/versão necessários
   ao requisito; sem connection strings/secrets no código; `dotnet-ef` fixado e
   compatível; sem alteração de container local; sem upgrade amplo colateral (repo sem
   serviços ainda).

## 5. Bloqueantes

Nenhum. Não há falha essencial: gate estático aprovado, evidência reexecutada com exit 0
em todos os comandos, e todos os critérios verificáveis atendidos com arquivo/linha
identificados acima.

## 6. Recomendações (não bloqueantes)

1. **Revalidar resolução dos `PackageVersion` no primeiro `dotnet restore` real (task
   3.0).** O gate static + `dotnet tool restore` prova o manifest de ferramentas e a
   validade XML, mas nenhuma restauração NuGet de projeto ocorreu ainda (nenhum
   `.csproj` existe). Se alguma versão central não resolver no registry naquele momento,
   ajustar só o número da versão — sem reprovação desta task.
2. **Documentar em 3.0 por que `Microsoft.NET.Test.Sdk` entrou no centro.**
   Pacote extra além do mínimo da task, porém esperado pelo runner de testes; apenas
   registro, sem ação nesta task.

Total de recomendações não bloqueantes: **2**.

## 7. Imutabilidade durante a revisão

- `git rev-parse HEAD` antes da análise semântica: `87a3125…` (completo em cabeçalho);
  repetido após as evidências e antes da escrita deste relatório: idêntico.
- Hashes SHA-256 dos 4 arquivos do escopo coletados antes da redação e conferidos após:
  `Directory.Build.props 0a3bb537…`, `Directory.Packages.props f35e762d…`,
  `.editorconfig dc8d4f18…`, `.config/dotnet-tools.json 44ba3f2e…` — sem alteração.
- Único arquivo escrito pelo validator: este relatório (`tasks/prd-fundacao-fase0/1_task_review.md`),
  artefato permitido do fluxo; nenhum código, status, task ou commit foi editado.
- HEAD e código inalterados durante a revisão; nenhuma nova validação é exigida por
  mutação.

## Resultado

**VALIDAÇÃO APROVADA** (2 recomendações não bloqueantes).
