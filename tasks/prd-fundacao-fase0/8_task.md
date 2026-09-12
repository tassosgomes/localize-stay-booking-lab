---
status: done
slice_type: enabling
verification_type: static
parallelizable: false
blocked_by: [3.0, 4.0, 5.0, 6.0]
---

<task_context>
<domain>contracts</domain>
<type>documentation</type>
<scope>configuration</scope>
<complexity>low</complexity>
<dependencies>http_server</dependencies>
<unblocks>"9.0"</unblocks>
<feedback_checkpoint>`swagger-cli validate` nos 3 JSON de `contracts/openapi/` e `asyncapi validate` em `contracts/asyncapi/diagnostics-v1.yaml` retornam sucesso</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --static</gate_command>
<gate_test_selector>N/A — habilitador static: contrato comum que fixa o formato de exportação para os PRDs de negócio futuros (ver `references/vertical-slicing.md`, "contrato comum que fixa tipos para várias fatias")</gate_test_selector>
<gate_expected_result>`swagger-cli validate contracts/openapi/catalog.json contracts/openapi/booking.json contracts/openapi/payment.json` e `asyncapi validate contracts/asyncapi/diagnostics-v1.yaml` retornam 0 erros</gate_expected_result>
<static_evidence>Saída de `swagger-cli validate` (ou equivalente já usado pelo autor) e `asyncapi validate` sem erros para os 4 arquivos gerados</static_evidence>
<vertical_slice>N/A — enabling</vertical_slice>
</task_context>

# Tarefa 8.0: Contratos exportados e validados — OpenAPI, AsyncAPI, Data Contract (V-05)

## Relacionada as User Stories

- N/A — TechSpec Standalone. Cobre a fatia V-05 (`techspec.md`, Mapa de Fatias Verticais).

## Visão Geral

Estabelece a convenção de local e o primeiro artefato real de cada um dos três estilos de contrato
(OpenAPI, AsyncAPI, Data Contract), prontos para os PRDs de negócio publicarem os contratos reais no
mesmo lugar. É `slice_type: enabling` (não vertical): não adiciona comportamento novo em nenhum
serviço — apenas exporta e valida o que os serviços já expõem (Swagger de cada um, topologia de
mensageria de V-03), fixando um contrato comum que os próximos PRDs de domínio vão estender.

## Entrega Observável

- **Entrada ou gatilho:** script/comando de export do Swagger de cada serviço (Catalog, Booking,
  Payment já rodando) e escrita manual do AsyncAPI/Data Contract.
- **Resultado esperado:** `contracts/openapi/{catalog,booking,payment}.json` são documentos OpenAPI
  válidos; `contracts/asyncapi/diagnostics-v1.yaml` descreve `DiagnosticPing` corretamente;
  `contracts/data-contracts/TEMPLATE.md` está pronto para `available_accommodations_v1`/
  `reservation_calendar_v1` futuros.
- **Checkpoint de feedback:** `swagger-cli validate` nos 3 JSON e `asyncapi validate` no YAML — 0
  erros.
- **Seletor focalizado:** N/A (enabling static).
- **Fora deste checkpoint:** nenhum contrato de negócio real (endpoints/eventos de catálogo, reserva
  ou pagamento) — isso é escopo de `tsg-flow-contract-creator` quando o primeiro PRD de domínio
  existir.

## Requisitos

- `contracts/openapi/*.json` exportados do Swagger real de cada serviço (não escritos à mão).
- `contracts/asyncapi/diagnostics-v1.yaml` documenta a exchange `diagnostics.topic`, a fila
  `notification.diagnostics` e o payload `DiagnosticPing` (incluindo `correlationId`/`causationId`)
  provados na task 6.0.
- `contracts/data-contracts/TEMPLATE.md` é um template reutilizável (sem dataset real ainda) para os
  futuros `available_accommodations_v1`/`reservation_calendar_v1`.
- Todos os 3 estilos de contrato validam com uma ferramenta de lint apropriada.

## Arquivos Envolvidos

- **Criar:**
  - `contracts/openapi/catalog.json`
  - `contracts/openapi/booking.json`
  - `contracts/openapi/payment.json`
  - `contracts/asyncapi/diagnostics-v1.yaml`
  - `contracts/data-contracts/TEMPLATE.md`
