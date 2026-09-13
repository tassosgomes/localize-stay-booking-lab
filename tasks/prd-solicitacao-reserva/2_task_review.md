# Review — Task 2.0: Cliente de disponibilidade de Catalog: 200/404/falha (V-02)

- **Modo:** focused (primeira revisão)
- **Validador:** worker fresco (skill `tsg-flow-validator`)
- **Base:** checkpoint task 1.0 — HEAD `cbb0242d3d3de54a7ecc05cfb03101b909d75924` (diff em unstaged + untracked)
- **Resultado:** **VALIDAÇÃO APROVADA (VALIDATION APPROVED)** — 0 bloqueantes, 2 recomendações não bloqueantes

## Gate (executado pelo validador, antes da leitura semântica)

```
scripts/ai-flow/gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Catalog.CatalogAvailabilityClientTests"
→ GATE: APROVADO (exit 0)
format: dotnet format ok (LocalizeStay.Booking.sln: 7 arquivos)
build: 4 solutions ok — 0 Warning(s) / 0 Error(s) (TreatWarningsAsErrors ativo; sem NU1109/MSB3277)
testes: FullyQualifiedName~...CatalogAvailabilityClientTests = 4/4 ok
```

Conferido com o contrato da task (`2_task.md` task_context): behavioral, seletor e resultado esperado (4 testes, 0 falhas) correspondem exatamente. Implementer reportou o mesmo resultado; evidência aqui é execução independente.

## Escopo revisado

Criados (7): `Application/Reservations/ICatalogAvailabilityClient.cs`; `Infra/Catalog/{CatalogAvailabilityHttpClient,CatalogClientOptions,CatalogClientExtensions,CatalogUnavailableException}.cs`; `IntegrationTests/Catalog/{CatalogAvailabilityClientTests,FakeCatalogServerFactory}.cs`.
Modificados (4): `Directory.Packages.props`; `Api/appsettings.json`; `Infra.csproj` (+2 refs); `IntegrationTests.csproj` (+1 ref).
`2_task.md` (status pending→in_progress) e `flow-state.json` são metadata de fluxo do orquestrador — fora do juízo de código. Nenhum arquivo fora da lista da task ("Arquivos Envolvidos") foi tocado.

## Verificação dos Critérios de Sucesso

| # | Critério | Evidência | Status |
|---|---|---|---|
| 1 | Teste focalizado passa | gate exit 0 | ✅ |
| 2 | Seletor: ≥1 teste/cenário, sem casos sem relação | filtro executou exatos 4 testes da classe; nenhum outro caso | ✅ |
| 3 | Build sem erros | gate (4 soluções, 0W/0E) | ✅ |
| 4 | 200 → `AvailabilityFacts` com 5 campos, `pricePerNight` decimal | `CatalogAvailabilityHttpClient.cs:64-77` (`decimal.TryParse` `NumberStyles.Number` + `InvariantCulture`); teste `:45-50` afirma os 5 campos, `350.00m` | ✅ |
| 5 | 404 → `null`, sem exceção | `CatalogAvailabilityHttpClient.cs:33-36`; teste `:61-74` | ✅ |
| 6 | 500 e timeout → `CatalogUnavailableException` | 5xx: `:38-43` (mensagem inclui status); rede/desserialização/timeout: `:55-62` (`TaskCanceledException` quando `!cancellationToken.IsCancellationRequested`, `HttpRequestException`, `TimeoutRejectedException`, `JsonException`; corpo 200 vazio/nulo: `:52-53`; `pricePerNight` inválido: `:67-70`); testes `:76-108` | ✅ |
| 7 | Sem retry (1 chamada HTTP por teste) | pipeline com **única** estratégia `AddTimeout` (`CatalogClientExtensions.cs:34-36`, sem `AddStandardResilienceHandler`); fake conta requisições (`FakeCatalogServerFactory.cs:59`) e os 4 testes afirmam `RequestsReceived == 1`, inclusive timeout (`CatalogAvailabilityClientTests.cs:107`) | ✅ |
| 8 | Checkpoint de feedback executado | gate acima | ✅ |
| 9 | Artefatos do gate criados na task ou pré-existentes | fake server + testes criados aqui; `AvailabilityFacts` (1.0) pré-existente | ✅ |
| 10 | Nenhum arquivo de task futura necessário | compila contra 1.0 apenas; wiring de `AddCatalogAvailabilityClient` no `Program.cs` é explicitamente task 3.0 (`3_task.md:138-139`, techspec `Program.cs`→V-03) e `Program.cs` está intocado | ✅ |
| 11 | Evidência prova só o mecanismo de comunicação | porta em Application é pura (sem regra RN-02..RN-06); adaptador é tradução pura 200/404/falha; sem superfície HTTP pública de Booking | ✅ |

