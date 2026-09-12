---
status: done
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: [3.0, 4.0, 5.0]
---

<task_context>
<domain>frontend</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
<unblocks>"8.0"</unblocks>
<feedback_checkpoint>`npm run test -- ServiceStatus.test.tsx` verde (fetch mockado via MSW); manualmente, `npm run dev` + abrir no navegador mostra os 3 status "Healthy" sem erro de CORS no console</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="ServiceStatus.test"</gate_command>
<gate_test_selector>Arquivo `frontend/localize-stay-frontend/src/components/ServiceStatus/ServiceStatus.test.tsx`</gate_test_selector>
<gate_expected_result>Teste de componente passa: com as 3 chamadas `fetch` mockadas (MSW) retornando `200`, o componente renderiza os 3 serviços com status "Healthy"; com uma delas falhando, renderiza o status de erro correspondente</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Frontend local (`npm run dev`) busca `/health` de Catalog, Booking e Payment via `fetch` direto (CORS, sem gateway) e renderiza o status de cada um</vertical_slice>
</task_context>

# Tarefa 7.0: Frontend exibe status dos 3 serviços via CORS (V-04)

## Relacionada as User Stories

- N/A — TechSpec Standalone. Cobre a fatia V-04 (`techspec.md`, Mapa de Fatias Verticais).

## Visão Geral

Primeira e única tela do frontend de teste nesta fundação: mostra ao desenvolvedor, de forma visual,
que os três serviços HTTP estão de pé e acessíveis via CORS direto (sem gateway/BFF, conforme
ADR-003). Estrutura "Base" (`react-architecture`): ainda não há `features/*` porque não há tela de
negócio.

## Entrega Observável

- **Entrada ou gatilho:** `npm run dev` e abrir a SPA no navegador.
- **Resultado esperado:** a tela busca `GET /health/ready` de Catalog, Booking e Payment (URLs de
  `.env.development`) e renderiza o status de cada um ("Healthy" ou erro).
- **Checkpoint de feedback:** `npm run test -- ServiceStatus.test.tsx` (fetch mockado via MSW) —
  verde. Manualmente (fora do gate automatizado): `npm run dev` + abrir no navegador mostra os 3
  status "Healthy" sem erro de CORS no console, com os 3 serviços reais rodando localmente (tasks
  3.0/4.0/5.0).
- **Seletor focalizado:** `ServiceStatus.test.tsx`
- **Fora deste checkpoint:** nenhuma tela de negócio; nenhum roteamento (`react-router`) além desta
  única tela; nenhuma configuração de runtime-env/Nginx (ver "Desvios Identificados" da TechSpec —
  o frontend roda só localmente nesta fase).

## Requisitos

- Estrutura "Base" (`react-architecture`): `src/components`, `src/hooks`, `src/services`,
  `src/utils` no nível de `src/` — sem `features/*` ainda.
- `src/services/healthService.ts` concentra as três chamadas `fetch` (Catalog/Booking/Payment).
- URLs de backend via `.env.development` (`VITE_CATALOG_URL`, `VITE_BOOKING_URL`,
  `VITE_PAYMENT_URL`), lidas por um módulo `src/config/env.ts` tipado.
- Cada um dos 3 serviços habilita CORS explícito para a origem do Vite dev server (atualização de
  `CorsExtensions.cs` nos 3 serviços — arquivos já criados nas tasks 3.0/4.0/5.0).
- Sem gateway/BFF: o componente chama as 3 URLs diretamente (ADR-003).

## Arquivos Envolvidos

- **Criar:**
  - `frontend/localize-stay-frontend/` (scaffold Vite + React + TS — `package.json`, `index.html`,
    `vite.config.ts`, `tsconfig.json`)
  - `frontend/localize-stay-frontend/src/config/env.ts`
  - `frontend/localize-stay-frontend/src/services/healthService.ts`
  - `frontend/localize-stay-frontend/src/components/ServiceStatus/ServiceStatus.tsx`
  - `frontend/localize-stay-frontend/src/components/ServiceStatus/ServiceStatus.test.tsx` (MSW
    mockando as 3 chamadas `fetch`)
  - `frontend/localize-stay-frontend/.env.development`
- **Modificar:**
  - `services/catalog/src/1-Services/LocalizeStay.Catalog.Api/Extensions/CorsExtensions.cs`
  - `services/booking/src/1-Services/LocalizeStay.Booking.Api/Extensions/CorsExtensions.cs`
  - `services/payment/src/1-Services/LocalizeStay.Payment.Api/Extensions/CorsExtensions.cs`
  - `README.md` (raiz) — adicionar seção "Como rodar o frontend localmente"
- **Referência:**
  - `docs/adr/adr-003-frontend-teste-react.md` — stack e padrão de integração direta
  - `services/{catalog,booking,payment}/.../HealthCheckExtensions.cs` (tasks 3.0/4.0/5.0) — contrato
    de resposta do `/health/ready` a ser consumido
