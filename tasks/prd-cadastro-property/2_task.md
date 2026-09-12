---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: [1.0, "prd-fundacao-fase0/9.0"]
---

<task_context>
<domain>services/catalog/properties</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database,http_server</dependencies>
<unblocks>"5.0; F02, F03 e F07"</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="FeatureSlice=PropertyUpdate"` executa ao menos um teste e prova PATCH 200/400/403/404/500, estado preservado nas falhas e operação `updateProperty` compatível; depois, reingestão manual mostra as duas operações no OpenMetadata</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="FeatureSlice=PropertyUpdate"</gate_command>
<gate_test_selector>Trait xUnit `FeatureSlice=PropertyUpdate` em `UpdatePropertyTests` e nos casos de edição de `PropertyOpenApiContractTests`</gate_test_selector>
<gate_expected_result>Filtro encontra testes; todos passam; 0 falhas; PATCH válido persiste somente campos presentes e todos os erros preservam o estado anterior; OpenAPI gerado contém as duas operações compatíveis</gate_expected_result>
<static_evidence>N/A — behavioral; a reingestão no OpenMetadata é checkpoint manual adicional e não substitui o gate</static_evidence>
<vertical_slice>Host responsável edita nome e/ou localização por PATCH; identidade, Host e status permanecem imutáveis, e validação, ausência, ownership divergente ou falha interna não alteram o cadastro</vertical_slice>
</task_context>

# Tarefa 2.0: Editar dados cadastrais preservando ownership e estado

## Relacionada às User Stories

- Host corrige nome ou localização da hospedagem (cobertura direta no backend; UI na task 5.0).
- Autor observa respostas inequívocas de sucesso, validação, ausência e ownership (cobertura direta por HTTP/testes).

## Visão Geral

Completa RF-02 sobre os artefatos da criação. A fatia distingue omissão de `null` no PATCH, carrega
a entidade tracked, verifica existência antes de ownership, altera somente campos presentes e fecha
o contrato/export OpenAPI e a catalogação das operações.

## Entrega Observável

- **Entrada ou gatilho:** `PATCH /v1/properties/{propertyId}`, header UUID do Host e JSON com `name` e/ou `location`.
- **Resultado esperado:** 200 com dados atualizados e ID/Host/status preservados; 400/403/404/500 contratuais mantêm o estado anterior.
- **Checkpoint de feedback:** gate focalizado; depois, reingestão manual do export e confirmação de `createProperty`/`updateProperty` no OpenMetadata.
- **Seletor focalizado:** trait xUnit `FeatureSlice=PropertyUpdate`.
- **Fora deste checkpoint:** UI (task 5.0), consulta/listagem, transferência de Host, ativação/desativação e concorrência otimista.

## Requisitos

- Distinguir campo omitido de `null`; exigir ao menos um campo conhecido; rejeitar membros desconhecidos e campos imutáveis.
- Validar `propertyId`/Host UUID, limites 120/500, vazio/branco sem trim; inexistência precede ownership.
- Atualizar entidade tracked e executar um único save; qualquer falha preserva nome/localização/ID/Host/status anteriores.
- Mapear 403 `HOST_OWNERSHIP_FORBIDDEN`, 404 `PROPERTY_NOT_FOUND` e manter 400/500 no ProblemDetails completo.
- Registrar `PropertyUpdated` e warning de ownership com IDs/trace, sem nome/localização/body.
- Manter last-write-wins; não introduzir ETag, row version ou 409.

## Arquivos Envolvidos

- **Criar:**
  - `services/catalog/src/3-Domain/LocalizeStay.Catalog.Domain/Properties/HostOwnershipForbiddenException.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Properties/PropertyNotFoundException.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Properties/Models/UpdatePropertyInput.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Properties/Validators/UpdatePropertyInputValidator.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Contracts/Properties/UpdatePropertyRequest.cs`
  - `services/catalog/tests/LocalizeStay.Catalog.IntegrationTests/Properties/UpdatePropertyTests.cs`
- **Modificar:**
  - `services/catalog/src/3-Domain/LocalizeStay.Catalog.Domain/Properties/Property.cs` (ownership e update atômico)
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Properties/IPropertyService.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Properties/PropertyService.cs`
  - `services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/Persistence/Repositories/PropertyRepository.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Controllers/PropertiesController.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/ErrorHandling/CatalogExceptionHandler.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Extensions/SwaggerExtensions.cs`
  - `services/catalog/tests/LocalizeStay.Catalog.UnitTests/Properties/PropertyTests.cs`
  - `services/catalog/tests/LocalizeStay.Catalog.UnitTests/Properties/PropertyValidatorsTests.cs`
  - `services/catalog/tests/LocalizeStay.Catalog.IntegrationTests/Contracts/PropertyOpenApiContractTests.cs`
  - `contracts/openapi/catalog.json` (export final com as duas operações)
