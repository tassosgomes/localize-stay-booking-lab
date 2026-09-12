# Revisão — Task 9.0: Registro do vhost RabbitMQ como CustomMessaging (V-06)

Modo: focused (primeira revisão). Não foram editados código, status, tasks ou commits.
Worktree: `/home/tsgomes/github-tassosgomes/localize-stay-booking-lab-prd-fundacao-fase0`, branch `feature/prd-fundacao-fase0`.
Task lida completa: `tasks/prd-fundacao-fase0/9_task.md` (181 linhas).

## 1. Contrato e gate (estágio 1, antes da semântica)

Contrato declarado na task:
- frontmatter `verification_type: static`
- `gate_command`: `scripts/ai-flow/gate.sh --filter="RegisterRabbitMq.PayloadBuilderTests"`
- `gate_test_selector`: classe `PayloadBuilderTests` do projeto `scripts/openmetadata/RegisterRabbitMq.Tests`
- `gate_expected_result`: teste(s) passam; payload tem `serviceType: CustomMessaging`, nome `localize-stay-rabbitmq`, tag `localize-stay`; tópicos `diagnostics.topic` + `notification.diagnostics`
- `static_evidence`: teste unitário sem HTTP real; chamada real é verificação manual

Divergência de rótulo: frontmatter diz `static`, mas comando/seletor/esperado/evidência descrevem teste com filtro (behavioral).
Julgamento: **cosmética, não bloqueante**. O plano (`tasks/prd-fundacao-fase0/tasks.md:190`) prevê exatamente
`gate.sh --filter="RegisterRabbitMq.PayloadBuilderTests"` para 9.0, e o contrato executável do orquestrador (filtro)
foi o executado. O conteúdo de `static_evidence` ("teste unitário sem chamada HTTP") é precisamente o que o filtro executa.
Ver recomendação R1 (alinhar rótulo).

Gate executado pelo validator (antes de qualquer leitura semântica além do contrato):
- `scripts/ai-flow/gate.sh --filter="RegisterRabbitMq.PayloadBuilderTests"` → `GATE: APROVADO`, exit 0
- Output: `arquivos alterados: 12 (.NET: 6, node: 0)`; `format: dotnet format ok (RegisterRabbitMq.sln: 3 arquivos)`;
  `build: dotnet build ok (RegisterRabbitMq.sln 0W/0E + 4 services sln ok)`; `testes: ok (RegisterRabbitMq.PayloadBuilderTests=9)`
- Exit 1 não ocorreu (reprovaria e encerraria); exit 2 não ocorreu (sem VALIDATION ERROR de infra/uso).

## 2. Escopo revisado

- Diff desde checkpoint `1399213` (`checkpoint(task 8.0)`): HEAD ainda é `1399213`; o diff é só não-commitado:
  `M tasks/prd-fundacao-fase0/9_task.md` (pending→validating), `M tasks/prd-fundacao-fase0/flow-state.json` (active 8.0→9.0),
  mais untracked do escopo `scripts/openmetadata/**` e `scripts/ai-flow/` (gate copiado pelo preparo).
- Arquivos fonte do escopo (ignorando `bin/`/`obj/`): `register-rabbitmq/PayloadBuilder.cs` (120 linhas),
  `register-rabbitmq/Program.cs` (146), `register-rabbitmq/RegisterRabbitMq.csproj`,
  `RegisterRabbitMq.Tests/PayloadBuilderTests.cs` (79), `RegisterRabbitMq.Tests/RegisterRabbitMq.Tests.csproj`,
  `RegisterRabbitMq.sln`, `ingestion-postgres.yaml` (58), `README.md` (51).

## 3. Verificação item a item (procedimento do orquestrador)

(a) Payload com serviceType CustomMessaging + nome + tags — **OK**.
- `PayloadBuilder.cs:30-36`: `ServiceName="localize-stay-rabbitmq"`, `ServiceType="CustomMessaging"`,
  `ConnectionConfigType="CustomMessaging"`, `TagFqn="localize-stay"`.
- `BuildMessagingServicePayload` (`:56-77`): `name`, `serviceType`, `connection.config.type`, `tags` via `TagLabels()` (`:110-119`, tagFQN + Classification/Manual/Confirmed).
- Confirmado também via `--dry-run` (payload impresso com os 4 campos) e pelos testes (abaixo).

(b) Tópicos reais de V-03 — **OK**.
- `PayloadBuilder.cs:40-44` `TopicNames = [diagnostics.topic, notification.diagnostics]`; `BuildTopicPayloads()` (`:107-108`)
  e `BuildTopicPayload` (`:83-101`, `service.name=localize-stay-rabbitmq` + tags).
- Confere com a topologia provada: `contracts/asyncapi/diagnostics-v1.yaml:76,83-94` (exchange `diagnostics.topic`,
  fila `notification.diagnostics`) e `tasks/prd-fundacao-fase0/6_task.md:65,137-138,155`.

(c) Teste sem rede (9 testes) — **OK**.
- `PayloadBuilderTests.cs`: só `JsonNode.Parse` + asserts; grep por `HttpClient|Http|network|Testcontainer|Docker` em
  `RegisterRabbitMq.Tests/` (excluindo bin/obj) retorna zero.
- Contagem: 5×`[Fact]` + 2×`[Theory]`×2 InlineData = 9 casos. Gate reportou 9; reexecução independente (abaixo) confirma 9/9.

(d) PAT/base só via env/args, nunca hardcoded nem impresso — **OK**.
- `Program.cs:26-30` (`OPENMETADATA_PAT`, `OPENMETADATA_BASE_URL`, default `http://localhost:8585/api`);
  `ParseArgs` (`:82-112`): `--pat`, `--url`, `--dry-run`, precedência do argumento; `ResolvePat` (`:114-122`) cai para env.
