---
status: done
slice_type: vertical
verification_type: behavioral
parallelizable: false
blocked_by: [3.0, 5.0]
---

<task_context>
<domain>frontend/reservation-request/e2e</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server,database,external_apis</dependencies>
<unblocks>""</unblocks>
<feedback_checkpoint>`scripts/ai-flow/gate.sh --filter="reservation-request"` executa a suíte Playwright `e2e/reservation-request.spec.ts` contra Booking + Catalog reais (sucesso + `PERIODO_INVALIDO`), confirma ID/status/valores congelados no browser real e roda o gate completo (lint, type-check, coverage, build, `api:generate` sem drift)</feedback_checkpoint>
<gate_command>scripts/ai-flow/gate.sh --filter="reservation-request"</gate_command>
<gate_test_selector>Arquivo `frontend/localize-stay-frontend/e2e/reservation-request.spec.ts`</gate_test_selector>
<gate_expected_result>Os 2 cenários Playwright passam contra backend real (sucesso confirma ID/status `solicitada`/preço-moeda-total exibidos; rejeição `PERIODO_INVALIDO` não cria Reservation e mostra o erro correto); `npm run api:generate` seguido de `git diff --exit-code -- src/services/api/generated/booking.ts` não produz diferença; lint, type-check, `test:coverage` e `build` passam</gate_expected_result>
<static_evidence>N/A — behavioral</static_evidence>
<vertical_slice>Um browser real solicita uma reserva válida contra Booking + Catalog reais e confirma ID/status/valores congelados exibidos; um segundo cenário confirma que uma rejeição representativa (`PERIODO_INVALIDO`) não cria Reservation nenhuma — fecha RF-01 nos dois lados, provando que UI e backend concordam fora de mocks</vertical_slice>
</task_context>

# Tarefa 6.0: Provar a jornada full-stack com Playwright e fechar o gate (V-FE-02)

## Relacionada às User Stories

- "Como Guest, eu quero solicitar uma reserva... para que meu pedido seja registrado com o preço
  vigente garantido" (cobertura E2E — confirmação final contra sistemas reais)
- "Como autor/arquiteto em estudo, eu quero que a validação de disponibilidade aconteça via chamada
  síncrona contratada a Catalog..." (cobertura E2E — fecha o ciclo de ponta a ponta observável no
  browser, não só em testes de integração de backend)
- "Como frontend de teste, eu quero enviar a solicitação de reserva e exibir o resultado..."
  (cobertura E2E — última confirmação de que a UI (5.0) e o backend (3.0) concordam fora de mocks)

## Visão Geral

Fecha RF-01 em ambos os lados: até aqui, o backend foi provado por testes de integração (3.0) e a UI
foi provada com MSW (5.0), mas nenhuma task anterior confirmou que os dois concordam contra sistemas
reais. Esta task adiciona `e2e/reservation-request.spec.ts` (Playwright) rodando contra Booking F01
completo (3.0) + Catalog real (ou stub equivalente já materializado por outra worktree), cobrindo o
caminho de sucesso e uma rejeição representativa, e executa o gate completo do frontend (lint,
type-check, cobertura, build, verificação de drift do contrato gerado). É a única task que depende de
artefatos de backend e de frontend simultaneamente, porque é o único ponto que prova a jornada
full-stack — nenhuma task anterior comprova UI+backend juntos.

## Entrega Observável

- **Entrada ou gatilho:** Playwright abre `/reservations` num browser real, preenche o formulário com
  uma Accommodation existente em Catalog (fixture/seed já disponível no ambiente de E2E) e submete;
  repete para um período inválido (`checkOut <= checkIn`).
- **Resultado esperado:**
  - Cenário 1 (sucesso): a UI mostra `ReservationResultSummary` com um ID real, status `solicitada` e
    os valores de preço/moeda/total efetivamente devolvidos pelo Booking real (que por sua vez veio
    de Catalog real); a Reservation existe de fato (confirmável via chamada HTTP direta ao Booking no
    teste, se necessário).
  - Cenário 2 (rejeição `PERIODO_INVALIDO`): a UI mostra o erro associado a `checkIn`/`checkOut` e
    nenhuma Reservation é criada — nenhuma chamada a Catalog ocorre (mesma garantia de RF-01, agora
    observável no browser).
  - `npm run api:generate` seguido de `git diff --exit-code -- src/services/api/generated/booking.ts`
    não produz diferença (contrato gerado permanece sincronizado com `api-contract.yaml`).
