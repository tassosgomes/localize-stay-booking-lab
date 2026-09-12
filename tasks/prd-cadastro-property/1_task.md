---
status: done
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: ["prd-fundacao-fase0/1.0", "prd-fundacao-fase0/2.0", "prd-fundacao-fase0/3.0", "prd-fundacao-fase0/8.0"]
---

<task_context>
<domain>services/catalog/properties</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database,http_server</dependencies>
<unblocks>"2.0"</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="FeatureSlice=PropertyCreate"` executa ao menos um teste xUnit e prova 201/400/500, persistência atômica, duplicata aceita e operação `createProperty` compatível com o OpenAPI</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="FeatureSlice=PropertyCreate"</gate_command>
<gate_test_selector>Trait xUnit `FeatureSlice=PropertyCreate` em `CreatePropertyTests` e nos casos de criação de `PropertyOpenApiContractTests`</gate_test_selector>
<gate_expected_result>Filtro encontra testes; todos passam; 0 falhas; PostgreSQL Testcontainers contém somente as Properties dos requests aceitos; OpenAPI gerado contém `createProperty` compatível</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Host cadastra uma Property ativa por POST; requests inválidos não persistem, duplicatas de nome/localização são aceitas e falhas retornam ProblemDetails contratual correlacionável</vertical_slice>
</task_context>

# Tarefa 1.0: Cadastrar Property ativa por HTTP

## Relacionada às User Stories

- Host cadastra hospedagem com dados básicos (cobertura direta no backend; UI nas tasks 4.0/5.0).
- Autor observa respostas inequívocas para cadastro válido e inválido (cobertura direta por HTTP/testes).

## Visão Geral

Entrega RF-01 ponta a ponta sobre a Fundação: contrato HTTP, validação, aggregate, persistência no
schema `catalog`, resposta, erros, logging seguro e testes. É `high` somente pelo acoplamento
irredutível da primeira fatia através de muitos artefatos; a lógica e o risco funcional permanecem
simples. A revisão deste plano é obrigatória antes do implementer.

## Entrega Observável

- **Entrada ou gatilho:** `POST /v1/properties`, header `X-Host-Reference-Id` UUID e JSON com `name`/`location`.
- **Resultado esperado:** 201 com `Location`, UUID estável, Host informado e status `active`; 400/500 seguem ProblemDetails e não deixam persistência parcial; nomes/localizações repetidos geram IDs distintos.
- **Checkpoint de feedback:** gate focalizado com PostgreSQL Testcontainers e OpenAPI gerado.
- **Seletor focalizado:** trait xUnit `FeatureSlice=PropertyCreate`.
- **Fora deste checkpoint:** edição, ownership divergente, 403/404, UI (tasks 4.0/5.0) e consulta pública.

## Requisitos

- Aplicar limites `name` 120 e `location` 500, rejeitando ausente, null, vazio, branco e JSON desconhecido sem aplicar trim.
- Gerar `Guid` na criação; preservar valores aceitos; iniciar `PropertyStatus.Active`; não consultar duplicidade.
- Executar exatamente um `SaveChangesAsync` por sucesso e propagar `CancellationToken` até EF Core.
- Mapear binding/validation para 400 `VALIDATION_ERROR` e falhas inesperadas para 500 `INTERNAL_ERROR`, sempre com `details` array e `traceId`, sem stack trace.
- Registrar `PropertyCreated` com IDs/operação/trace; nunca nome, localização ou body completo.
- Preservar `/health/live` e `/health/ready`; não adicionar autenticação, eventos, cache ou OpenTelemetry.

## Arquivos Envolvidos

- **Criar:**
  - `services/catalog/src/3-Domain/LocalizeStay.Catalog.Domain/Properties/Property.cs`
  - `services/catalog/src/3-Domain/LocalizeStay.Catalog.Domain/Properties/PropertyStatus.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Abstractions/Persistence/IPropertyRepository.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Abstractions/Persistence/IUnitOfWork.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Properties/IPropertyService.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Properties/PropertyService.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Properties/Models/CreatePropertyInput.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Properties/Models/PropertyResult.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/Properties/Validators/CreatePropertyInputValidator.cs`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/DependencyInjection.cs`
  - `services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/Persistence/Configurations/PropertyConfiguration.cs`
  - `services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/Persistence/Repositories/PropertyRepository.cs`
  - `services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/Persistence/CatalogUnitOfWork.cs`
  - `services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/Persistence/Migrations/*_AddProperties.cs` e snapshot/designer EF associados
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Contracts/Properties/CreatePropertyRequest.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Contracts/Properties/PropertyResponse.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Controllers/PropertiesController.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/ErrorHandling/CatalogExceptionHandler.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Extensions/ApplicationExtensions.cs`
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Extensions/ErrorHandlingExtensions.cs`
  - `services/catalog/tests/LocalizeStay.Catalog.UnitTests/LocalizeStay.Catalog.UnitTests.csproj`
  - `services/catalog/tests/LocalizeStay.Catalog.UnitTests/Properties/PropertyTests.cs`
  - `services/catalog/tests/LocalizeStay.Catalog.UnitTests/Properties/PropertyValidatorsTests.cs`
  - `services/catalog/tests/LocalizeStay.Catalog.IntegrationTests/Properties/CreatePropertyTests.cs`
  - `services/catalog/tests/LocalizeStay.Catalog.IntegrationTests/Contracts/PropertyOpenApiContractTests.cs`
