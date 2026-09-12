---
status: done
slice_type: enabling
verification_type: static
parallelizable: false
blocked_by: [3.0, 4.0, 5.0, 6.0, 8.0]
---

<task_context>
<domain>observability/catalog</domain>
<type>integration</type>
<scope>configuration</scope>
<complexity>medium</complexity>
<dependencies>external_apis</dependencies>
<unblocks>""</unblocks>
<feedback_checkpoint>Teste unitário `PayloadBuilderTests` verde, comprovando que o payload enviado a `/v1/services/messagingServices` e `/v1/topics` tem os campos corretos (nome, tipo `CustomMessaging`, tags de ownership). Manualmente, a UI do `ecad-dev-openmetadata` real lista os 4 schemas, os 3 serviços de API e a fila/exchange de diagnóstico com owner e tag `localize-stay`</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="RegisterRabbitMq.PayloadBuilderTests"</gate_command>
<gate_test_selector>Classe `PayloadBuilderTests` do projeto `scripts/openmetadata/RegisterRabbitMq.Tests`</gate_test_selector>
<gate_expected_result>Teste(s) da classe `PayloadBuilderTests` passam (verde); o payload construído para `/v1/services/messagingServices` tem `serviceType: CustomMessaging`, nome `localize-stay-rabbitmq` e tag `localize-stay`; os tópicos `diagnostics.topic` e `notification.diagnostics` são incluídos</gate_expected_result>
<static_evidence>Teste unitário do construtor de payload (sem chamada HTTP real); a chamada real contra `ecad-dev-openmetadata` é verificação manual documentada</static_evidence>
<vertical_slice>N/A — enabling</vertical_slice>
</task_context>

# Tarefa 9.0: Registro do vhost RabbitMQ como CustomMessaging no OpenMetadata (V-06)

## Relacionada as User Stories

- N/A — TechSpec Standalone. Cobre a fatia V-06 (`techspec.md`, Mapa de Fatias Verticais).

## Visão Geral

`ecad-dev-openmetadata` (reaproveitado do projeto `ecad-sba`) passa a catalogar os ativos do Localize
Stay: os 4 schemas do database `localize_stay`, os 3 serviços HTTP (via as specs OpenAPI de 8.0) e a
topologia RabbitMQ do vhost `/localize-stay` (via 6.0). Como o OpenMetadata 2.0.x não tem conector de
ingestão nativo para RabbitMQ, o vhost é registrado manualmente como um serviço `CustomMessaging`
(`localize-stay-rabbitmq`) via API REST — não uma pipeline de ingestion agendada. É
`slice_type: enabling` porque não adiciona comportamento a nenhum serviço de aplicação; só produz um
script de registro e configuração de ingestion cujo resultado observável vive no OpenMetadata, não no
repositório.

## Entrega Observável

- **Entrada ou gatilho:** rodar `scripts/openmetadata/register-rabbitmq` contra a API do
  `ecad-dev-openmetadata` real, com um Personal Access Token de escopo mínimo (gerado no próprio
  OpenMetadata — ver "Questões em Aberto" da TechSpec, já aprovadas para resolução nesta task);
  configurar o ingestion connector Postgres apontando para `postgres-main`, filtrado ao database
  `localize_stay`.
- **Resultado esperado:** a UI do `ecad-dev-openmetadata` lista os 4 schemas, os 3 serviços de API
  (a partir das specs de 8.0) e a fila/exchange de diagnóstico, cada um com owner e tag
  `localize-stay`, visualmente separados dos ativos do `ecad-sba`.
- **Checkpoint de feedback:** teste unitário `PayloadBuilderTests` (sem rede) comprova que o payload
  do registro está correto. Manualmente (fora do gate automatizado): inspecionar a UI do
  `ecad-dev-openmetadata` real e confirmar os 4 schemas + 3 serviços + 1 serviço `CustomMessaging`
  com 2 tópicos, todos com tag `localize-stay`.
- **Seletor focalizado:** `RegisterRabbitMq.PayloadBuilderTests`
- **Fora deste checkpoint:** nenhuma alteração em configuração global do OpenMetadata ou do
  `ecad-sba`; nenhuma automação agendada de ingestion para o `CustomMessaging` (é registro pontual,
  já que não há conector nativo).

