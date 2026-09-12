# Domain Map

## Visão geral

O Localize Stay v2 é um laboratório de arquitetura distribuída cujo escopo funcional está deliberadamente congelado (`vision.md`, `docs/brief.md`): cadastro/consulta de hospedagens, consulta de disponibilidade, criação de reserva e uma saga de pagamento simulado (autorização → confirmação, ou rejeição/timeout → compensação/cancelamento), com integração sempre via contrato explícito (OpenAPI, AsyncAPI, Data Contract) e catalogação no OpenMetadata.

A partir dos fluxos de valor da Fase 0 — (1) manter hospedagens e disponibilidade, (2) criar e conduzir o ciclo de vida da reserva, (3) simular pagamento e compensação, (4) notificar partes interessadas sobre o resultado final — quatro domínios conceituais emergem, coincidindo com os serviços candidatos já apontados no brief: **Catalog**, **Booking**, **Payment** e **Notification**. Esta decomposição valida e formaliza essas fronteiras à luz da linguagem ubíqua e das regras de ownership de dados descritas na Vision, sem entrar em arquitetura física, contratos técnicos ou tarefas de execução — isso é responsabilidade de etapas posteriores (baseline arquitetural, PRDs, contratos).

Dois assuntos citados na Vision são explicitamente tratados como fora desta decomposição de domínios de negócio: autenticação/autorização (decisão adiada para etapas de arquitetura) e a catalogação de ativos no OpenMetadata (prática transversal de governança, não um domínio em si).

## Domínios

### Catalog

- **Responsabilidade:** Manter o catálogo de hospedagens e suas unidades, e expor a disponibilidade por período e quantidade de hóspedes.
- **O que não faz:** Não cria nem conduz reservas, não decide confirmação/cancelamento, não processa pagamento, não envia notificações.
- **Entidades conceituais:** Property (hospedagem), Accommodation/Room (unidade reservável), Availability (janela de disponibilidade por período e capacidade).
- **Linguagem ubíqua:** Property, Accommodation/Room, disponibilidade, `available_accommodations_v1`.
- **Interações:** É consultado por Booking no momento de criação da reserva, para validar se a acomodação existe e está disponível no período solicitado; publica um dataset contratado de disponibilidade para consumo por outros domínios (hoje nenhum consumidor formal além de Booking; candidato natural para a futura Busca da Fase 2).
- **Justificativa:** Tem motivo de mudança independente (evolução do portfólio de hospedagens e regras de disponibilidade) e é o único dono legítimo da verdade sobre o que existe e o que está livre — misturar essa responsabilidade com Booking acoplaria o ciclo de vida do catálogo ao da reserva.

### Booking

- **Responsabilidade:** Orquestrar o ciclo de vida da reserva (solicitada → confirmada/cancelada) e coordenar a saga de ponta a ponta com Payment.
- **O que não faz:** Não processa pagamento, não mantém o catálogo de hospedagens/disponibilidade, não envia notificações às partes interessadas.
- **Entidades conceituais:** Reservation (com seus estados de ciclo de vida), Saga/estado de orquestração da reserva.
- **Linguagem ubíqua:** Reservation, Saga, `ReservationRequested`, `ReservationConfirmed`, `ReservationCancelled`, `reservation_calendar_v1`.
- **Interações:** Consulta Catalog para validar acomodação/disponibilidade ao criar uma reserva; solicita autorização a Payment e reage aos seus resultados para confirmar ou compensar/cancelar a reserva; publica os eventos finais consumidos por Notification; publica o dataset contratado de calendário de reservas.
- **Justificativa:** É o orquestrador central da transação de negócio do sistema; precisa de fronteira própria para evoluir os aspectos de resiliência da saga (Fase 1: idempotência, retries, compensação) sem acoplar-se aos internos de Catalog ou Payment.

### Payment

- **Responsabilidade:** Simular autorização, rejeição e estorno (compensação) de pagamento associados a uma reserva.
- **O que não faz:** Não decide o estado final da reserva, não mantém catálogo de hospedagens, não notifica o hóspede/anfitrião diretamente.
- **Entidades conceituais:** Payment (com seus estados: autorizado, rejeitado, estornado/compensado).
- **Linguagem ubíqua:** Payment, `PaymentRequested`, `PaymentAuthorized`, `PaymentRejected`, compensação/estorno.
- **Interações:** Reage ao `PaymentRequested` publicado por Booking; publica `PaymentAuthorized`/`PaymentRejected`, consumidos por Booking para avançar ou compensar a saga.
- **Justificativa:** Isola as regras e os modos de falha específicos de pagamento (rejeição, timeout, estorno) do fluxo de orquestração de Booking, reproduzindo a fronteira real entre uma plataforma de reservas e um processador de pagamento — ponto central de estudo de saga e compensação do laboratório.