- **Modificar:**
  - `Directory.Packages.props` (somente FluentValidation/DI/logging ausentes; sem upgrade amplo)
  - `services/catalog/LocalizeStay.Catalog.sln` (adicionar UnitTests)
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Program.cs` (somente encadear extensões)
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Extensions/SwaggerExtensions.cs` (operationId/schema/base path da criação)
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Extensions/PersistenceExtensions.cs` (repository/UoW)
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/LocalizeStay.Catalog.Api.csproj`
  - `services/catalog/src/2-Application/LocalizeStay.Catalog.Application/LocalizeStay.Catalog.Application.csproj`
  - `services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/LocalizeStay.Catalog.Infra.csproj`
  - `services/catalog/src/4-Infra/LocalizeStay.Catalog.Infra/Persistence/CatalogDbContext.cs` (DbSet e configurations)
  - `services/catalog/tests/LocalizeStay.Catalog.IntegrationTests/CustomWebApplicationFactory.cs` e respectivo `.csproj` (migration, isolamento e override controlado de UoW)
  - `contracts/openapi/catalog.json` (export-base produzido pela Fundação 8.0; incluir `createProperty` após o gate)
- **Referência:**
  - `tasks/prd-cadastro-property/{prd.md,techspec.md,api-contract.yaml,api-contract.md}`
  - `context/architecture-baseline.md`, `domains/catalog/domain.md`, `docs/adr/adr-001-backend-stack-dotnet.md`
  - artefatos concluídos pelas tasks Fundação 1.0, 2.0, 3.0 e 8.0
- **Skills para consultar durante implementação:**
  - `dotnet-architecture`, `dotnet-dependency-config`, `dotnet-program-setup`, `dotnet-testing`, `dotnet-observability`, `test-guide`.

## Subtarefas

- [ ] 1.1 Criar aggregate, status, inputs/result, invariantes e validator da criação.
- [ ] 1.2 Implementar portas/adaptadores EF, mapping, Unit of Work e migration `AddProperties`, removendo a sentinela apenas se estiver no snapshot real.
- [ ] 1.3 Implementar Service Pattern, DTOs/controller, JSON estrito, DI e `Program.cs` por extensões.
- [ ] 1.4 Implementar ProblemDetails 400/500 e logs estruturados seguros/correlacionáveis.
- [ ] 1.5 Criar testes unitários somente para invariantes/branches e integração/contrato com trait `PropertyCreate` para todos os cenários RF-01.
- [ ] 1.6 Executar o gate focalizado, confirmar que seleciona testes e atualizar `contracts/openapi/catalog.json` somente após sucesso.

