# Resumo de Tarefas de Implementação — Fundação Técnica da Fase 0

> **Modo de operação:** Standalone — não há PRD de origem nem user stories. Esta TechSpec traduz
> diretamente `vision.md`, `docs/brief.md`, `context/domain-map.md`,
> `context/architecture-baseline.md` e `docs/adr/*` em infraestrutura executável (ver cabeçalho da
> TechSpec). A tabela "Rastreabilidade" abaixo mapeia **Fatias da TechSpec → Tasks**, substituindo a
> rastreabilidade US → Tasks do template padrão.
> **TechSpec de origem:** [`techspec.md`](./techspec.md) — Status: Aprovado — revisão de 2026-09-11.
> **ADRs pertinentes:** [ADR-001](../../docs/adr/adr-001-backend-stack-dotnet.md) (stack .NET),
> [ADR-002](../../docs/adr/adr-002-broker-fase0-rabbitmq.md) (RabbitMQ), [ADR-003](../../docs/adr/adr-003-frontend-teste-react.md) (frontend sem gateway).
> **Status do plano:** Confirmado para implementação
> **Regra de entrega:** cada task de comportamento é uma fatia vertical validável isoladamente
>
> **Pré-requisito de execução (fora do escopo destas tasks):** `scripts/ai-flow/gate.sh` ainda não
> existe neste repositório. Antes de despachar qualquer task ao orquestrador/implementer, rode
> `tsg-flow-gate-creator` uma vez para gerar o gate determinístico (stack .NET + Node/Vite). Os
> `gate_command` abaixo assumem que esse script existirá com suporte a `--filter="<selector>"` e
> `--static`.

## Visão Geral

Estabelece o esqueleto técnico dos três serviços HTTP (Catalog, Booking, Payment), do Notification
Worker e do frontend de teste, conectados ao PostgreSQL, RabbitMQ e OpenMetadata compartilhados do
`infra` (Coolify). Não implementa nenhuma regra de negócio — o objetivo é que os futuros PRDs de
domínio (catálogo, reserva, pagamento) comecem sobre uma base já compilável, testável e catalogada,
sem repetir decisões estruturais.

## Skills de Stack Consultadas

| Skill | Caminho | Influência |
|-------|---------|------------|
| `dotnet-architecture` | `.claude/skills/dotnet-architecture` | Clean Architecture por serviço, formato monorepo (`examples/microservices.md`) |
| `dotnet-dependency-config` | `.claude/skills/dotnet-dependency-config` | EF Core + Npgsql, `dotnet user-secrets`, versionamento central de pacotes, RabbitMQ |
| `dotnet-program-setup` | `.claude/skills/dotnet-program-setup` | `Program.cs` enxuto, `Extensions/` por concern |
| `dotnet-observability` | `.claude/skills/dotnet-observability` | Health checks liveness/readiness, logging correlacionado |
| `dotnet-testing` | `.claude/skills/dotnet-testing` | Testcontainers (Postgres, RabbitMQ) |
| `react-architecture` | `.claude/skills/react-architecture` | Estrutura "Base" do frontend de teste |
| `react-testing` | `.claude/skills/react-testing` | Padrão de teste de componente com fetch mockado (MSW) |

## Fases de Implementação

As fases agrupam sequência de comportamento e feedback, não uma camada arquitetural.

### Fase 1 — Convenções e provisionamento de banco (habilitadores)
`Directory.Build.props`/`Directory.Packages.props`/`.editorconfig` compartilhados e os scripts de
`db/bootstrap` que criam o database `localize_stay`, as três roles de serviço e os quatro schemas em
`postgres-main`. Nenhum serviço compila ou conecta antes desta fase existir.

### Fase 2 — Serviços HTTP sobem e provam conexão real
Catalog, Booking e Payment sobem cada um com sua solution Clean Architecture, schema/role próprios e
`/health/ready` consultando o Postgres real.

### Fase 3 — Mensageria de diagnóstico
Booking publica uma mensagem de diagnóstico no vhost `/localize-stay` do RabbitMQ compartilhado; o
Notification Worker consome, faz ACK e loga com `correlationId`/`causationId`.

