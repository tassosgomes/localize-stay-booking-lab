---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: true
blocked_by: []
---

<task_context>
<domain>services/booking</domain>
<type>integration</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>external_apis,http_server</dependencies>
<unblocks>"3.0"</unblocks>
<feedback_checkpoint>`dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Catalog.CatalogAvailabilityClientTests"` verde para os 4 cenários (200/404/500/timeout) contra um fake server local</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Catalog.CatalogAvailabilityClientTests"</gate_command>
<gate_test_selector>Classe `CatalogAvailabilityClientTests` (`LocalizeStay.Booking.IntegrationTests`)</gate_test_selector>
<gate_expected_result>4 testes passam (200→`AvailabilityFacts` preenchido, 404→`null`, 500→`CatalogUnavailableException`, timeout→`CatalogUnavailableException`); 0 falhas</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>`ICatalogAvailabilityClient` traduz corretamente 200/404/falha de infraestrutura do endpoint síncrono de Catalog, sem aplicar nenhuma regra de negócio de Booking</vertical_slice>
</task_context>

# Tarefa 2.0: Cliente de disponibilidade de Catalog: 200/404/falha (V-02)

## Relacionada às User Stories

- "Como autor/arquiteto em estudo, eu quero que a validação de disponibilidade aconteça via chamada
  síncrona contratada a Catalog, para que eu possa observar e documentar esse padrão de integração"
  (cobertura direta)

## Visão Geral

Implementa a porta `ICatalogAvailabilityClient` e seu adaptador HTTP (`CatalogAvailabilityHttpClient`),
que chama `GET /accommodations/{id}/availability-check` (contrato provisório de Catalog, tag
"Catalog Availability (dependency)" em `api-contract.yaml`) com timeout explícito e **sem retry**.
Esta fatia só prova o mecanismo de comunicação (200/404/falha) — nenhuma regra de negócio de Booking
(RN-02 a RN-06) é aplicada aqui; isso é responsabilidade do `Reservation.Create` (task 1.0), consumido
pela orquestração da task 3.0. É independente de 1.0 porque não compartilha nenhum arquivo com o
domínio/persistência.

## Entrega Observável

- **Entrada ou gatilho:** teste de integração que aponta `CatalogAvailabilityHttpClient` (via
  `IHttpClientFactory`) para um fake server HTTP local (`WebApplicationFactory` minimalista dedicado
  só a simular Catalog, sem NuGet adicional) configurado para responder 200, 404, 500 ou expirar por
  timeout.
- **Resultado esperado:** 200 → `AvailabilityFacts` com os campos do corpo deserializados; 404 →
  `null` (não é exceção); 500 ou timeout → `CatalogUnavailableException`.
- **Checkpoint de feedback:** `dotnet test --filter "FullyQualifiedName~CatalogAvailabilityClientTests"`
  verde para os 4 cenários.
- **Seletor focalizado:** `LocalizeStay.Booking.IntegrationTests.Catalog.CatalogAvailabilityClientTests`
- **Fora deste checkpoint:** nenhuma regra de negócio (RN-02 a RN-06) é validada aqui; a tradução do
  404/`CatalogUnavailableException` em `ACOMODACAO_INDISPONIVEL`/`CATALOG_INDISPONIVEL` HTTP é a task
  3.0; nenhuma chamada real ao serviço Catalog (só o fake server local).

## Requisitos

- `ICatalogAvailabilityClient.CheckAvailabilityAsync(accommodationId, checkIn, checkOut, guestsCount,
  cancellationToken)` retorna `Task<AvailabilityFacts?>`.
- 200: deserializa o corpo (`AvailabilityCheckResponse` do contrato) em `AvailabilityFacts`.
- 404: retorna `null` — resultado de negócio válido, não uma exceção (a tradução em rejeição é da
  Application, task 3.0).
- Qualquer `HttpRequestException`, `TaskCanceledException` (timeout), status `>= 500` ou falha ao
  deserializar o corpo de um 200 lança `CatalogUnavailableException`.
- Pipeline de resiliência via `Microsoft.Extensions.Http.Resilience` (`AddResilienceHandler`
  customizado) com **apenas** timeout explícito (proposto: 3s) — **sem retry**, sem usar
  `AddStandardResilienceHandler` padrão (que inclui retry). Decisão deliberada: retry/idempotência de
  rede pertencem a F06 (Fase 1), não a esta feature.
- `CatalogClient:BaseUrl` configurável via `appsettings.json` (dev: `http://localhost:5101/v1` —
  porta já aprovada pela fundação técnica da Fase 0 para Catalog, não a `5010` do `servers.url`
  ilustrativo do `api-contract.yaml`, conforme resolução já registrada em "Questões em Aberto" da
  TechSpec).

## Arquivos Envolvidos

- **Criar:**
  - `services/booking/src/2-Application/LocalizeStay.Booking.Application/Reservations/ICatalogAvailabilityClient.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Catalog/CatalogAvailabilityHttpClient.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Catalog/CatalogClientOptions.cs`
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Catalog/CatalogClientExtensions.cs`
    (`AddCatalogAvailabilityClient(IServiceCollection, IConfiguration)` — typed client via
    `AddHttpClient<ICatalogAvailabilityClient, CatalogAvailabilityHttpClient>` + `AddResilienceHandler`
    só com timeout)
  - `services/booking/src/4-Infra/LocalizeStay.Booking.Infra/Catalog/CatalogUnavailableException.cs`
  - `services/booking/tests/LocalizeStay.Booking.IntegrationTests/Catalog/CatalogAvailabilityClientTests.cs`
    + um fake server mínimo dedicado (ex.: `FakeCatalogServerFactory.cs`, outro
    `WebApplicationFactory` no próprio projeto de teste)
- **Modificar:**
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/appsettings.json` (adiciona
    `CatalogClient:BaseUrl`; a fundação já criou este arquivo só com a connection string de Postgres)
