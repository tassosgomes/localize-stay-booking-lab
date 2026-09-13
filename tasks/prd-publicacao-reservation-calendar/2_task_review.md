# Revisão focused — task 2.0

## Resultado

**VALIDAÇÃO APROVADA** — 0 bloqueantes, 1 recomendação.

## Evidências

- Gate contratual executado antes da revisão:
  `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests.ServiceRoles"`
  → `GATE: APROVADO`; format e build verdes; 6 testes `ServiceRoles_*` passaram contra PostgreSQL 16/Testcontainers.
- Revisado o worktree diff desde `05d9ad99408dafbc8f536f8f7ac34998f35a6cf3`: testes de leitura e das cinco negações para `catalog_role`, `booking_role` e `payment_role`; `SET ROLE` é sempre acompanhado de `RESET ROLE` em `finally`.
- `db/integration/verify-reservation-calendar.sql` valida relkind, allow-list de colunas, grants e negações via `SET LOCAL ROLE`; permanece uma evidência manual complementar, fora do gate.
- `scripts/openmetadata/README.md` documenta a ingestão e o checklist de dataset, owner Booking, tag, descrição/compatibilidade e lineage. A confirmação real no OpenMetadata **não foi executada nem alegada como executada**: depende do homelab/PAT e permanece pendente para o operador, conforme a TechSpec.

## Bloqueantes

Nenhum.

## Recomendação

1. Antes de completar a entrega de governança, o operador deve executar a ingestão e anexar à evidência a confirmação real de owner, descrição e lineage no OpenMetadata; isso não invalida o gate automatizado desta task.
