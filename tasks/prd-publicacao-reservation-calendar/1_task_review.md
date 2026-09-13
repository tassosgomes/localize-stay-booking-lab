# Revisão focada — Task 1.0

## Escopo e evidências

- Revisado: `db/integration/001-reservation-calendar-v1.sql` e a fixture/coleção/classe `ReservationCalendar*` criadas em `services/booking/tests/.../Reservations/`.
- Base/HEAD: `9f103c05380abc138765c259f6625092818afd37`; os quatro artefatos de implementação estão untracked e pertencem ao escopo da task.
- Gate executado antes da revisão: `scripts/ai-flow/gate.sh --sln=services/booking/LocalizeStay.Booking.sln --filter="FullyQualifiedName~LocalizeStay.Booking.IntegrationTests.Reservations.ReservationCalendarContractTests"`.
- Resultado do gate: `GATE: APROVADO`; format e build verdes, 7 testes focalizados aprovados em PostgreSQL Testcontainers.

## Revisão

- O DDL é transacional, idempotente e usa projeção explícita de `booking.reservations`, as sete colunas ordenadas e o filtro terminal prescrito. Os comentários e os três `GRANT SELECT` estão presentes.
- A fixture aplica migrations reais com PostgreSQL 16, provisiona schema/roles descartáveis e carrega o DDL versionado do disco.
- Os testes cobrem relkind/schema, exclusão de solicitada, cardinalidade/fidelidade/período, leitura pós-commit por conexão nova, invariância contra transição tardia, allow-list e reaplicação do DDL.

## Bloqueantes

Nenhum.

## Recomendações

Nenhuma. A validação dos grants negativos e de isolamento entre schemas permanece corretamente para a task 2.0.

## Veredito

VALIDAÇÃO APROVADA — 0 bloqueantes, 0 recomendações.