## Conformidade com specs/contrato

- **Path/query fiel ao contrato**: teste `:54-57` afirma o formato exato `/v1/accommodations/{id}/availability-check?checkIn=2026-10-10&checkOut=2026-10-13&guestsCount=2` (schema/params de `api-contract.yaml:112-194`). Corpo 200 do fake coincide campo a campo com `AvailabilityCheckResponse` (`api-contract.yaml:464-504`, `pricePerNight` como string).
- **Timeout configurável**: `CatalogClientOptions.TimeoutSeconds` (default 3s, validado > 0 em `CatalogClientExtensions.cs:28-32`); teste usa 1s contra delay de 10s no fake — prova o timeout da pipeline (não o default de 100s do `HttpClient`).
- **BaseUrl dev**: `appsettings.json` → `http://localhost:5101/v1`, conforme decisão registrada na TechSpec (divergência deliberada com o `servers.url: 5010` ilustrativo do contrato; techspec.md §Questões/Considerações).
- **Convenções**: typed client via `IHttpClientFactory` (`AddHttpClient<ICatalogAvailabilityClient, CatalogAvailabilityHttpClient>`, sem `new HttpClient()`); fake server via `TestServer` do `Microsoft.AspNetCore.Mvc.Testing` já referenciado — sem NuGet de teste novo; `JsonSerializerDefaults.Web` para camelCase do contrato.
- **Camada**: Application não depende de Infra (exceção citada apenas em comentário doc); `CatalogUnavailableException` em Infra conforme plano (task 3.0 consome).

## Pacotes (NU1109 / duplicação)

- `Microsoft.Extensions.Http.Resilience` 10.10.0 — exigido pela task (`AddResilienceHandler`); uso real em `CatalogClientExtensions.cs`.
- `Microsoft.Extensions.Http` 10.0.12 — uso direto no projeto de testes (`ConfigureHttpClientDefaults`/`AddHttpClient`); versão alinhada à família 10.0.12 já presente (EF Core), ponto único de verdade em `Directory.Packages.props`, sem duplicação (CPM + `CentralPackageTransitivePinningEnabled`).
- `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.12 — uso direto em Infra (`services.Configure<T>(IConfiguration)`).
- Build com 0 warnings em TreatWarningsAsErrors confirma ausência de conflito NU1109/MSB3277.

## Recomendações (não bloqueantes)

1. **Dissimetria de asserções 500 vs timeout**: o teste de 500 afirma o status na mensagem (`:89`), o de timeout afirma apenas o tipo da exceção. Acrescentar asserção sobre `InnerException` (`TimeoutRejectedException`/`TaskCanceledException`) endureceria a prova da *causa* timeout sem alterar comportamento — pode entrar como polimento na task 3.0 ou F06.
2. **Echo de `accommodationId` não conferido**: o adaptador desserializa `AccommodationId` mas não valida contra o id solicitado (payload com echo divergente seria aceito como 200 válido). O contrato não exige; sugerido como endurecimento futuro (F06), não nesta task.

## Integridade da revisão

HEAD `cbb0242` e a árvore (8 entradas changed/untracked) conferidos idênticos no início e no fim da revisão. Nenhum código, checkbox, frontmatter ou `flow-state.json` foi alterado pelo validador.

**Contagem: 0 bloqueantes · 2 recomendações — VALIDAÇÃO APROVADA (VALIDATION APPROVED)**
