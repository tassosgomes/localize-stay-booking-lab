# Task 2.0 — revisão focused (primeira)

- **Modo:** focused (não é revalidação)
- **Escopo:** PATCH `updateProperty` (RF-02 / V-02). UI, consulta/listagem, transferência de Host, F07 e concorrência otimista fora de escopo.
- **Diff base:** `2a9d47fe14c616b92b6f3c4960418cea554e86a9` (checkpoint task 1.0; working tree + untracked da fatia)
- **HEAD durante a revisão:** `2a9d47fe14c616b92b6f3c4960418cea554e86a9` (`feature/prd-cadastro-property`) — inalterado
- **Independência:** worker fresco desta runtime; não é segundo revisor humano. Gate reexecutado; aprovação do implementer não foi reutilizada.

## Gate

```bash
scripts/ai-flow/gate.sh --filter="FeatureSlice=PropertyUpdate"
```

- **Resultado:** exit 0 — `GATE: APROVADO`
- **Testes:** `FeatureSlice=PropertyUpdate=42`, 0 falhas
- **Build/format:** ok (`TreatWarningsAsErrors` herdado; 18 arquivos .NET no escopo do format)

## O que foi revisado

Contrato da task, diff desde o checkpoint, untracked da fatia (`UpdatePropertyRequest`, `UpdatePropertyInput`/`Validator`, `HostOwnershipForbiddenException`, `PropertyNotFoundException`, `UpdatePropertyTests`) e skills `dotnet-architecture`, `dotnet-dependency-config`, `dotnet-program-setup`, `dotnet-testing`, `dotnet-observability`, `test-guide`. Trechos de `api-contract.yaml` / techspec só para conferir PATCH, presença vs `null`, 403/404 e `updateProperty`.

Comportamento coberto: 200 com nome, localização ou ambos, sem trim, ID/Host/status imutáveis; 400 para `{}`/`null`/vazio/branco/limites/UUID/JSON desconhecido ou imutável com estado preservado; 403 ownership com warning sem payload; 404 existência antes de ownership; 500 sanitizado via UoW controlada; um `SaveChangesAsync` tracked; `UpdateDetails` valida os dois valores antes de atribuir; CT até o EF; Service Pattern; Domain sem EF/ASP.NET; export OpenAPI com as duas operações e `application/problem+json`.

OpenMetadata: `OPENMETADATA_PAT`/`OPENMETADATA_URL` ausentes neste ambiente. Reingestão é checkpoint manual adicional e não substitui o gate.

## Bloqueantes

Nenhum.

## Recomendações (3)

1. **Reingestão OpenMetadata** (checkpoint manual pós-gate): não há evidência de `createProperty`/`updateProperty` no catálogo nesta revisão. Sem credenciais, não é bloqueio; repetir a ingestion de `contracts/openapi/catalog.json` quando o PAT estiver disponível.
2. **Warning de ownership duplicado** (`PropertyService.UpdateAsync` ~L56–64 e `CatalogExceptionHandler` ~L60–67): o mesmo 403 gera dois logs `HOST_OWNERSHIP_FORBIDDEN` com IDs/trace. Manter o warning da Application (exigido) e evitar o segundo no handler, ou o inverso, para não poluir correlação.
3. **Trait herdada nos unitários** (`PropertyTests.cs` L7, `PropertyValidatorsTests.cs` L9): a classe permanece `FeatureSlice=PropertyCreate`, então os casos de edição também entram no filtro da criação. Isolar os testes de update (classe própria ou só trait de método) para o seletor da V-01 ficar exclusivo.

## Veredito

VALIDAÇÃO APROVADA (3 recomendações)