- **Ação externa sem arquivo novo:** reexecutar a ingestion do API Service Catalog já criada pela Fundação 9.0, usando `contracts/openapi/catalog.json`, e registrar evidência do OpenMetadata.
- **Referência:**
  - `tasks/prd-cadastro-property/{prd.md,techspec.md,api-contract.yaml,api-contract.md}`
  - `context/architecture-baseline.md`, `domains/catalog/domain.md`, `docs/adr/adr-001-backend-stack-dotnet.md`
  - artefatos produzidos pela task 1.0.
- **Skills para consultar durante implementação:**
  - `dotnet-architecture`, `dotnet-dependency-config`, `dotnet-program-setup`, `dotnet-testing`, `dotnet-observability`, `test-guide`.

## Subtarefas

- [ ] 2.1 Implementar presença explícita do PATCH, input/validator e rejeição de membros desconhecidos/imutáveis.
- [ ] 2.2 Implementar load tracked, not-found, ownership e alteração do aggregate sem estado parcial.
- [ ] 2.3 Expor endpoint PATCH fino, propagar cancelamento e completar ProblemDetails 403/404/500.
- [ ] 2.4 Adicionar logs de sucesso/rejeição seguros e preservar health checks/configuração existentes.
- [ ] 2.5 Criar testes unitários de branches e integração/contrato com trait `PropertyUpdate`, cobrindo todos os cenários RF-02.
- [ ] 2.6 Executar gate, atualizar/validar export final e reingerir manualmente o API Service no OpenMetadata.

## Sequenciamento

- Bloqueado por: 1.0 e Fundação 9.0 (configuração/scripts de catalogação a reutilizar).
- Desbloqueia: 5.0 e dependências de produto F02/F03/F07 no backend.
- Paralelizável: Não; modifica aggregate, service, repository, controller, handler e testes criados por 1.0.

## Rastreabilidade

- Cobre RF-02, DP-02, fronteira de F07 e a operação `updateProperty` do contrato 1.0.0.
- Evidência: 200 para um/dois campos; 400 para corpo/presença/limites/JSON; 403 Host divergente; 404 ID ausente; 500 sanitizado; estado anterior preservado.

## Detalhes de Implementação

Fluxo fechado: Controller adapta path/header e presença do JSON → `IPropertyService.UpdateAsync` →
`IPropertyRepository.GetByIdForUpdateAsync` → not-found → `Property.EnsureOwnedBy` →
`UpdateDetails` → um `SaveChangesAsync` → 200. A leitura é tracked por ser comando.

`UpdatePropertyInput` carrega presença e valor separados para cada campo. O mecanismo pode ser
setters que registram presença ou `Optional<T>` pequeno restrito à borda HTTP; `string?` isolado não
é suficiente. A entidade valida ambos os novos valores antes de atribuir qualquer um. ID,
`HostReferenceId` e `Status` não entram no schema aceito.

Testes de integração consultam o banco depois de cada erro. Use override controlado da Unit of Work
para 500; não teste wiring/mocks do repository. Todo teste da fatia recebe
`[Trait("FeatureSlice", "PropertyUpdate")]`; os casos adicionados ao contract test usam a mesma trait.

## Prontidão para Implementação

- **Decisões fechadas:** PATCH parcial atômico; presença distinta de null; existência antes de ownership; Host/ID/status imutáveis; last-write-wins; códigos/shape de erro; export e reingestão final.
- **Limites de decisão do implementer:** escolher entre setters com presença e `Optional<T>` local; seguir o mecanismo mais simples que mantém o schema e passa os testes.
- **Dependências disponíveis:** todos os artefatos de 1.0 e suas pré-condições; OpenMetadata real é necessário somente para o checkpoint manual pós-gate.
- **Artefatos exigidos pelo gate:** `UpdatePropertyTests.cs` é criado nesta task; `PropertyOpenApiContractTests.cs`, factory, migration e fixture existem desde 1.0 e são modificados quando necessário.
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma; o frontend aprovado é coberto pelas tasks 3.0–5.0.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `scripts/ai-flow/gate.sh --filter="FeatureSlice=PropertyUpdate"`.
- [ ] O seletor encontra ao menos um teste e não executa casos exclusivos da criação.
- [ ] Build compila sem warnings/erros pelo gate.
- [ ] PATCH de nome, localização ou ambos retorna 200 e preserva ID/Host/status.
- [ ] Corpo vazio, null, branco, excesso, UUID inválido, campo desconhecido ou imutável retorna 400 e preserva o estado.
- [ ] Host divergente retorna 403; ID inexistente retorna 404; nenhuma falha executa commit útil.
- [ ] 500 controlado é sanitizado e deixa o estado anterior intacto.
- [ ] Logs são estruturados/correlacionáveis e não contêm nome, localização ou body.
- [ ] OpenAPI gerado/exportado é compatível com todas as respostas e schemas do contrato 1.0.0.
- [ ] OpenMetadata mostra `createProperty` e `updateProperty` com versão/ownership esperados (evidência manual adicional).
- [ ] Todos os artefatos do gate existem antes da task ou são criados/modificados nela; nenhum vem de task futura.
- [ ] Validator focused da task aprova no perfil standard.