- **Checkpoint de feedback:** `gate.sh --filter="reservation-request"` (Playwright real) + gate
  completo do frontend (lint/type-check/coverage/build/drift).
- **Seletor focalizado:** `e2e/reservation-request.spec.ts`.
- **Fora deste checkpoint:** os outros 5 cenários de rejeição (já cobertos por MSW em 5.0, não
  repetidos em E2E); consulta de Reservation (F02); saga de pagamento (F03).

## Requisitos

- `e2e/reservation-request.spec.ts` roda contra o app real (`npm run dev`/preview) apontando
  `VITE_BOOKING_API_URL` para o Booking real de 3.0, com Catalog real ou stub equivalente já
  disponível no ambiente de E2E — não usar MSW/Prism nesta task (isso é dev/teste automatizado,
  cobertos em 4.0/5.0).
- Cenário de sucesso usa uma Accommodation existente e válida (fixture/seed do ambiente de E2E,
  fora do escopo desta task criar) e confirma os 3 valores congelados exibidos (preço por noite,
  moeda `BRL`, total) batem com o que o Booking real retornou.
- Cenário de rejeição usa `PERIODO_INVALIDO` como caso representativo (escolhido porque, junto com
  `QUANTIDADE_HOSPEDES_INVALIDA`, é o único rejeitado sem round-trip a Catalog — mais rápido e
  determinístico em E2E); os demais 5 cenários de erro não são repetidos aqui, já provados por MSW em
  5.0.
- Nenhum novo componente de produção é criado nesta task — só o spec Playwright e, se necessário,
  ajustes de configuração de ambiente de E2E (`playwright.config.ts`, se ainda não cobrir esta rota).
- Executar o gate completo do frontend nesta task (não só o E2E): `npm run lint`, `npm run
  type-check`, `npm run test:coverage`, `npm run build`, `npm run api:generate` +
  `git diff --exit-code`, `npm run test:e2e` — mesma sequência de `frontend-techspec.md` §Gate e
  Contrato, adaptada aos nomes de script reais do `package.json` do worktree.

## Arquivos Envolvidos

- **Criar:**
  - `frontend/localize-stay-frontend/e2e/reservation-request.spec.ts`
- **Modificar:**
  - `frontend/localize-stay-frontend/playwright.config.ts` (apenas se a rota/base URL de E2E ainda
    não estiver configurada; reaproveitar se `prd-cadastro-property` já a configurou)
- **Referência:**
  - `frontend/localize-stay-frontend/src/features/reservation-request/**` (5.0) — seletores/labels da
    UI a exercitar
  - `services/booking/**` (3.0) — endpoint real a consumir
  - `tasks/prd-solicitacao-reserva/api-contract.yaml` — valores esperados de resposta
  - `tasks/prd-cadastro-property/5_task.md` — mesmo padrão de E2E já aplicado neste frontend
    (`PropertyUpdate.spec.ts`), reaproveitar convenções de fixture/setup de ambiente
- **Skills para consultar durante implementação:**
  - `react-testing` — Playwright, fixtures, isolamento de ambiente E2E
  - `test-guide` — quando um comportamento pertence a E2E vs. integração/unitário

## Subtarefas

- [ ] 6.1 Confirmar/ajustar `playwright.config.ts` para apontar ao app real com
      `VITE_BOOKING_API_URL`/Catalog reais e uma Accommodation fixture disponível
- [ ] 6.2 Escrever o cenário de sucesso: preencher, submeter, confirmar ID/status/valores congelados
      exibidos
- [ ] 6.3 Escrever o cenário de rejeição `PERIODO_INVALIDO`: preencher com `checkOut<=checkIn`,
      confirmar erro na UI e nenhuma Reservation criada
- [ ] 6.4 Executar o gate completo do frontend (lint, type-check, coverage, build, drift, E2E) e
      registrar a evidência

## Sequenciamento

- Bloqueado por: 3.0 (endpoint `POST /v1/reservations` completo e real), 5.0 (jornada de UI completa)
- Desbloqueia: nenhuma task desta feature (fecha RF-01 nos dois lados); externamente, F02/F03 do
  domínio Booking e a evolução do frontend de teste passam a ter uma jornada full-stack de referência