## Requisitos

- Ingestion connector Postgres consultando `postgres-main`, filtrado ao database `localize_stay`
  (configurado no próprio `ecad-dev-openmetadata`, via UI ou YAML de ingestion — não é código deste
  repositório, mas o YAML/config usado deve ser versionado em `scripts/openmetadata/` para
  reprodutibilidade).
- Specs OpenAPI de 8.0 registradas como serviços de API no OpenMetadata.
- Script `register-rabbitmq` registra o vhost `/localize-stay` como serviço `CustomMessaging`
  chamado `localize-stay-rabbitmq`, com um tópico por exchange/fila real (`diagnostics.topic`,
  `notification.diagnostics`), via `/v1/services/messagingServices` + `/v1/topics`.
- Toda entidade criada (schemas, serviços, tópicos) recebe tag/owner `localize-stay`, para não
  confundir com os ativos do `ecad-sba` no mesmo catálogo.
- Credencial de ingestion com escopo de leitura; Personal Access Token com escopo de escrita mínimo
  para o script de registro — nenhum reaproveita o token administrativo do Coolify.

## Arquivos Envolvidos

- **Criar:**
  - `scripts/openmetadata/register-rabbitmq/PayloadBuilder.cs` (ou equivalente — constrói o JSON de
    `/v1/services/messagingServices` e `/v1/topics`)
  - `scripts/openmetadata/register-rabbitmq/Program.cs` (executa a chamada HTTP real contra
    `ecad-dev-openmetadata`, usando o PAT via variável de ambiente)
  - `scripts/openmetadata/RegisterRabbitMq.Tests/PayloadBuilderTests.cs` (+ `.csproj`)
  - `scripts/openmetadata/ingestion-postgres.yaml` (config de ingestion versionada para
    reprodutibilidade, referenciando `postgres-main`/`localize_stay`)
- **Modificar:**
  - Nenhum.
- **Referência:**
  - `contracts/openapi/*.json` (task 8.0) — specs a registrar como serviços de API
  - `docs/adr/adr-002-broker-fase0-rabbitmq.md` — nomes de exchange/fila
  - `/home/tsgomes/github-tassosgomes/infra/AGENTS.md` — convenções de acesso ao `infra`
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — credenciais via variável de ambiente, nunca hardcoded

## Subtarefas

- [ ] 9.1 Implementar `PayloadBuilder` (payload de `/v1/services/messagingServices` com
      `serviceType: CustomMessaging`, nome `localize-stay-rabbitmq`, tag `localize-stay`) e o payload
      dos 2 tópicos (`diagnostics.topic`, `notification.diagnostics`)
- [ ] 9.2 Implementar `Program.cs` que executa a chamada HTTP real usando um PAT lido de variável de
      ambiente (nunca hardcoded)
- [ ] 9.3 Criar `PayloadBuilderTests` cobrindo a construção do payload (sem chamada de rede)
- [ ] 9.4 Documentar/versionar `ingestion-postgres.yaml` para o connector Postgres filtrado a
      `localize_stay`
- [ ] 9.5 Gerar o PAT de escopo mínimo no `ecad-dev-openmetadata` real, rodar o script e confirmar
      manualmente na UI que os 4 schemas + 3 serviços + `CustomMessaging` aparecem com tag
      `localize-stay` — fora do gate automatizado, registrar evidência

## Sequenciamento

- Bloqueado por: 3.0, 4.0, 5.0 (schemas a catalogar), 6.0 (topologia RabbitMQ a registrar), 8.0
  (specs OpenAPI a registrar)
- Desbloqueia: Nenhuma (última task do plano)
- Paralelizável: Não (depende de todas as fatias anteriores)

## Rastreabilidade

- Esta tarefa cobre: Fatia V-06 da TechSpec, incluindo a resolução das duas pendências de "Questões
  em Aberto" (disponibilidade de `CustomMessaging` na build 2.0.1 e nome do PAT), já aprovadas para
  resolução durante esta task.
