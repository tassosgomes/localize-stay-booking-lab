# Localize Stay v2

## Brief de Arquitetura e Estudos — v1

**Escopo congelado para início do projeto**

> **Princípio:** reduzir o produto, não a engenharia.  
> O Localize Stay não será um clone de Airbnb/Booking; será um laboratório para praticar decisões arquiteturais reais.

## Objetivo

Construir um projeto-laboratório de arquitetura distribuída com domínio pequeno e profundidade técnica suficiente para estudar integrações, contratos, eventos e evolução de plataforma ao longo do ano.

## 1. Escopo funcional congelado

- Cadastrar e consultar hospedagens/acomodações.
- Consultar disponibilidade básica por período e quantidade de hóspedes.
- Criar uma reserva e executar pagamento simulado.
- Confirmar ou cancelar a reserva conforme o resultado da saga.
- Publicar e consumir eventos de integração.
- Expor contratos OpenAPI, AsyncAPI e Data Contracts.
- Catalogar ativos, ownership e relacionamentos no OpenMetadata.

## 2. Arquitetura inicial

| Domínio / serviço | Responsabilidade | Principais artefatos |
|---|---|---|
| **Catalog** | Hospedagens, unidades e disponibilidade publicada. | `Property`, `Accommodation/Room`, disponibilidade e views publicadas. |
| **Booking** | Reserva, lifecycle e orquestração da saga. | `Reservation`, estados, comandos e eventos. |
| **Payment** | Autorização, rejeição e estorno simulados. | `Payment` e eventos de resultado. |
| **Notification Worker** | Consumidor assíncrono para demonstrar fan-out. | Consome eventos finais; sem regra central de negócio. |

Infraestrutura inicial: uma instância PostgreSQL com ownership lógico por schema (`catalog.*`, `booking.*`, `payment.*`) e um schema de integração para dados publicados.

**Regra:** um serviço não acessa tabelas internas de outro domínio.

## 3. Contratos e estilos de integração

| Necessidade | Mecanismo | Contrato | Exemplo |
|---|---|---|---|
| Chamada síncrona | HTTP | OpenAPI | Criar reserva / consultar hospedagem. |
| Comunicação assíncrona | Broker de eventos | AsyncAPI | `PaymentRequested`, `PaymentAuthorized`, `BookingConfirmed`. |
| Compartilhamento de dados | View / Materialized View | Data Contract | `available_accommodations_v1`, `reservation_calendar_v1`. |

### Regra para Data Contracts

A tabela é implementação interna do domínio; a view publicada é parte da interface de dados.

Consumidores leem somente datasets publicados e contratados.

## 4. Saga principal

```text
1. ReservationRequested
2. Reservar/validar acomodação
3. PaymentRequested
4. PaymentAuthorized -> ReservationConfirmed
5. PaymentRejected ou timeout -> compensação -> ReservationCancelled
```

Pontos obrigatórios de estudo:

- Idempotência.
- Retries.
- Correlation e causation IDs.
- Timeout.
- Compensação.
- Versionamento de eventos.
- Compatibilidade de contratos.

## 5. Fora do escopo inicial

- Pagamentos reais/Stripe.
- Upload de imagens.
- Reviews.
- Mapas e geolocalização avançada.
- Busca Elasticsearch.
- Kubernetes.
- Multi-tenancy.
- Pricing dinâmico.
- Cupons.
- Chat.
- Recommendation engine.
- Observabilidade avançada.
- CDN e multi-cloud.

## 6. Roadmap de evolução

| Fase | Objetivo | Tecnologias / temas possíveis |
|---|---|---|
| **0 — Fundação** | Implementar o escopo congelado e os contratos. | Service-Based Architecture, PostgreSQL, OpenAPI, AsyncAPI, OpenMetadata. |
| **1 — Resiliência** | Tornar a saga robusta e observável. | Outbox, retries, DLQ, tracing, metrics, contract tests. |
| **2 — Busca** | Criar busca desacoplada sem mudar o domínio principal. | Elasticsearch/OpenSearch, CDC, Debezium, Kafka, indexação eventual. |
| **3 — Plataforma cloud** | Explorar implantação e trade-offs de provedores. | AWS, GCP ou Azure; managed DB, broker, containers, secrets, IAM. |
| **4 — Escala e entrega** | Estudar distribuição, performance e operação. | CDN, cache, autoscaling, load tests, SLOs, DR. |

> **Regra para o ano:** cada nova tecnologia entra somente quando resolve um problema que já existe no projeto. O roadmap é uma fila de estudos, não um checklist obrigatório.

## 7. Ordem de implementação para começar

1. Modelar Catalog, Booking e Payment e definir os limites de ownership.
2. Subir PostgreSQL e criar schemas/roles de cada domínio.
3. Definir os primeiros endpoints e publicar OpenAPI.
4. Implementar criação de reserva e a saga com pagamento simulado.
5. Definir eventos e documentá-los com AsyncAPI.
6. Criar o primeiro dataset publicado por view/materialized view e seu Data Contract.
7. Registrar APIs, tópicos/datasets, owners e lineage no OpenMetadata.
8. Adicionar testes de contrato e documentar as decisões arquiteturais principais.

## 8. Definition of Done da primeira etapa

- Uma reserva percorre o fluxo completo de sucesso e falha.
- OpenAPI descreve as APIs públicas relevantes e AsyncAPI descreve eventos/canais.
- Pelo menos um Data Contract protege uma view ou materialized view publicada entre domínios.
- OpenMetadata apresenta os principais ativos, ownership e relacionamentos.
- O README explica por que cada integração usa API, evento ou contrato de dados.
- Não existe funcionalidade fora do escopo congelado na primeira entrega.

## 9. Trilha de publicação no Medium

Cada evolução pode virar um artigo independente, sempre partindo de uma decisão arquitetural concreta:

- **Service-Based Architecture vs. microservices:** por que começar mais simples.
- **OpenAPI, AsyncAPI e Data Contracts:** três contratos para três problemas.
- **Saga, idempotência e compensação sem framework mágico.**
- **Views contratadas e, depois, busca com CDC + Debezium + Kafka + Elasticsearch.**
- **Evoluindo a mesma aplicação para AWS, GCP ou Azure e comparando trade-offs.**