- Paralelizável: Não — é o ponto de convergência final, depende de ambas as fatias anteriores

## Rastreabilidade

- Esta tarefa cobre: V-FE-02 da `frontend-techspec.md`; fecha RF-01 completo (User Story do
  "frontend de teste") com evidência de browser real; confirma ausência de drift entre contrato e
  tipos gerados.
- Evidência esperada: `reservation-request.spec.ts` verde para os 2 cenários contra sistemas reais;
  `git diff --exit-code` sem diferença em `booking.ts`; gate completo do frontend verde.

## Detalhes de Implementação

Estrutura do spec (`frontend-techspec.md` §Abordagem de Testes → E2E):

```ts
test("Guest solicita reserva válida e vê o resultado congelado", async ({ page }) => {
  await page.goto("/reservations");
  // preencher accommodationId (fixture), guestReference, checkIn, checkOut, guestsCount válidos
  // submeter
  // esperar ReservationResultSummary: id, status "solicitada", pricePerNight, currency "BRL", totalAmount
});

test("Período inválido é rejeitado sem criar Reservation", async ({ page }) => {
  await page.goto("/reservations");
  // preencher com checkOut <= checkIn
  // submeter
  // esperar erro associado a checkIn/checkOut; nenhuma navegação para o resumo de sucesso
});
```

Este é o único checkpoint da feature que roda contra Booking e Catalog reais simultaneamente — os
outros 5 cenários de erro (`QUANTIDADE_HOSPEDES_INVALIDA`, `ACOMODACAO_INDISPONIVEL`,
`CAPACIDADE_EXCEDIDA`, `PERIODO_INDISPONIVEL`, `CATALOG_INDISPONIVEL`, `500`) permanecem cobertos
apenas por MSW em 5.0, conforme decisão já registrada na TechSpec frontend (E2E cobre "sucesso + uma
rejeição representativa", não os 7 cenários).

**Convenções da stack:**
- Playwright contra app real, sem MSW (`react-testing`).
- Gate completo executado nesta task, não só o E2E — é o checkpoint final da feature no frontend
  (`frontend-techspec.md` §Gate e Contrato).

## Prontidão para Implementação

- **Decisões fechadas:** E2E cobre exatamente 2 cenários (sucesso + `PERIODO_INVALIDO`), não os 7;
  ambiente de E2E usa Booking + Catalog reais, nunca MSW/Prism; esta task não cria novos componentes
  de produção.
- **Limites de decisão do implementer:** mecanismo exato de fixture/seed da Accommodation usada no
  E2E (reaproveitar o que `prd-cadastro-property`/Fundação já disponibilizam para o ambiente de E2E,
  sem recriar infraestrutura de seed nesta task).
- **Dependências disponíveis:** endpoint real de Booking (3.0), UI completa (5.0), Catalog real
  (dependência externa já assumida pelas TechSpecs).
- **Artefatos exigidos pelo gate:** `reservation-request.spec.ts` é criado nesta própria task; app,
  UI e endpoint já existem de 3.0/5.0.
- **Dependências futuras:** Nenhuma — esta task fecha RF-01 por completo nos dois lados.
- **Ambiguidades bloqueantes:** Nenhuma.

## Critérios de Sucesso (Verificáveis)

- [ ] Teste focalizado passa: `scripts/ai-flow/gate.sh --filter="reservation-request"`
- [ ] O seletor encontra os 2 testes do spec e não executa suítes sem relação com esta task
- [ ] Build compila sem erros; lint e type-check passam
- [ ] Cenário de sucesso confirma ID, status `solicitada` e valores congelados exibidos, batendo com
      a resposta real do Booking
- [ ] Cenário de rejeição confirma que nenhuma Reservation é criada para `PERIODO_INVALIDO`
- [ ] `npm run api:generate` seguido de `git diff --exit-code -- src/services/api/generated/booking.ts`
      não produz diferença
- [ ] `npm run test:coverage` permanece ≥70% statements/branches/functions/lines na feature
- [ ] Checkpoint de feedback executado: `gate.sh --filter="reservation-request"` → verde (2 cenários)
- [ ] Todos os artefatos usados pelo gate existem antes da task ou foram criados/modificados nela
- [ ] Nenhum arquivo produzido por task futura é necessário para compilar ou validar esta task
- [ ] A evidência acima prova RF-01 completo full-stack (UI real + backend real), fechando a feature