- Sem segredo versionado: grep por `PAT|password|secret|token` no escopo mostra só nomes de vars/placeholders
  (`OPENMETADATA_PAT`, `${OM_POSTGRES_PASSWORD}`, `<pat>`, `--pat <token>`); busca por `eyJ|ghp_|Bearer [A-Za-z0-9]{10,}|password\s*=\s*"..."` retorna zero.
- PAT nunca impresso: erro de HTTP imprime `Resposta (PAT omitido)` (`:133`); exceção imprime só tipo + `(PAT omitido)` (`:142`);
  falha agregada não ecoa o token (`:72`). `--dry-run` (`:39-50`) sai antes de resolver/usar o PAT e não faz rede.

(e) Ingestion filtrada a localize_stay com segredos via env — **OK**.
- `ingestion-postgres.yaml:17-30`: `type: postgres`, `hostPort: postgres-main:5432`, `database: localize_stay`,
  `ingestAllDatabases: false`, `password: ${OM_POSTGRES_PASSWORD}` (env, nunca versionada).
- Filtros (`:35-43`): `databaseFilterPattern.includes=[localize_stay]`, `schemaFilterPattern.includes=[catalog, booking, payment, integration]`,
  `includeTables/Views: true`, sem profiler/lineage. Servidor/token via `${OM_SERVER_HOST_PORT}`/`${OM_INGESTION_JWT}` (`:53-56`).
  Cabeçalho (`:1-16`) declara que a ingestion roda no próprio OpenMetadata, não neste repo.

(f) Chamada real/UI/PAT na build 2.0.1 como limitação manual documentada, não bloqueio — **OK**.
- `PayloadBuilder.cs:13-27` (remarks): `PUT /api/v1/services/messagingServices` + `PUT /api/v1/topics` (upsert),
  `CustomMessaging` válido no enum `messagingServiceType` (docs públicas), confirmação na build 2.0.1 instalada = verificação manual.
- `README.md:35-51`: questões em aberto resolvidas sem workaround + seção "Limitações documentadas" (gate cobre só payload;
  PAT/execução/ingestion/UI são manuais, executor sem acesso ao homelab). `ingestion-postgres.yaml:11-16` repete o passo manual.
  Conforme task (`Fora deste checkpoint`, subtarefa 9.5 fora do gate) e TechSpec (decisão delegada), tratar como limitação é correto.

(g) Namespace unificado não quebrou o contrato do gate — **OK**.
- Ambos os `.csproj` usam `RootNamespace=RegisterRabbitMq`; `PayloadBuilderTests.cs:5` declara `namespace RegisterRabbitMq;`
  (classe `PayloadBuilderTests`). O seletor `RegisterRabbitMq.PayloadBuilderTests` casa por substring FQN no `dotnet test`
  e selecionou exatamente 9 testes, nenhum alheio (evidência abaixo: `Total: 9`). Nenhum teste fora do escopo foi executado.

## 4. Evidência reexecutada pelo validator

- `dotnet test scripts/openmetadata/RegisterRabbitMq.Tests --filter "FullyQualifiedName~RegisterRabbitMq.PayloadBuilderTests"` →
  `Passed! - Failed: 0, Passed: 9, Skipped: 0, Total: 9` (`net10.0`, ~31ms), exit 0.
  Prova somente payload correto; não depende de task futura (é a última do plano); artefatos do gate criados nesta task.
- `dotnet run --project scripts/openmetadata/register-rabbitmq -- --dry-run` → exit 0; imprime
  `PUT /v1/services/messagingServices` (serviceType CustomMessaging, nome, tag) + 2× `PUT /v1/topics` com nomes/serviço/tags corretos, sem rede.
- Critérios de sucesso da task: focalizado passa ✓; seletor encontra ≥1 e só casos da task (9, sem relação externa) ✓;
  build sem erros ✓ (gate: 5 sln ok); payload serviceType/nome/tag ✓; 2 tópicos ✓; PAT via env ✓;
  checkpoint manual documentado como evidência manual ✓; artefatos existem na task ✓; nada de task futura ✓; sem dependência futura ✓.

## 5. Bloqueantes

Nenhum. Gate aprovado + semântica confere nos 7 pontos (a–g).

## 6. Recomendações (não bloqueantes)

- R1 (cosmética, contrato): alinhar `verification_type` do frontmatter de 9.0 com o contrato executável —
  ou `behavioral` (dado o filtro), ou manter `static` com nota explícita de que a evidência = teste com filtro
  (o plano já prevê filtro). Não afeta o veredito.
- R2 (higiene): não versionar `bin/`/`obj/` sob `scripts/openmetadata/` (há artefatos compilados untracked);
  garantir `.gitignore` cobre esses diretórios antes do checkpoint/commit da task.
- R3 (clareza menor): `ingestion-postgres.yaml:57-58` menciona aplicar tag/owner na UI; se o servidor suportar
  `classification`/`owner` no YAML, considerar declarar explicitamente — hoje o texto como comentário + passo manual é suficiente.

## 7. Imutabilidade

- HEAD no início: `139921369aca4b9b0a647117121cbc4fb5141740`; HEAD ao final: `139921369aca4b9b0a647117121cbc4fb5141740` — inalterado.
- Validator não editou código, status, tasks ou commits; único arquivo escrito por esta revisão é este relatório.
- `git status` final: `M tasks/prd-fundacao-fase0/9_task.md`, `M tasks/prd-fundacao-fase0/flow-state.json`,
  `?? scripts/ai-flow/`, `?? scripts/openmetadata/` (inclui este `9_task_review.md` como untracked do escopo) — nenhum commit criado.

## Resultado

VALIDAÇÃO APROVADA (3 recs)