## Sequenciamento

- Bloqueado por: Fundação 1.0 (convenções), 2.0 (schema/role), 3.0 (solution/DbContext/fixture-base) e 8.0 (export OpenAPI); `gate.sh` criado é pré-condição operacional.
- Desbloqueia: 2.0 desta F01.
- Paralelizável: Não; produz todos os artefatos centrais consumidos pela edição.

## Rastreabilidade

- Cobre RF-01, DP-01 e a operação `createProperty` do contrato 1.0.0.
- Evidência: 201/Location/body/persistência; 400 para header/body/limites/JSON; 500 sanitizado; duplicata aceita; zero persistência parcial.

## Detalhes de Implementação

Fluxo fechado: Controller adapta header/body → `IPropertyService.CreateAsync` valida →
`Property.Create` aplica invariantes → `IPropertyRepository.AddAsync` → um
`IUnitOfWork.SaveChangesAsync` → mapeamento manual para `PropertyResponse` e 201.

`Property` é o modelo mapeado pelo EF via Fluent API para `catalog.properties`: `id uuid` PK,
`name varchar(120)`, `location varchar(500)`, `host_reference_id uuid`, `status varchar(16)`. Não
criar índice único nem auditoria. Domain não depende de EF/ASP.NET; API depende de Application;
Infra implementa portas da Application. Service Pattern simples permanece suficiente; não usar
MediatR, repository genérico, Domain Service, Factory ou State.

Testes unitários isolam branches de invariantes/limites. Integração prova HTTP + DI + migration +
PostgreSQL real, inclusive estado do banco após erros. Não duplicar assertions triviais entre
camadas. Todo teste da fatia recebe `[Trait("FeatureSlice", "PropertyCreate")]`.

## Prontidão para Implementação

- **Decisões fechadas:** contrato 1.0.0; Controller; Service Pattern simples; entidade EF=aggregate; validação duplicada nas bordas essenciais; UUID; limites; valores sem trim; duplicatas aceitas; sem auth/eventos/cache/OTel.
- **Limites de decisão do implementer:** mecanismo local de mapeamento manual e nomes finais da migration; JSON estrito deve ficar limitado aos DTOs F01 e ser provado.
- **Dependências disponíveis:** somente após Fundação 1.0/2.0/3.0/8.0; contrato e ADR já existem.
- **Artefatos exigidos pelo gate:** `CreatePropertyTests.cs`, casos create de `PropertyOpenApiContractTests.cs` e traits são criados nesta task; factory/projeto-base vêm da Fundação e são modificados aqui; `gate.sh` é pré-condição externa.
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma; o frontend possui tasks próprias 3.0–5.0.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `scripts/ai-flow/gate.sh --filter="FeatureSlice=PropertyCreate"`.
- [ ] O seletor encontra ao menos um teste e não executa casos de edição.
- [ ] Build compila sem warnings/erros pelo gate (`TreatWarningsAsErrors` herdado).
- [ ] 201 inclui `Location`, UUID, dados enviados, Host do header e `active`, com registro persistido.
- [ ] 400 cobre ausente/null/vazio/branco/acima do limite/UUID ou JSON inválido e não persiste nada.
- [ ] Duas solicitações iguais geram duas Properties com IDs distintos.
- [ ] 500 controlado é sanitizado, correlacionável e não deixa commit parcial.
- [ ] Logs não contêm nome, localização, body, secrets ou PII.
- [ ] OpenAPI gerado de `createProperty` é compatível com `api-contract.yaml` e o export valida.
- [ ] Todos os artefatos do gate existem na task ou nas pré-condições; nenhum vem de task futura.
- [ ] Validator focused da task aprova no perfil standard.
