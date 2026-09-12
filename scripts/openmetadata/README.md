# Registro do RabbitMQ no OpenMetadata (V-06, task 9.0)

O vhost `/localize-stay` do broker compartilhado `ecad-dev-rabbitmq` é
catalogado como serviço `CustomMessaging` (`localize-stay-rabbitmq`), com um
tópico por exchange/fila real de V-03 (`diagnostics.topic`,
`notification.diagnostics`) — via `PUT /v1/services/messagingServices` +
`PUT /v1/topics` (upsert idempotente). O OpenMetadata 2.0.x não tem conector
de ingestão nativo para RabbitMQ, por isso o registro é pontual via script,
não uma pipeline agendada.

## Arquivos

| Arquivo | Papel |
|---|---|
| `register-rabbitmq/` | Console .NET: `PayloadBuilder.cs` (JSON puro, sem rede) + `Program.cs` (chamada HTTP real) |
| `RegisterRabbitMq.Tests/` | `PayloadBuilderTests` — teste unitário do payload, sem rede (gate da task) |
| `ingestion-postgres.yaml` | Config versionada do connector Postgres filtrado a `localize_stay` |
| `RegisterRabbitMq.sln` | Solution dos dois projetos acima |

## Uso (dono do homelab — manual, fora do gate)

```bash
# 1. Só imprime os payloads, sem rede:
dotnet run --project scripts/openmetadata/register-rabbitmq -- --dry-run

# 2. Registro real (PAT de escopo mínimo de escrita, gerado no próprio
#    OpenMetadata — nunca o token administrativo do Coolify):
export OPENMETADATA_PAT="<pat>"
dotnet run --project scripts/openmetadata/register-rabbitmq -- --url https://<ecad-dev-openmetadata>/api

# 3. Confirmar na UI: 4 schemas + 3 serviços de API (specs de V-05) +
#    CustomMessaging com 2 tópicos, todos com a tag `localize-stay`.
```

## Questões em aberto da TechSpec (resolução nesta task)

- **`CustomMessaging` na build 2.0.1:** verificado offline que
  `CustomMessaging` é valor válido do enum `messagingServiceType` no schema
  do OpenMetadata 2.0.x (docs públicas de `messagingServices/create` +
  `openmetadata-standards`), com config de conexão do tipo
  `CustomMessaging`. Nenhum workaround (Kafka genérico) foi necessário no
  payload. A confirmação na build instalada específica é manual (passo 3).
- **PAT de escopo mínimo:** o nome/escopo exato depende da instância real;
  o script aceita qualquer PAT via `--pat`/`OPENMETADATA_PAT` — sem bloqueio.

## Limitações documentadas (não bloqueiam o gate)

O gate cobre só o payload (`PayloadBuilderTests`, sem rede). Exigem o
`ecad-dev-openmetadata` real e ficam como verificação manual: gerar o PAT,
executar o script, rodar a ingestion Postgres (`ingestion-postgres.yaml`) e
confirmar visualmente a UI — o executor desta task não tem acesso ao homelab.
