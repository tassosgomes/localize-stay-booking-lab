# Task 1.0 — revisão focused (primeira)

- **Modo:** focused (não é revalidação)
- **Escopo:** POST `createProperty` (RF-01 / V-01). PATCH, UI, 403 e 404 fora de escopo.
- **Diff base:** `5d0f9eff8548db9d8846782c3133743790014825` (sem checkpoint; working tree + untracked da task)
- **HEAD durante a revisão:** `5d0f9eff8548db9d8846782c3133743790014825` (`feature/prd-cadastro-property`) — inalterado
- **Independência:** worker fresco desta runtime; não é segundo revisor humano. Aprovação do implementer não foi reutilizada.

## Gate

```bash
scripts/ai-flow/gate.sh --filter="FeatureSlice=PropertyCreate"
```

- **Resultado:** exit 0 — `GATE: APROVADO`
- **Testes:** `FeatureSlice=PropertyCreate=40`, 0 falhas
- **Build/format:** ok (`TreatWarningsAsErrors` herdado)

## O que foi revisado

Contrato da task, diff desde a base, untracked da fatia (aggregate, Application/Infra/API, migration `AddProperties`, testes, `contracts/openapi/catalog.json`) e skills `dotnet-architecture`, `dotnet-dependency-config`, `dotnet-program-setup`, `dotnet-testing`, `dotnet-observability`, `test-guide`. Trechos de `api-contract.yaml` / techspec só para conferir `createProperty`, ProblemDetails e persistência.

Comportamento coberto: 201 + `Location` + persistência sem trim; duplicata aceita; 400 header/body/limites/JSON desconhecido sem persistir; 500 sanitizado sem commit; log `PropertyCreated` sem nome/localização; health live/ready preservados; um `SaveChangesAsync`; CT até o EF; Service Pattern; Domain sem EF/ASP.NET.

## Bloqueantes

Nenhum.

## Recomendações (3)

1. **OpenAPI export vs contrato-fonte** (`contracts/openapi/catalog.json`): `createProperty` existe (path, `operationId`, header UUID, schemas), mas o export documenta 400/500 como `application/json` (runtime e YAML usam `application/problem+json`), não declara o header `Location` do 201 e deixa `CreatePropertyRequest.name/location` nullable/sem `required`. Aceitável em V-01; fechar na conformidade OpenAPI da V-02.
2. **Mensagens de JSON inválido** (`CatalogProblemDetailsFactory.FromJsonException`, ~L79–88; `FromModelState` ~L64–66): `JsonException.Message` / `Exception.Message` podem vazar nome de tipo CLR. Preferir mensagem estável em português e deixar o detalhe interno só no log.
3. **Pastas de migration:** `AddProperties` está em `Infra/Persistence/Migrations`, enquanto `InitialCreate` e o snapshot permanecem em `Infra/Migrations`. O gate migrou com sucesso; consolidar na próxima alteração de schema.

## Veredito

VALIDAÇÃO APROVADA (3 recomendações)