### Fase 4 — Frontend e contratos
Frontend de teste exibe o status dos três serviços via CORS; cada serviço exporta seu OpenAPI real; a
topologia de diagnóstico é documentada em AsyncAPI; template de Data Contract fica pronto.

### Fase 5 — Catalogação
`ecad-dev-openmetadata` passa a listar os schemas, os três serviços e a topologia RabbitMQ do
Localize Stay, com tags de ownership que os distinguem do `ecad-sba`.

## Mapa de Entrega e Feedback

| Slice | Task | Comportamento observável | Gate executável | Seletor focalizado | Bloqueado por |
|-------|------|---------------------------|------------------|----------------------|----------------|
| EN-01 | 1.0 | Convenções de build compartilhadas restauram/validam sem erro | `gate.sh --static` | N/A (enabling static) | Nenhum |
| EN-02 | 2.0 | `db/bootstrap` cria database/roles/schemas/grants de forma idempotente, com isolamento cruzado provado | `gate.sh --static` | N/A (enabling static) | Nenhum |
| V-01 | 3.0 | Catalog sobe, conecta com `catalog_role` e responde `/health/ready` contra Postgres real | `gate.sh --filter="Catalog.HealthCheckTests"` | `HealthCheckTests` (Catalog.IntegrationTests) | 1.0, 2.0 |
| V-02 | 4.0 | Booking sobe com o mesmo padrão de V-01 | `gate.sh --filter="Booking.HealthCheckTests"` | `HealthCheckTests` (Booking.IntegrationTests) | 1.0, 2.0 |
| V-02 | 5.0 | Payment sobe com o mesmo padrão de V-01 | `gate.sh --filter="Payment.HealthCheckTests"` | `HealthCheckTests` (Payment.IntegrationTests) | 1.0, 2.0 |
| V-03 | 6.0 | Booking publica `DiagnosticPing`; Worker consome, ACKa e loga correlationId/causationId | `gate.sh --filter="Messaging.DiagnosticPingFlowTests"` | `DiagnosticPingFlowTests` | 4.0 |
| V-04 | 7.0 | Frontend mostra os 3 status "Healthy" via CORS, sem gateway | `gate.sh --filter="ServiceStatus.test"` | `ServiceStatus.test.tsx` | 3.0, 4.0, 5.0 |
| V-05 | 8.0 | Os 3 OpenAPI exportados e o AsyncAPI da topologia de diagnóstico validam sem erro | `gate.sh --static` | N/A (enabling static) | 3.0, 4.0, 5.0, 6.0 |
| V-06 | 9.0 | Payload de registro do vhost como `CustomMessaging` no OpenMetadata é construído corretamente | `gate.sh --filter="RegisterRabbitMq.PayloadBuilderTests"` | `PayloadBuilderTests` | 3.0, 4.0, 5.0, 6.0, 8.0 |

### Habilitadores inevitáveis

| Enabler | Task | Justificativa de horizontalidade | Menor validação | Desbloqueia |
|---------|------|-----------------------------------|-------------------|-------------|
| EN-01 | 1.0 | `Directory.Build.props`/`Directory.Packages.props`/`.editorconfig` precisam existir antes do primeiro `dotnet build`; se cada serviço definisse os seus, V-01/V-02 divergiriam em versão de pacote e regra de compilador | `dotnet tool restore` + validação XML dos props | 3.0, 4.0, 5.0 |
| EN-02 | 2.0 | Criar role/schema exige privilégio maior que o de aplicação; a TechSpec já decidiu que isso não pode ser uma migration de serviço (violaria "nenhum serviço aplica migration em schema alheio"). Como a Build Order da TechSpec exige que **parte** de `db/bootstrap` já esteja rodado antes de V-01, mas o inventário de artefatos atribui **todos** os 4 scripts a V-02, esta task consolida o provisionamento completo (database + 3 roles + 4 schemas + grants) em um único habilitador executado antes de qualquer serviço — resolve a inconsistência sem duplicar scripts entre V-01 e V-02 | Rodar os 4 scripts 2x contra um Postgres descartável (idempotência) + query de grants | 3.0, 4.0, 5.0 |

