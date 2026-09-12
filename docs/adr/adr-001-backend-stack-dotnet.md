# ADR-001: Stack de backend — .NET / C# (ASP.NET Core)

## Status

Accepted — 2026-09-11

## Contexto

O Localize Stay v2 é um laboratório pessoal de arquitetura distribuída (`vision.md`) composto por quatro
serviços independentes — Catalog, Booking, Payment e Notification Worker (`context/domain-map.md`) — que devem
expor contratos formais (OpenAPI, AsyncAPI, Data Contracts) e evoluir por fases, começando por uma
"Service-Based Architecture" com PostgreSQL (`docs/brief.md`, Fase 0).

A `vision.md` (seção 5, Restrições Técnicas) registra explicitamente que a linguagem/framework de aplicação
**não** é uma decisão da Vision, e fica reservada para o baseline arquitetural — ou seja, para esta etapa.

Não havia, até este ADR, nenhum código, `.csproj`, `.sln` ou `package.json` no repositório que já fixasse essa
escolha. O único sinal existente era o conjunto de skills de convenções já instalado no ambiente do autor
(`dotnet-architecture`, `dotnet-code-quality`, `dotnet-dependency-config`, `dotnet-observability`,
`dotnet-performance`, `dotnet-production-readiness`, `dotnet-program-setup`, `dotnet-testing`) — um indício forte
de preferência, mas que orienta padrões de código, não decide a stack por si (instrução explícita da skill de
baseline).

Diante disso, a decisão foi levada ao autor do laboratório, que confirmou a opção abaixo.

## Decisão

Os quatro serviços (Catalog, Booking, Payment, Notification Worker) serão implementados em **.NET (C#) com
ASP.NET Core**, usando Minimal APIs ou Controllers conforme a necessidade de cada serviço, e Entity Framework
Core como acesso a dados sobre a instância única de PostgreSQL.

## Alternativas consideradas

- **Node.js / TypeScript** — linguagem única com um eventual frontend React futuro, mas sem o conjunto de
  skills de qualidade/observabilidade/testes já disponível neste repositório para essa stack.
- **Outra stack (Java/Spring, Python/FastAPI, Go)** — não escolhida; não havia sinal de preferência do autor
  nem skills de convenção instaladas para essas stacks neste repositório.

## Consequências

- TechSpecs de backend devem seguir as convenções das skills `dotnet-architecture`, `dotnet-code-quality`,
  `dotnet-dependency-config`, `dotnet-observability`, `dotnet-performance`, `dotnet-production-readiness` e
  `dotnet-program-setup`, e os testes devem seguir `dotnet-testing`.
- A escolha de ASP.NET Core é compatível com a exposição de contratos OpenAPI nativamente (Swashbuckle/Minimal
  API metadata) e com bibliotecas maduras de client para RabbitMQ (ver ADR-002) e Entity Framework Core para
  ownership lógico por schema.
- Troca de stack após início da implementação é uma decisão cara (reescrita dos quatro serviços); qualquer
  revisão futura desta decisão deve gerar uma nova ADR marcando esta como Superseded, não uma edição silenciosa.