- **Referência:**
  - `api-contract.yaml` (paths `/accommodations/{accommodationId}/availability-check`,
    `AvailabilityCheckResponse`) — schema exato do corpo 200
  - `techspec.md` (Cliente HTTP de Catalog — resiliência) — decisão de timeout sem retry
- **Skills para consultar durante implementação:**
  - `dotnet-dependency-config` — `IHttpClientFactory`, `AddResilienceHandler` (timeout sem retry)
  - `dotnet-testing` — fake HTTP server via `WebApplicationFactory` em vez de dependência nova

## Subtarefas

- [ ] 2.1 Implementar `ICatalogAvailabilityClient` e `CatalogAvailabilityHttpClient` (200/404 sem
      exceção)
- [ ] 2.2 Implementar `CatalogUnavailableException` e o mapeamento de falha (timeout, 5xx, erro de
      deserialização)
- [ ] 2.3 Implementar `CatalogClientOptions`/`CatalogClientExtensions` com `AddResilienceHandler`
      (timeout 3s, sem retry) e `CatalogClient:BaseUrl` em `appsettings.json`
- [ ] 2.4 Criar o fake server local de Catalog (200/404/500/timeout configuráveis por teste)
- [ ] 2.5 Escrever `CatalogAvailabilityClientTests` cobrindo os 4 cenários

## Sequenciamento

- Bloqueado por: Nenhuma (fundação externa já provisionada)
- Desbloqueia: 3.0 (consome `ICatalogAvailabilityClient`/`CatalogUnavailableException`)
- Paralelizável: Sim, com 1.0 — nenhum arquivo compartilhado

## Rastreabilidade

- Esta tarefa cobre: RN-04 (mecanismo de consulta, não a regra sobre os fatos), AC de RF-01 "Catalog
  indisponível"; Catalog RN-02/RN-03/RN-04/RN-06 do ponto de vista de Booking como consumidor do
  formato de resposta.
- Evidência esperada: `CatalogAvailabilityClientTests` verde para 200/404/500/timeout.

## Detalhes de Implementação

```csharp
public interface ICatalogAvailabilityClient
{
    // Retorna null quando Catalog responde 404 (accommodation não encontrada).
    // Lança CatalogUnavailableException para timeout, erro de rede, 5xx ou payload inesperado.
    Task<AvailabilityFacts?> CheckAvailabilityAsync(
        Guid accommodationId, DateOnly checkIn, DateOnly checkOut, int guestsCount,
        CancellationToken cancellationToken);
}
```

Corpo 200 esperado (`AvailabilityCheckResponse`, `api-contract.yaml`):
`{ accommodationId, active, maxGuests, availableForPeriod, pricePerNight: "350.00", currency: "BRL" }`
— `pricePerNight` chega como string decimal e deve ser convertido para `decimal` ao montar
`AvailabilityFacts` (não serializado de volta; a serialização como string só existe na saída HTTP de
Booking, task 3.0).

**Convenções da stack:**
- Typed client registrado via `IHttpClientFactory`
  (`dotnet-dependency-config/examples`), nunca `new HttpClient()` direto.
- `AddResilienceHandler` customizado com uma única estratégia de timeout — não usar
  `AddStandardResilienceHandler` (inclui retry, antecipa F06).
- Teste de integração usa um `WebApplicationFactory` minimalista como fake server, conforme
  `dotnet-testing` — evita introduzir WireMock.Net ou similar (desvio já registrado na TechSpec como
  disciplina de introdução de tecnologia).

## Prontidão para Implementação

- **Decisões fechadas:** sem retry no pipeline de resiliência (F06 trata disso); 404 retorna `null`,
  não exceção; `CatalogClient:BaseUrl` dev = `http://localhost:5101/v1`.
- **Limites de decisão do implementer:** valor exato do timeout (proposto 3s, ajustável sem impacto
  arquitetural — já registrado como questão em aberto não bloqueante na TechSpec); nome exato do
  header/opção interna do fake server.
- **Dependências disponíveis:** nenhuma além de `Directory.Packages.props` (fundação, para
  `Microsoft.Extensions.Http.Resilience`).
- **Artefatos exigidos pelo gate:** `CatalogAvailabilityClientTests.cs` e o fake server são criados
  nesta própria task.
- **Dependências futuras:** Nenhuma — a task 3.0 consome a porta/exceção já prontas e testadas por
  esta task.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `dotnet test --filter "FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Catalog.CatalogAvailabilityClientTests"`
- [ ] O seletor encontra pelo menos um teste por cenário (200/404/500/timeout) e não executa casos sem
      relação com esta task
- [ ] Build compila sem erros: `dotnet build services/booking/LocalizeStay.Booking.sln`
- [ ] Cenário 200 retorna `AvailabilityFacts` com os 5 campos corretamente convertidos (`pricePerNight`
      como `decimal`)
- [ ] Cenário 404 retorna `null`, sem lançar exceção
- [ ] Cenários 500 e timeout lançam `CatalogUnavailableException`
- [ ] Nenhuma tentativa de retry ocorre nos cenários de falha (uma única chamada HTTP por teste)
- [ ] Checkpoint de feedback executado conforme descrito acima
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente o mecanismo de comunicação com Catalog, não regra de negócio de
      Booking nem a superfície HTTP pública (tasks 1.0/3.0)