**Nota de desvio do Build Order da TechSpec:** a TechSpec descreve V-02 (Booking/Payment) como
dependente de V-01 ("reaplica o mesmo padrão"). Essa é uma dependência de estilo/precedente de
código, não uma dependência de artefato: Booking e Payment não importam nenhum arquivo produzido por
Catalog, apenas replicam sua forma. Por isso as tasks 4.0 e 5.0 têm `blocked_by: [1.0, 2.0]` (os
habilitadores reais) e são marcadas `parallelizable: true` entre si e com 3.0 — ver "Análise de
Paralelização". Isso segue a regra do Task Creator de que `blocked_by` reflete artefato
efetivamente consumido, não ordem de aprendizado sugerida.

## Tarefas

- [x] 1.0 Convenções de solução compartilhadas (EN-01)
- [x] 2.0 Bootstrap de banco: database, roles, schemas e grants (EN-02)
- [x] 3.0 Catalog sobe e prova conexão real com Postgres (V-01)
- [x] 4.0 Booking sobe com o mesmo padrão de Catalog (V-02)
- [x] 5.0 Payment sobe com o mesmo padrão de Catalog (V-02)
- [x] 6.0 Mensageria de diagnóstico: Booking publica, Notification Worker consome (V-03)
- [x] 7.0 Frontend exibe status dos 3 serviços via CORS (V-04)
- [x] 8.0 Contratos exportados e validados: OpenAPI, AsyncAPI, Data Contract (V-05)
- [x] 9.0 Registro do vhost RabbitMQ como CustomMessaging no OpenMetadata (V-06)

## Rastreabilidade Fatias TechSpec → Tasks

| Fatia/Requisito da TechSpec | Tasks Relacionadas | Tipo de Cobertura |
|------------------------------|----------------------|--------------------|
| EN-01 — Convenções de solução | 1.0 | Direta |
| EN-02 (resolve inconsistência de `db/bootstrap` entre V-01/V-02) | 2.0 | Direta |
| V-01 — Catalog | 3.0 | Direta |
| V-02 — Booking, Payment, schema `integration` | 4.0, 5.0 | Direta |
| V-03 — Mensageria de diagnóstico | 6.0 | Direta |
| V-04 — Frontend | 7.0 | Direta |
| V-05 — Contratos | 8.0 | Direta |
| V-06 — OpenMetadata | 9.0 | Direta |

## Validação de Cobertura

### Requisitos Funcionais / Fatias

| Fatia | Task(s) | Status |
|-------|---------|--------|
| EN-01 | 1.0 | ✅ Coberto |
| EN-02 | 2.0 | ✅ Coberto |
| V-01 | 3.0 | ✅ Coberto |
| V-02 | 4.0, 5.0 | ✅ Coberto |
| V-03 | 6.0 | ✅ Coberto |
| V-04 | 7.0 | ✅ Coberto |
| V-05 | 8.0 | ✅ Coberto |
| V-06 | 9.0 | ✅ Coberto |

### Artefatos da TechSpec

| Artefato | Task | Status |
|----------|------|--------|
| `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` | 1.0 | ✅ |
| `db/bootstrap/{001-database,002-roles,003-schemas,004-grants}.sql` | 2.0 | ✅ |
| `services/catalog/**` (solution, Extensions, DbContext, teste) | 3.0 | ✅ |
| `services/booking/**` (solution, Extensions, DbContext, teste) | 4.0 | ✅ |
| `services/payment/**` (solution, Extensions, DbContext, teste) | 5.0 | ✅ |
| `services/booking/.../MessagingExtensions.cs`, `DiagnosticsEndpoints`, `services/notification-worker/**` | 6.0 | ✅ |
| `frontend/localize-stay-frontend/**` | 7.0 | ✅ |
| `contracts/openapi/*.json`, `contracts/asyncapi/diagnostics-v1.yaml`, `contracts/data-contracts/TEMPLATE.md` | 8.0 | ✅ |
| `scripts/openmetadata/register-rabbitmq.*` | 9.0 | ✅ |
| `README.md` (seções de execução local) | 3.0, 4.0, 5.0, 7.0 | ✅ |