- Evidência esperada: `PayloadBuilderTests` verde; confirmação visual na UI do
  `ecad-dev-openmetadata` real (evidência manual).

## Detalhes de Implementação

Da TechSpec (`techspec.md`, "Registro do RabbitMQ no OpenMetadata (V-06)"):

> OpenMetadata 2.0.x não tem conector de ingestão nativo para RabbitMQ [...]. O schema de serviços de
> mensageria do OpenMetadata inclui, porém, um tipo genérico `CustomMessaging`, e a API REST
> (`/v1/services/messagingServices` + `/v1/topics`) permite registrar manualmente um serviço e seus
> tópicos sem um ingestion connector automático. V-06 registra o vhost `/localize-stay` como um
> serviço `CustomMessaging` chamado `localize-stay-rabbitmq`, com um tópico por exchange/fila real
> (`diagnostics.topic`, `notification.diagnostics`), via um script simples contra essa API.

Da TechSpec ("Questões em Aberto", já aprovadas para resolução durante esta task):

> - Confirmar que o tipo de serviço `CustomMessaging` está de fato disponível na build 2.0.1 [...]
>   se não estiver presente, o registro pode precisar de um workaround (ex.: registrar como Kafka
>   genérico) ou upgrade do OpenMetadata.
> - Definir o nome exato do Personal Access Token/credencial [...] gerado no próprio OpenMetadata,
>   com escopo mínimo, não reaproveitar o token administrativo do Coolify.

Se `CustomMessaging` não estiver disponível na build instalada, o implementer deve aplicar o
workaround documentado (registrar como serviço `Kafka` genérico, mantendo a tag `localize-stay` para
não confundir com ativos reais de Kafka) e registrar essa decisão nas notas de execução da task —
isso não é uma ambiguidade bloqueante, é uma decisão já delegada pela TechSpec ao momento da
execução.

**Convenções da stack:** credencial (PAT) sempre via variável de ambiente, nunca versionada — mesmo
princípio já aplicado às connection strings do Postgres (tasks 2.0/3.0/4.0/5.0).

## Prontidão para Implementação

- **Decisões fechadas:** nome do serviço `localize-stay-rabbitmq`; tag/owner `localize-stay` em toda
  entidade criada; PAT nunca reaproveita o token administrativo do Coolify.
- **Limites de decisão do implementer:** linguagem/stack exata do script de registro (C# é a
  recomendação por consistência com o resto do repositório, mas outra linguagem já disponível no
  ambiente é aceitável); workaround de `CustomMessaging` indisponível (registrar como Kafka
  genérico), se necessário, conforme decisão já delegada pela TechSpec.
- **Dependências disponíveis:** specs OpenAPI (8.0), topologia RabbitMQ provada (6.0), schemas
  provisionados (2.0, materializados em 3.0/4.0/5.0).
- **Artefatos exigidos pelo gate:** `PayloadBuilderTests.cs` é criado nesta própria task e não
  depende de rede.
- **Dependências futuras:** Nenhuma — última task do plano.
- **Ambiguidades bloqueantes:** Nenhuma — as duas pendências da TechSpec já têm resolução delegada
  documentada acima.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~RegisterRabbitMq.Tests.PayloadBuilderTests"`
- [ ] O seletor encontra pelo menos um teste e não executa casos sem relação com esta task
- [ ] Build compila sem erros: `dotnet build scripts/openmetadata/RegisterRabbitMq.Tests`
- [ ] Payload construído tem `serviceType: CustomMessaging` (ou o workaround documentado), nome
      `localize-stay-rabbitmq` e tag `localize-stay`
- [ ] Payload inclui os 2 tópicos (`diagnostics.topic`, `notification.diagnostics`)
- [ ] PAT é lido de variável de ambiente, nunca hardcoded no script versionado
- [ ] Checkpoint de feedback executado: script rodado contra `ecad-dev-openmetadata` real; UI mostra
      os 4 schemas + 3 serviços + `CustomMessaging` com tag `localize-stay` (evidência manual)
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para validar esta task
- [ ] A evidência acima prova somente o registro/payload correto e não depende de nenhuma task futura
      (é a última do plano)
