---
status: done
slice_type: enabling
verification_type: static
parallelizable: true
blocked_by: []
---

<task_context>
<domain>engine/infra/solution-conventions</domain>
<type>implementation</type>
<scope>configuration</scope>
<complexity>low</complexity>
<dependencies>none</dependencies>
<unblocks>"3.0, 4.0, 5.0, 6.0"</unblocks>
<feedback_checkpoint>`dotnet tool restore` conclui sem erro usando `.config/dotnet-tools.json`; `xmllint --noout Directory.Build.props Directory.Packages.props` não retorna erro</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --static</gate_command>
<gate_test_selector>N/A — habilitador static, sem código executável nesta task (ver justificativa em vertical-slicing.md: convenção compartilhada que precisa existir antes do primeiro `dotnet build`)</gate_test_selector>
<gate_expected_result>`dotnet tool restore` termina com código 0; `xmllint --noout` nos dois arquivos `.props` não imprime erro; `.editorconfig` é lido sem warning por um IDE/linter EditorConfig</gate_expected_result>
<static_evidence>Rodar `dotnet tool restore` duas vezes seguidas (idempotência do manifest) e `xmllint --noout Directory.Build.props Directory.Packages.props`</static_evidence>
<vertical_slice>N/A — enabling</vertical_slice>
</task_context>

# Tarefa 1.0: Convenções de solução compartilhadas (EN-01)

## Relacionada as User Stories

- N/A — TechSpec Standalone, sem PRD/user stories de origem. Cobre o habilitador EN-01 do
  `techspec.md`.

## Visão Geral

`Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` e `.config/dotnet-tools.json`
precisam existir na raiz do repositório antes do primeiro `dotnet build` de qualquer serviço. Se cada
serviço (Catalog, Booking, Payment, Notification Worker) definisse os seus próprios, as tasks 3.0/4.0
divergiriam em versão de pacote e regra de compilador, e "mesmo padrão" deixaria de ser verdade — por
isso este é um habilitador horizontal (`slice_type: enabling`), não uma fatia vertical.

## Entrega Observável

- **Entrada ou gatilho:** `dotnet tool restore` executado na raiz do repositório.
- **Resultado esperado:** ferramentas .NET (incluindo `dotnet-ef` fixado na major do `EFCore.Design`)
  restauradas com sucesso; os dois arquivos `.props` são XML válido e aplicam `Nullable`,
  `ImplicitUsings`, `TreatWarningsAsErrors` e `LangVersion` centralmente.
- **Checkpoint de feedback:** `dotnet tool restore` (código 0) + `xmllint --noout` nos dois `.props`.
- **Seletor focalizado:** N/A (enabling static).
- **Fora deste checkpoint:** nenhum serviço ainda existe para provar que as convenções realmente se
  aplicam a um build real — isso é provado em 3.0 (Catalog), a primeira task a consumir estes
  arquivos.

## Requisitos

- Um único ponto de verdade para versões de pacotes centrais usados por todos os serviços (EF Core,
  Npgsql, Swashbuckle, xUnit, Testcontainers, `Rmq.CloudEvents`).
- `TreatWarningsAsErrors` habilitado para todos os projetos desde o início (evita débito silencioso).
- `.editorconfig` cobrindo pelo menos C# (indentação, `var`, ordenação de usings) — não precisa ser
  exaustivo, mas deve existir.
- `dotnet-ef` fixado na mesma major do pacote `Microsoft.EntityFrameworkCore.Design` referenciado em
  `Directory.Packages.props`.

## Arquivos Envolvidos

- **Criar:**
  - `Directory.Build.props`
  - `Directory.Packages.props`
  - `.editorconfig`
  - `.config/dotnet-tools.json`
- **Modificar:**
  - Nenhum.
- **Referência:**
  - `docs/adr/adr-001-backend-stack-dotnet.md` (stack .NET usada por todos os serviços)
  - `context/architecture-baseline.md` (regras de comunicação e observabilidade que os pacotes
    centrais precisam suportar, ex.: cliente RabbitMQ, health checks)
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — versionamento central de pacotes (`Directory.Packages.props`),
    convenção de `dotnet-tools.json`

## Subtarefas

- [ ] 1.1 Criar `Directory.Build.props` com `Nullable=enable`, `ImplicitUsings=enable`,
      `TreatWarningsAsErrors=true`, `LangVersion` explícita (mesma major do SDK confirmado no
      repositório)
