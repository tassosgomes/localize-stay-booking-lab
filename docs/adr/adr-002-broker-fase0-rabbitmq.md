# ADR-002: Broker de eventos da Fase 0 — RabbitMQ

## Status

Accepted — 2026-09-11

## Contexto

O `docs/brief.md` (seção 3) exige que toda comunicação assíncrona entre domínios seja feita via "broker de
eventos" e documentada em AsyncAPI, mas não nomeia a tecnologia — a escolha do broker concreto é uma decisão
de arquitetura, não de produto.

A saga principal descrita no brief (seção 4) e no `context/domain-map.md` depende desse broker desde a
Fase 0: Booking publica `PaymentRequested`, Payment reage e publica `PaymentAuthorized`/`PaymentRejected`,
Booking reage para confirmar/cancelar a reserva e publica os eventos finais consumidos por Notification.

O roadmap (`vision.md`, seção 4) só introduz Kafka explicitamente na **Fase 2** (Busca, junto com CDC/Debezium),
e reserva Outbox/DLQ para a **Fase 1** (Resiliência) — ou seja, a Fase 0 não tem, ainda, o problema que Kafka
resolve (streaming de CDC em alto volume) nem a robustez de entrega que o Outbox/DLQ da Fase 1 endereça. Adotar
Kafka já na Fase 0 anteciparia complexidade operacional (particionamento, Zookeeper/KRaft) sem um problema atual
que a justifique — o que contraria a regra do roadmap ("cada nova tecnologia entra somente quando resolve um
problema que já existe no projeto") e o princípio de evitar otimização prematura desta etapa.

## Decisão

A Fase 0 usa **RabbitMQ** como broker de eventos para a saga Booking ↔ Payment ↔ Notification, com exchanges e
filas modeladas 1:1 com os eventos contratados em AsyncAPI (`PaymentRequested`, `PaymentAuthorized`,
`PaymentRejected`, `ReservationConfirmed`, `ReservationCancelled`).

Kafka permanece reservado para a Fase 2, como peça independente do pipeline de CDC/busca (não substitui o
RabbitMQ da saga; os dois podem coexistir, cada um resolvendo um problema diferente).

## Alternativas consideradas

- **Kafka desde a Fase 0** — unificaria o broker desde o início, mas introduz complexidade operacional
  (partições, consumer groups, Zookeeper/KRaft) antes de existir volume ou caso de uso (CDC) que a justifique.
- **Azure Service Bus / AWS SNS-SQS / NATS** — viáveis, mas exigiriam ambiente cloud ou infraestrutura gerenciada
  já na Fase 0, o que a Vision reserva explicitamente para as Fases 3/4 (Plataforma cloud).

## Consequências

- O AsyncAPI da Fase 0 descreve exchanges/filas do RabbitMQ (binding AMQP) para cada evento da saga.
- A introdução de Outbox (Fase 1) se apoia no publisher RabbitMQ já existente, sem trocar de broker.
- A introdução de Kafka (Fase 2) é aditiva (para CDC/indexação), não uma substituição do RabbitMQ da saga; se o
  projeto decidir futuramente unificar em um único broker, isso exige uma nova ADR marcando esta como
  Superseded.
