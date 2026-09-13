---
status: pending
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: [2.0, 4.0]
---

<task_context>
<domain>frontend/reservation-lookup/e2e</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server,database,external_apis</dependencies>
<unblocks>""</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="reservation-lookup"` executa a suíte Playwright `e2e/reservation-lookup.spec.ts` contra Booking real (encontrada + não encontrada), confirma dados/`sagaStatus=pendente` no browser real e roda o gate completo (lint, type-check, coverage, build, `api:generate` sem drift)</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="reservation-lookup"</gate_command>
<gate_test_selector>Arquivo `frontend/localize-stay-frontend/e2e/reservation-lookup.spec.ts`</gate_test_selector>
<gate_expected_result>Os 2 cenários Playwright passam contra Booking real (encontrada confirma os dados congelados e `sagaStatus=pendente` exibidos; identificador bem formado porém inexistente mostra "não encontrada"); `npm run api:generate` seguido de `git diff --exit-code -- src/services/api/generated/reservationDetail.ts` não produz diferença; lint, type-check, `test:coverage` e `build` passam</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Um browser real solicita uma Reservation via jornada real de F01 (ou API direta), consulta esse identificador em `/reservations/consultar` contra Booking real e confirma os dados exibidos, incluindo `sagaStatus=pendente`; um segundo cenário consulta um identificador bem formado porém inexistente e confirma a mensagem de "não encontrada" — fecha RF-01 nos dois lados, provando que UI e backend concordam fora de mocks</vertical_slice>
</task_context>

# Tarefa 5.0: Provar a jornada full-stack com Playwright e fechar o gate (V-02 frontend)

## Relacionada às User Stories

- "Como frontend de teste, eu quero buscar uma Reservation por identificador e exibir seus dados e o
  estado da saga, para permitir verificação manual do fluxo completo sem acesso direto ao banco."
  (cobertura direta — fecha a jornada fora de mocks)
- "Como Guest, eu quero receber uma resposta clara quando o identificador informado não corresponde a
  nenhuma Reservation..." (cobertura direta — cenário de "não encontrada" contra backend real)

## Visão Geral

Fecha RF-01 nos dois lados com uma jornada Playwright real: um cenário crítico solicita uma Reservation
via jornada real de F01 (ou cria uma via API direta em `beforeEach`) e depois consulta esse
identificador em `/reservations/consultar`, confirmando os dados exibidos e `sagaStatus=pendente`. Um
segundo cenário consulta um identificador bem formado porém inexistente e confirma a mensagem de "não
encontrada". Os estados `confirmada`/`cancelada` **não** são exercitados aqui — dependem da mesma
técnica de seed via SQL direto que o backend documenta como dívida técnica (§Riscos Conhecidos de
`techspec.md`), não de uma jornada de usuário real reproduzível pelo browser; ficam cobertos por
RTL+MSW na task 4.0. Esta é a única task que depende de um artefato do backend (2.0, o endpoint real)
e de um artefato do frontend (4.0, a UI) ao mesmo tempo — o ponto de convergência final da feature.

## Entrega Observável

- **Entrada ou gatilho:** `npx playwright test e2e/reservation-lookup.spec.ts` contra o frontend real
  servido localmente e Booking real (`:5102`).
- **Resultado esperado:** cenário 1 — a Reservation recém-criada aparece com todos os campos
  corretos e `sagaStatus=pendente`; cenário 2 — um identificador bem formado sem Reservation
  correspondente mostra a mensagem de "não encontrada".
- **Checkpoint de feedback:** `scripts/ai-flow/gate.sh --filter="reservation-lookup"` verde
  (Playwright + gate completo: lint, type-check, coverage, build, drift-check).
- **Seletor focalizado:** `e2e/reservation-lookup.spec.ts`
- **Fora deste checkpoint:** estados `confirmada`/`cancelada` (cobertos por RTL+MSW na task 4.0, não
  aqui); qualquer novo comportamento de produto (esta task só prova o que 2.0/4.0 já implementaram).

## Requisitos

- Cenário 1: cria uma Reservation (via jornada real de `/reservations` de F01, ou via chamada HTTP
  direta a `POST /v1/reservations` em `beforeEach` — decisão local do implementer, seguindo o padrão
  já usado por `e2e/reservation-request.spec.ts` se aplicável), navega para
  `/reservations/consultar`, busca pelo identificador retornado e confirma: dados congelados
  (período, hóspedes, preço, moeda, total), `status=solicitada`, `sagaStatus=pendente`,
  `correlationId` visível, sem `cancellationReason`.
- Cenário 2: busca um identificador bem formado (UUID válido) que não corresponde a nenhuma
  Reservation criada e confirma a mensagem de "não encontrada" (tom neutro, não erro).
- Gate completo (`npm run lint`, `npm run type-check`, `npm run test:coverage`, `npm run build`,
  `npm run api:generate` + `git diff --exit-code -- src/services/api/generated/reservationDetail.ts`,
  `npm run test:e2e`) verde.

## Arquivos Envolvidos

- **Criar:**
  - `frontend/localize-stay-frontend/e2e/reservation-lookup.spec.ts`
- **Referência:**
  - `frontend/localize-stay-frontend/e2e/reservation-request.spec.ts` — padrão de teste Playwright
    contra Booking real a seguir (fixtures, timeouts, `getByLabel`/`getByRole`)
  - `frontend/localize-stay-frontend/src/features/reservation-lookup/index.ts` (task 4.0) —
    `ReservationLookupPage`, rota `/reservations/consultar`
  - `services/booking` (task 2.0) — `GET /v1/reservations/{reservationId}` real
  - `frontend-techspec.md` (§Mocks e Ambiente de Desenvolvimento, §Abordagem de Testes — E2E,
    §Sequenciamento de Desenvolvimento) — escopo normativo dos 2 cenários
- **Skills para consultar durante implementação:**
  - `react-testing` — Playwright: jornada crítica, sem duplicar cobertura já provada por RTL+MSW

## Subtarefas

- [ ] 5.1 Escrever o cenário 1: criar Reservation (jornada real de F01 ou API direta), consultar em
      `/reservations/consultar` e confirmar campos + `sagaStatus=pendente`
- [ ] 5.2 Escrever o cenário 2: consultar identificador bem formado porém inexistente e confirmar
      "não encontrada"
- [ ] 5.3 Rodar o gate completo (`scripts/ai-flow/gate.sh --filter="reservation-lookup"`) e confirmar
      lint, type-check, coverage, build, drift-check e os 2 cenários Playwright verdes

## Sequenciamento

- Bloqueado por: 2.0 (endpoint real), 4.0 (UI real)
- Desbloqueia: Nenhuma (última task da feature)
- Paralelizável: Não — ponto de convergência final, depende de backend e frontend prontos

## Rastreabilidade

- Esta tarefa cobre: jornada full-stack real (browser → Booking) exigida pela User Story do
  "frontend de teste"; ausência de drift entre contrato e tipos gerados.
- Evidência esperada: os 2 cenários Playwright verdes; gate completo aprovado.

## Detalhes de Implementação

Escopo normativo (`frontend-techspec.md` §Abordagem de Testes — E2E):

> Um cenário crítico solicita uma reserva via jornada real de F01 (ou reaproveita um `beforeEach` que
> cria uma via API direta), depois consulta esse identificador em `/reservations/consultar` e confirma
> os dados exibidos, incluindo `sagaStatus=pendente`. Um segundo cenário consulta um identificador bem
> formado, porém inexistente, e confirma a mensagem de "não encontrada". Estados
> `confirmada`/`cancelada` não são exercitados em E2E.

Padrão de teste a seguir (`e2e/reservation-request.spec.ts`, já existente): sem MSW/Prism — Booking
real; `getByLabel`/`getByRole` para interação; timeout estendido se a criação da Reservation depender
de publish best-effort do broker (mesma nota de risco já documentada em F01, se o cenário 1 reaproveitar
a jornada real de criação em vez de API direta).

**Convenções da stack:**
- Nenhuma duplicação de cobertura já provada por RTL+MSW (task 4.0) — o E2E cobre só a jornada
  crítica fora de mocks, não os 5 cenários completos de novo.
- `npm run api:generate` roda antes do drift-check, mesmo mecanismo já usado para `booking.ts`/
  `catalog.ts`.

## Prontidão para Implementação

- **Decisões fechadas:** apenas 2 cenários (encontrada + não encontrada); `confirmada`/`cancelada`
  ficam fora do E2E (decisão já confirmada na TechSpec frontend, não uma lacuna desta task).
- **Limites de decisão do implementer:** criar a Reservation via jornada real de F01 ou via API
  direta em `beforeEach` — qualquer uma satisfaz o requisito, desde que produza um identificador real
  para consultar.
- **Dependências disponíveis:** endpoint real (task 2.0), UI real (task 4.0), Booking rodando em
  `:5102` (mesma instância usada pela E2E de F01).
- **Artefatos exigidos pelo gate:** `e2e/reservation-lookup.spec.ts` é criado nesta própria task.
- **Dependências futuras:** Nenhuma — última task da feature.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `scripts/ai-flow/gate.sh --filter="reservation-lookup"`
- [ ] O seletor encontra os 2 cenários Playwright e não executa specs sem relação com esta task
- [ ] Build compila sem erros: `npm run build`
- [ ] Cenário 1: Reservation criada é encontrada em `/reservations/consultar` com todos os campos
      corretos, `status=solicitada`, `sagaStatus=pendente`, `correlationId` visível, sem
      `cancellationReason`
- [ ] Cenário 2: identificador bem formado porém inexistente mostra "não encontrada" (tom neutro)
- [ ] `npm run api:generate` seguido de `git diff --exit-code -- src/services/api/generated/reservationDetail.ts`
      não produz diferença
- [ ] `npm run lint`, `npm run type-check`, `npm run test:coverage`, `npm run build` passam
- [ ] Checkpoint de feedback executado conforme descrito acima
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova a jornada full-stack real; estados `confirmada`/`cancelada` continuam
      provados apenas por RTL+MSW (task 4.0), conforme decisão já registrada