### Categorias Obrigatórias

| # | Categoria | Task(s) / N/A | Skill Relacionada | Status |
|---|-----------|----------------|----------------------|--------|
| 1 | Setup / Configuração | 1.0, 2.0 | `dotnet-dependency-config` | ✅ |
| 2 | Modelos de Dados | 2.0 (schemas/roles), 3.0/4.0/5.0 (migration sentinela) | `dotnet-architecture` | ✅ |
| 3 | Lógica de Negócio | N/A — TechSpec Standalone explícita: nenhuma regra de negócio nesta fundação; fica para os PRDs de domínio | — | ✅ (justificado) |
| 4 | Endpoints / Interfaces | 3.0, 4.0, 5.0 (`/health/*`), 6.0 (`POST /internal/diagnostics/ping`) | `dotnet-program-setup` | ✅ |
| 5 | Integrações Externas | 2.0 (Postgres), 6.0 (RabbitMQ), 9.0 (OpenMetadata) | `dotnet-dependency-config` | ✅ |
| 6 | Validações e Erros | 2.0 (subtarefa 2.5 — negação de acesso cruzado entre schemas) | `dotnet-code-quality` | ✅ |
| 7 | Testes | Subtarefas em 1.0–9.0 (Testcontainers, MSW, unitário de payload) | `dotnet-testing`, `react-testing` | ✅ |
| 8 | Observabilidade | 3.0/4.0/5.0 (health liveness/readiness), 6.0 (correlationId/causationId) | `dotnet-observability` | ✅ |
| 9 | Documentação | 2.0 (`db/bootstrap/README.md`), 3.0/4.0/5.0/7.0 (README raiz) | — | ✅ |
| 10 | Segurança | 2.0 (grants de menor privilégio por role/schema) | — | ✅ |

### Coesão e Faixa de Tamanho

| Task | slice_type | Criar | Modificar | Subtarefas | Fatias | Faixa | Justificativa |
|------|------------|-------|-----------|------------|--------|-------|----------------|
| 1.0 | enabling | 4 | 0 | 4 | 1 | ✅ | Dentro da faixa budget |
| 2.0 | enabling | 5 | 0 | 6 | 1 | ✅ | Dentro da faixa budget |
| 3.0 | vertical | 9 | 1 | 6 | 1 | ⚠️ | Acima de "criar 8" da faixa budget; esqueleto Clean Architecture completo (Api/Application/Domain/Infra + Extensions + DbContext + teste) é o menor conjunto que compila e prova `/health/ready` real — dividir por camada quebraria a fatia (ver `references/vertical-slicing.md`) |
| 4.0 | vertical | 9 | 1 | 6 | 1 | ⚠️ | Mesma justificativa de 3.0 — réplica estrutural do mesmo padrão para Booking |
| 5.0 | vertical | 9 | 1 | 6 | 1 | ⚠️ | Mesma justificativa de 3.0 — réplica estrutural do mesmo padrão para Payment |
| 6.0 | vertical | 7 | 1 | 6 | 1 | ✅ | Dentro da faixa budget; cruza Booking e Worker porque é uma única jornada (publish→consume→ack) |
| 7.0 | vertical | 6 | 4 | 5 | 1 | ✅ | Dentro da faixa budget |
| 8.0 | enabling | 5 | 0 | 4 | 1 | ✅ | Dentro da faixa budget |
| 9.0 | enabling | 4 | 0 | 4 | 1 | ✅ | Dentro da faixa budget |

### Integridade dos Gates