### Notification

- **Responsabilidade:** Consumir os eventos finais do fluxo de reserva (confirmação/cancelamento) e disparar as notificações correspondentes às partes interessadas.
- **O que não faz:** Não decide regras de negócio de reserva ou pagamento, não é fonte de verdade para nenhum estado central do domínio; apenas reage a eventos já concluídos.
- **Entidades conceituais:** Notification (mensagem/registro de envio associado ao evento de origem).
- **Linguagem ubíqua:** Notification, evento consumido (`ReservationConfirmed`, `ReservationCancelled`).
- **Interações:** Consome eventos publicados por Booking (e indiretamente originados por Payment); não publica eventos consumidos por outros domínios — é o fim da cadeia de integração assíncrona.
- **Justificativa:** Ainda que o brief a descreva como "sem regra central de negócio", tem motivo de mudança próprio (canais e formatos de notificação) e existe especificamente para demonstrar fan-out assíncrono desacoplado do núcleo de negócio — objetivo de estudo explícito da Fase 0. Mantê-la fundida a Booking acoplaria uma preocupação de apresentação/entrega a um domínio central de orquestração.

## Dependências entre domínios

| Origem | Destino | Interação de negócio | Responsabilidade dos dados |
|---|---|---|---|
| Booking | Catalog | Validar acomodação e disponibilidade ao criar uma reserva | Catalog é dono de Property/Accommodation/Availability |
| Booking | Payment | Solicitar autorização de pagamento para uma reserva | Payment é dono do registro de Payment |
| Payment | Booking | Informar autorização/rejeição para avançar ou compensar a saga | Booking é dono do estado de Reservation |
| Booking | Notification | Notificar partes interessadas sobre o resultado final da reserva | Notification é dono do registro de notificação |
| Catalog | (futuros consumidores, ex.: Busca — Fase 2) | Consumir disponibilidade publicada sem acoplar ao domínio de origem | Catalog permanece dono de `available_accommodations_v1` |

## Pontos de atenção

- **Notification como domínio de suporte:** é o menor domínio identificado e não decide regra de negócio central, o que o torna candidato natural a uma pergunta de "granularidade excessiva" (Regra 3). Foi mantido separado porque tem motivo de mudança distinto e é o objeto de estudo explícito de fan-out assíncrono da Fase 0 — não uma sobra técnica.
- **Validação síncrona Booking → Catalog vs. leitura do dataset publicado:** o brief lista "criar reserva" como chamada síncrona (OpenAPI) mas também define `available_accommodations_v1` como dataset publicado. Este mapa assume que a validação pontual de disponibilidade no momento da reserva é síncrona, e o dataset publicado serve consumidores mais amplos (ex.: busca futura). Esta é uma decisão de integração técnica e deve ser confirmada/formalizada no baseline arquitetural, não nesta etapa.
- **Auth/Identidade não modelada:** a Vision adia explicitamente a decisão de autenticação/autorização; nenhum domínio de Identity foi criado aqui para não antecipar essa decisão.
- **OpenMetadata/Governança não é um domínio:** catalogação de ativos, ownership e relacionamentos é uma prática transversal aplicada sobre os quatro domínios de negócio, não um bounded context próprio.
- **Nenhum candidato a fusão:** os quatro domínios têm responsabilidades, linguagens e motivos de mudança distintos; não há sobreposição identificada.
- **Nenhum candidato a divisão nesta fase:** a saga de resiliência (Fase 1: idempotência, retries, DLQ) permanece dentro da fronteira de Booking/Payment — não justifica um domínio "Saga" separado, pois a orquestração é intrínseca ao ciclo de vida da reserva.

## Decisões estruturais conceituais

- Mantidos os quatro domínios já apontados no brief (Catalog, Booking, Payment, Notification), validados contra `vision.md`: cada um tem responsabilidade única, linguagem ubíqua própria e motivo de mudança distinto.
- Notification tratado como domínio de suporte (supporting subdomain), não core, mas preservado como fronteira própria pelo valor de estudo de fan-out assíncrono, e não fundido a Booking.
- Identity/Auth e Governança/Catalogação (OpenMetadata) não entram como domínios de negócio nesta decomposição — a primeira por ser decisão de arquitetura ainda em aberto, a segunda por ser prática transversal sobre os demais domínios.
- O modo exato de validação de disponibilidade (chamada síncrona vs. dataset publicado) fica registrado como ponto em aberto para o baseline arquitetural, não como decisão de fronteira de domínio.