- [ ] 1.2 Criar `Directory.Packages.props` com `ManagePackageVersionsCentrally=true` e as versões
      centrais de EF Core, Npgsql, Swashbuckle, xUnit, Testcontainers e `Rmq.CloudEvents`
- [ ] 1.3 Criar `.editorconfig` e `.config/dotnet-tools.json` (com `dotnet-ef` fixado)
- [ ] 1.4 Validar com `dotnet tool restore` (2x, idempotência) e `xmllint --noout` nos dois `.props`;
      registrar a evidência

## Sequenciamento

- Bloqueado por: Nenhum
- Desbloqueia: 3.0, 4.0, 5.0, 6.0 (qualquer `dotnet build` de serviço)
- Paralelizável: Sim (independente de 2.0 — EN-02 mexe só em banco/SQL, sem sobreposição de arquivo)

## Rastreabilidade

- Esta tarefa cobre: Habilitador EN-01 da TechSpec (`techspec.md`, seção "Habilitadores
  inevitáveis").
- Evidência esperada: os 4 arquivos existem na raiz; `dotnet tool restore` e `xmllint` passam; a
  primeira solution real (task 3.0) compila usando estes arquivos sem precisar redefini-los.

## Detalhes de Implementação

Da TechSpec (`techspec.md`, "Habilitadores inevitáveis" e "Estrutura de repositório"):

> `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` e
> `.config/dotnet-tools.json` precisam existir antes do primeiro `dotnet build`; se cada serviço
> definir os seus, V-02/V-03 divergem em versão de pacote e regras de compilador, e o "mesmo padrão"
> deixa de ser verdade.

Versões centrais mínimas a fixar em `Directory.Packages.props` (nomes de pacote; a versão exata deve
ser a mais recente estável compatível com o SDK .NET já instalado no ThinkPad — confirmar com
`dotnet --version` antes de escolher):

- `Microsoft.EntityFrameworkCore.Design`
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Swashbuckle.AspNetCore`
- `xunit`, `xunit.runner.visualstudio`
- `Testcontainers.PostgreSql`, `Testcontainers.RabbitMq`
- `Rmq.CloudEvents` (biblioteca de mensageria já referenciada no baseline de
  `dotnet-dependency-config`, usada a partir da task 6.0)

**Convenções da stack (das skills consultadas):**
- Versionamento central de pacotes via `Directory.Packages.props`, conforme
  `dotnet-dependency-config`.
- `dotnet-ef` no `.config/dotnet-tools.json` fixado na mesma major do
  `Microsoft.EntityFrameworkCore.Design` — evita incompatibilidade entre CLI e biblioteca.

## Prontidão para Implementação

- **Decisões fechadas:** `TreatWarningsAsErrors=true` para todos os projetos desde o início (decisão
  da TechSpec, não abrir para debate); versionamento central obrigatório (nenhum serviço fixa versão
  de pacote individualmente).
- **Limites de decisão do implementer:** escolha das versões exatas de cada pacote (desde que estável
  e compatível com o SDK instalado); regras específicas de `.editorconfig` além do mínimo listado.
- **Dependências disponíveis:** nenhuma — é a primeira task do plano.
- **Artefatos exigidos pelo gate:** `.config/dotnet-tools.json` e os dois `.props` são criados nesta
  própria task; nenhum artefato preexistente é necessário.
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] `dotnet tool restore` (rodado da raiz do repo) termina com código de saída 0
- [ ] `dotnet tool restore` rodado uma segunda vez também termina com código 0 (idempotência do
      manifest)
- [ ] `xmllint --noout Directory.Build.props Directory.Packages.props` não retorna erro
- [ ] `Directory.Packages.props` lista pelo menos os pacotes centrais citados em "Detalhes de
      Implementação", cada um com uma única versão central
- [ ] `.editorconfig` existe e cobre pelo menos a seção `[*.cs]`
- [ ] Checkpoint de feedback executado: `dotnet tool restore && xmllint --noout Directory.Build.props
      Directory.Packages.props` → sem erro
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados nela (todos criados
      nesta própria task)
- [ ] Nenhum arquivo produzido por task futura é necessário para validar esta task
- [ ] A evidência acima prova somente este habilitador e não depende de nenhum serviço já existir