- **Skills para consultar durante implementação:**
  - `react-architecture` — estrutura "Base"
  - `react-testing` — padrão de teste de componente com `fetch` mockado (MSW)

## Subtarefas

- [ ] 7.1 Fazer scaffold do Vite + React + TS em `frontend/localize-stay-frontend/` (estrutura Base)
- [ ] 7.2 Implementar `src/config/env.ts` e `src/services/healthService.ts` (3 chamadas `fetch`)
- [ ] 7.3 Implementar `ServiceStatus.tsx` renderizando o status de cada serviço
- [ ] 7.4 Atualizar `CorsExtensions.cs` dos 3 serviços para aceitar a origem do Vite dev server
- [ ] 7.5 Criar `ServiceStatus.test.tsx` com MSW mockando as 3 chamadas (caso feliz + 1 falha)

## Sequenciamento

- Bloqueado por: 3.0, 4.0, 5.0 (os três `/health/ready` precisam existir e ter CORS habilitável)
- Desbloqueia: 8.0 (nenhuma dependência direta de artefato, mas mantém a ordem da TechSpec — ver
  Mapa de Entrega em `tasks.md`)
- Paralelizável: Não com 3.0/4.0/5.0 (depende deles); pode rodar em paralelo com 6.0 (nenhum arquivo
  compartilhado, exceto que ambas modificam `Program.cs`/`Extensions` de Booking — se executadas em
  paralelo, coordenar o merge do `CorsExtensions.cs` de Booking)

## Rastreabilidade

- Esta tarefa cobre: Fatia V-04 da TechSpec.
- Evidência esperada: `ServiceStatus.test.tsx` verde; `npm run dev` mostra os 3 status "Healthy" sem
  erro de CORS (evidência manual).

## Detalhes de Implementação

Da TechSpec (`techspec.md`, "Frontend (V-04)"):

> - Estrutura "Base" (`react-architecture`): `src/components`, `src/hooks`, `src/services`,
>   `src/utils` no nível de `src/` — ainda não há `features/*` porque não há tela de negócio.
> - `src/services/healthService.ts` concentra as três chamadas `fetch` (Catalog/Booking/Payment).
> - URLs de backend via `.env.development` (`VITE_CATALOG_URL`, `VITE_BOOKING_URL`,
>   `VITE_PAYMENT_URL`), lidas por um módulo `src/config/env.ts` tipado.

**Desvio deliberado, já decidido pela TechSpec (não reabrir):** `react-runtime-config`
(`runtime-env.js`/Nginx/`envsubst`) não é aplicado nesta fase — o frontend roda apenas localmente
(ADR-003 não prevê deploy/containerização dele). Não implemente o pipeline completo de runtime-config
aqui.

**Convenções da stack (das skills consultadas):**
- Teste de componente mockando `fetch` com MSW, conforme `react-testing`, cobrindo o caso feliz (3
  serviços Healthy) e pelo menos um caso de falha (um serviço indisponível).

## Prontidão para Implementação

- **Decisões fechadas:** sem `features/*`, sem gateway/BFF, sem pipeline de runtime-config nesta
  fase (decisões já fechadas na TechSpec, não reabrir); nomes das variáveis de ambiente
  (`VITE_CATALOG_URL`, `VITE_BOOKING_URL`, `VITE_PAYMENT_URL`).
- **Limites de decisão do implementer:** layout visual exato da tela de status; biblioteca de
  mock HTTP para o teste (MSW é a referência da skill, mas outra equivalente é aceitável se já usada
  no repositório).
- **Dependências disponíveis:** `/health/ready` de Catalog (3.0), Booking (4.0), Payment (5.0) já
  implementados e compilando.
- **Artefatos exigidos pelo gate:** `ServiceStatus.test.tsx` é criado nesta própria task; os mocks de
  `fetch` (MSW) são definidos no próprio teste, sem depender de nenhum serviço real rodando.
- **Dependências futuras:** Nenhuma.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `npm run test -- ServiceStatus.test.tsx` (ou equivalente do runner
      configurado, ex. Vitest)
- [ ] O seletor encontra pelo menos um teste e não executa casos sem relação com esta task
- [ ] Build compila sem erros: `npm run build` (Vite) e `npm run typecheck` (ou `tsc --noEmit`)
- [ ] Componente renderiza "Healthy" para os 3 serviços quando os 3 `fetch` mockados retornam 200
- [ ] Componente renderiza o status de erro quando um dos 3 `fetch` mockados falha
- [ ] `CorsExtensions.cs` dos 3 serviços aceita a origem do Vite dev server (`http://localhost:5173`
      ou porta configurada)
- [ ] Checkpoint de feedback executado: `npm run dev` com os 3 serviços reais rodando localmente →
      os 3 status aparecem "Healthy" sem erro de CORS no console (evidência manual)
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova somente esta fatia (frontend consumindo os 3 health checks) e não
      depende de contratos (8.0) ou catalogação (9.0)
