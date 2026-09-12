# ADR-003: Frontend de teste/visualização — React, sem gateway/BFF na Fase 0

## Status

Accepted — 2026-09-11

## Contexto

`vision.md` e `docs/brief.md` não previam nenhum frontend: o escopo funcional congelado da Fase 0 é expresso
inteiramente como contratos de API/evento/dataset entre serviços (`context/domain-map.md`), e o público-alvo
descrito na Vision são o próprio autor e leitores de artigos — não usuários finais de uma aplicação web.

Depois do baseline inicial, o autor pediu explicitamente uma interface web simples, cujo único propósito é
facilitar a verificação manual do sistema (criar hospedagem, consultar disponibilidade, criar reserva, ver o
resultado da saga de pagamento) — não é o foco do laboratório, que continua sendo a engenharia de contratos,
saga e catalogação entre os quatro serviços de backend.

O repositório já tem um conjunto completo de skills de convenção para **React + Vite + TypeScript**
(`react-architecture`, `react-code-quality`, `react-observability`, `react-production-readiness`,
`react-runtime-config`, `react-subpath-deploy`, `react-testing`), e o próprio autor nomeou "React" ao pedir o
frontend — não havia alternativa de stack a comparar aqui, diferente da decisão de backend (ADR-001).

Ficava em aberto, porém, como esse frontend se conecta aos quatro serviços: via um gateway/BFF dedicado, ou
chamando diretamente as APIs OpenAPI de Catalog, Booking e Payment a partir do navegador.

## Decisão

1. Um frontend simples em **React + Vite + TypeScript** é adicionado ao sistema como uma peça de suporte a
   testes/visualização — não como um quinto domínio de negócio. Ele não é um bounded context do
   `domain-map.md`, não tem ownership de dados e não implementa regra de negócio: é um cliente fino sobre as
   APIs já contratadas.
2. Na Fase 0, o frontend chama **diretamente** as APIs públicas de Catalog, Booking e Payment (contratadas em
   OpenAPI), sem um gateway/BFF intermediário. Cada serviço habilita CORS explicitamente para a origem do
   frontend.

## Alternativas consideradas

- **Gateway/BFF dedicado** — agregaria as chamadas dos três serviços atrás de uma única origem, evitando CORS
  multi-serviço, mas introduz um componente novo (roteamento, agregação, versionamento próprio) para um
  problema que ainda não existe (não há autenticação a centralizar, nem múltiplos frontends a atender) —
  contraria a regra do roadmap de só introduzir tecnologia quando resolve um problema já existente.
- **Não ter frontend na Fase 0** (testar só via OpenAPI/Swagger UI ou HTTP client) — foi a posição original do
  baseline, mas o autor considerou insuficiente para "ver o sistema funcionando" de ponta a ponta.

## Consequências

- O frontend não entra no `domain-map.md` nem é tratado como dono de dados; TechSpecs de frontend usam
  `tsg-flow-frontend-techspec-creator` e seguem as skills `react-*` já instaladas.
- Cada serviço de backend (Catalog, Booking, Payment) precisa expor uma política de CORS permitindo a origem do
  frontend — isso é um requisito de `dotnet-program-setup` a carregar nas respectivas TechSpecs.
- Se, no futuro, surgir a necessidade real de um ponto único de entrada (ex.: autenticação centralizada,
  múltiplos clientes, agregação de chamadas), a introdução de um gateway/BFF é uma nova decisão de arquitetura
  e deve gerar uma ADR marcando esta como Superseded — não uma extensão silenciosa.
- Não é criado um "Notification" visual nem uma UI de leitura para Notification na Fase 0: o foco do frontend é
  o fluxo de reserva/pagamento (Catalog → Booking → Payment); acompanhar o resultado de Notification, se
  necessário, pode ficar restrito a log/observação direta do worker.