- **Modificar:**
  - Nenhum.
- **Referência:**
  - `services/{catalog,booking,payment}/**` (tasks 3.0/4.0/5.0) — endpoint `/swagger` de cada serviço,
    fonte do export
  - `services/booking/.../MessagingExtensions.cs`, `DiagnosticsEndpoints.cs` (task 6.0) — topologia e
    payload a documentar em AsyncAPI
  - `docs/adr/adr-002-broker-fase0-rabbitmq.md` — convenção de nomes de evento
- **Skills para consultar durante implementação:**
  - `dotnet-program-setup` — geração do documento OpenAPI pelo Swashbuckle já configurado nas tasks
    3.0/4.0/5.0

## Subtarefas

- [ ] 8.1 Exportar `contracts/openapi/{catalog,booking,payment}.json` a partir do `/swagger` de cada
      serviço rodando localmente
- [ ] 8.2 Escrever `contracts/asyncapi/diagnostics-v1.yaml` documentando a topologia e o payload de
      diagnóstico provados na task 6.0
- [ ] 8.3 Escrever `contracts/data-contracts/TEMPLATE.md`
- [ ] 8.4 Validar os 4 artefatos (`swagger-cli validate` nos 3 JSON, `asyncapi validate` no YAML) e
      registrar a evidência

## Sequenciamento

- Bloqueado por: 3.0, 4.0, 5.0 (Swagger dos 3 serviços precisa existir), 6.0 (topologia RabbitMQ
  precisa existir para ser documentada)
- Desbloqueia: 9.0 (OpenMetadata registra os serviços a partir destas specs OpenAPI)
- Paralelizável: Não (depende de todas as fatias de serviço anteriores)

## Rastreabilidade

- Esta tarefa cobre: Fatia V-05 da TechSpec.
- Evidência esperada: `swagger-cli validate` e `asyncapi validate` sem erros nos 4 artefatos.

## Detalhes de Implementação

Da TechSpec (`techspec.md`, "Abordagem de Testes" → "Testes de Contrato"):

> - `contracts/openapi/*.json` validados com um linter de OpenAPI (ex.: `swagger-cli validate` ou
>   equivalente já usado pelo autor) para garantir que o export de cada serviço é um documento válido.
> - `contracts/asyncapi/diagnostics-v1.yaml` validado com `asyncapi validate` (ou ferramenta
>   equivalente), estabelecendo o padrão que os PRDs de negócio devem seguir para os eventos reais.

**Convenções da stack:** nenhuma nova — apenas exporta o que Swashbuckle (task 3.0/4.0/5.0) já gera.

## Prontidão para Implementação

- **Decisões fechadas:** convenção de local (`contracts/openapi/`, `contracts/asyncapi/`,
  `contracts/data-contracts/`) já fixada pela TechSpec; nomes de arquivo (`catalog.json`,
  `booking.json`, `payment.json`, `diagnostics-v1.yaml`, `TEMPLATE.md`).
- **Limites de decisão do implementer:** ferramenta exata de export do Swagger (script `curl` +
  `jq`, ou `dotnet swagger tofile`); ferramenta exata de lint (`swagger-cli` ou equivalente já
  disponível no ambiente).
- **Dependências disponíveis:** os 3 serviços rodando localmente com Swagger habilitado (tasks
  3.0/4.0/5.0); topologia de mensageria provada (task 6.0).
- **Artefatos exigidos pelo gate:** os 5 arquivos de contrato são criados nesta própria task.
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: N/A — substituído por evidência estática (ver abaixo)
- [ ] `swagger-cli validate contracts/openapi/catalog.json contracts/openapi/booking.json contracts/openapi/payment.json` retorna 0 erros
- [ ] `asyncapi validate contracts/asyncapi/diagnostics-v1.yaml` retorna 0 erros
- [ ] `contracts/asyncapi/diagnostics-v1.yaml` documenta `correlationId`/`causationId` no payload
- [ ] `contracts/data-contracts/TEMPLATE.md` não referencia nenhum dataset real ainda (é um template
      genérico)
- [ ] Checkpoint de feedback executado: `swagger-cli validate` + `asyncapi validate` → 0 erros
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para validar esta task
- [ ] A evidência acima prova somente a validade dos contratos exportados e não depende da
      catalogação no OpenMetadata (9.0)