| Task | Gate | Teste/fixture disponível | Filtro isolado | Repo compilável | Dependência futura | Status |
|------|------|-----------------------------|-------------------|--------------------|------------------------|--------|
| 1.0 | `gate.sh --static` | Criado na própria task | N/A (static) | Sim | Não | ✅ |
| 2.0 | `gate.sh --static` | Criado na própria task | N/A (static) | Sim | Não | ✅ |
| 3.0 | `gate.sh --filter="Catalog.HealthCheckTests"` | Criado na própria task | Sim | Sim | Não | ✅ |
| 4.0 | `gate.sh --filter="Booking.HealthCheckTests"` | Criado na própria task | Sim | Sim | Não | ✅ |
| 5.0 | `gate.sh --filter="Payment.HealthCheckTests"` | Criado na própria task | Sim | Sim | Não | ✅ |
| 6.0 | `gate.sh --filter="Messaging.DiagnosticPingFlowTests"` | Criado na própria task | Sim | Sim | Não | ✅ |
| 7.0 | `gate.sh --filter="ServiceStatus.test"` | Criado na própria task | Sim | Sim | Não | ✅ |
| 8.0 | `gate.sh --static` | Criado na própria task | N/A (static) | Sim | Não | ✅ |
| 9.0 | `gate.sh --filter="RegisterRabbitMq.PayloadBuilderTests"` | Criado na própria task | Sim | Sim | Não | ✅ |

### Ciclo de Vida de Artefatos Compartilhados

| Artefato | Primeira task produtora | Tasks consumidoras | Dependências consistentes | Status |
|----------|----------------------------|--------------------------|---------------------------------|--------|
| `Directory.Build.props`, `Directory.Packages.props` | 1.0 | 3.0, 4.0, 5.0, 6.0 | Sim | ✅ |
| `db/bootstrap/*.sql` (database, roles, schemas, grants) | 2.0 | 3.0, 4.0, 5.0 | Sim | ✅ |
| `services/booking/**` (solution + `/health/ready`) | 4.0 | 6.0, 7.0 | Sim | ✅ |
| `services/catalog/**`, `services/payment/**` (`/health/ready`) | 3.0, 5.0 | 7.0 | Sim | ✅ |
| OpenAPI/AsyncAPI de `contracts/` | 8.0 | 9.0 | Sim | ✅ |
| Topologia RabbitMQ `/localize-stay` provada em 6.0 | 6.0 | 8.0, 9.0 | Sim | ✅ |

Nenhuma task depende de artefato produzido por task posterior.

## Análise de Paralelização

### Lanes de Execução Paralela

| Lane | Tarefas | Descrição |
|------|---------|------------|
| Lane A | 3.0 | Catalog — só depende dos habilitadores 1.0/2.0 |
| Lane B | 4.0 | Booking — só depende dos habilitadores 1.0/2.0; réplica independente de 3.0 |
| Lane C | 5.0 | Payment — só depende dos habilitadores 1.0/2.0; réplica independente de 3.0/4.0 |

As lanes A/B/C podem rodar em paralelo assim que 1.0 e 2.0 concluírem, mesmo que a TechSpec descreva
V-02 como posterior a V-01 por precedente de código (ver nota de desvio acima). O executor standard
permanece sequencial; isto é só informação de planejamento.

### Caminho Crítico

1.0/2.0 (paralelos entre si) → 4.0 (Booking, mais restritivo pois desbloqueia mensageria) → 6.0 → 8.0
→ 9.0. 3.0 e 5.0 alimentam 7.0 em paralelo ao ramo de mensageria, mas 7.0 só fecha depois que os três
health checks (3.0, 4.0, 5.0) existirem.

### Diagrama de Dependências

```
1.0 EN-01 ─┐
           ├─▶ 3.0 V-01 Catalog ───────────────┐
2.0 EN-02 ─┤                                     ├─▶ 7.0 V-04 Frontend ─┐
           ├─▶ 4.0 V-02 Booking ─▶ 6.0 V-03 Msg ─┤                        ├─▶ 8.0 V-05 Contratos ─▶ 9.0 V-06 OpenMetadata
           └─▶ 5.0 V-02 Payment ─────────────────┘                        │
                                                    (6.0 também alimenta 8.0)
```
