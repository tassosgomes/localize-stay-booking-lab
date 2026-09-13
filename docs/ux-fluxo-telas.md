# Plano de UX — Fluxo de telas do Localize Stay v2

**Status:** Aprovado para orientar as próximas especificações de frontend
**Data:** 2026-09-12
**Escopo:** frontend de teste/visualização da Fase 0

Este documento define a jornada de telas do Localize Stay v2 a partir do escopo da [Vision](../vision.md),
do [Domain Map](../context/domain-map.md) e dos padrões de interação observados em Airbnb e Booking.com.
O produto continua sendo um laboratório de arquitetura distribuída, não um clone comercial dessas plataformas.

## 1. Objetivo de UX

O frontend deve tornar a jornada de hospedagem fácil de entender e, ao mesmo tempo, permitir verificar o
fluxo distribuído de Catalog, Booking e Payment.

A experiência deve ter duas camadas:

- **Camada de usuário:** busca, escolha, solicitação de reserva e consulta do resultado em linguagem de negócio.
- **Camada de laboratório:** detalhes técnicos opcionais para observar a saga, sem transformar o fluxo principal
  em uma tela de diagnóstico.

O frontend é um cliente fino sobre as APIs contratadas. Ele não decide disponibilidade, pagamento, confirmação
ou cancelamento.

## 2. Referências de mercado e adaptação

Airbnb inicia a busca com destino, datas e quantidade de hóspedes e oferece filtros por preço, capacidade,
comodidades e tipo de acomodação. [Airbnb — filtros de busca](https://www.airbnb.com/help/article/479)

Booking.com segue uma estrutura semelhante, acrescentando a seleção de quartos/unidades, políticas, dados do
viajante e confirmação da reserva. [Booking.com — fluxo de reserva](https://help.business.booking.com/hc/en-us/articles/28518928349204-Make-an-accommodation-booking)

### Padrões adotados

| Padrão | Aplicação no Localize Stay |
|---|---|
| Critérios de viagem antes dos resultados | Destino, período e hóspedes são preenchidos antes da consulta de disponibilidade. |
| Resultados comparáveis | Cards com capacidade, comodidades, disponibilidade e preço total em BRL. |
| Filtros e ordenação | Filtros compatíveis com os dados existentes do Catalog; sem filtros que exijam novos domínios. |
| Detalhes antes da decisão | A hospedagem e a unidade reservável são revisadas antes do checkout. |
| Resumo persistente | Datas, hóspedes e valor total permanecem visíveis durante a solicitação. |
| Resultado acompanhável | A Reservation tem estado claro e pode ser consultada novamente pelo identificador. |

### Padrões deliberadamente não adotados na Fase 0

- Aprovação manual de anfitrião: não existe domínio de Host aprovador.
- Mapa, geolocalização avançada, reviews, chat, recomendações e upload de imagens: estão fora do escopo da Vision.
- Pagamento real: o checkout usa somente um simulador de autorização, rejeição e timeout.
- Login e autenticação: a Fase 0 usa identificadores de referência não autenticados, conforme [PD-002](product-decisions/PD-002-identificacao-atores-sem-autenticacao.md).

## 3. Arquitetura de informação

### Fluxo-alvo

```text
Explorar / Buscar
  ↓
Resultados de disponibilidade
  ↓
Detalhes da hospedagem
  ↓
Checkout: resumo → dados do Guest → pagamento simulado
  ↓
Processamento da saga
  ├─ PaymentAuthorized → ReservationConfirmed
  ├─ PaymentRejected → compensação → ReservationCancelled
  └─ timeout → processamento pendente → estado final confirmado ou cancelado
  ↓
Consulta da Reservation
```

### Telas do fluxo

| Ordem | Tela | Conteúdo essencial | Domínio |
|---:|---|---|---|
| 1 | **Explorar / Buscar** | Destino, check-in, check-out, hóspedes e CTA “Ver disponibilidade”. | Catalog |
| 2 | **Resultados** | Cards de hospedagens, preço total, capacidade, comodidades, filtros, ordenação e estado vazio. | Catalog |
| 3 | **Detalhes da hospedagem** | Property, Accommodation/Room, capacidade, comodidades, período selecionado, preço e CTA “Reservar”. | Catalog |
| 4 | **Checkout — resumo** | Hospedagem, unidade, período, hóspedes e valores congelados. Revalidar disponibilidade ao solicitar. | Booking |
| 5 | **Checkout — Guest** | Identificador e dados sintéticos do Guest de referência. Não há criação de conta. | Booking |
| 6 | **Checkout — pagamento simulado** | Cenário explícito: sucesso, rejeição ou timeout. Nenhum dado de pagamento real. | Payment |
| 7 | **Processando reserva** | Progresso de negócio: validação, pagamento e finalização. Evitar duplo envio. | Booking + Payment |
| 8 | **Resultado** | Reservation confirmada ou cancelada, identificador, hospedagem, período, hóspedes e valor. | Booking |
| 9 | **Consultar reserva** | Busca pontual por identificador, estado atual e situação observável da saga. | Booking |
| 10 | **Gestão do catálogo** | Cadastro/edição de Property, Accommodation e disponibilidade. | Catalog |

`Minhas reservas` pode ser uma evolução posterior. Na Fase 0, a consulta deve permanecer pontual por
identificador, conforme a decisão DP-01 do PRD de [Consulta de Reserva](../tasks/prd-consulta-reserva/prd.md);
uma listagem exigiria novo contrato e nova decisão de produto.

## 4. Estados da jornada

Os estados canônicos continuam sendo os do domínio Booking. “Processando” é apenas uma representação visual de
um estado ainda não concluído.

| Estado exibido | Significado |
|---|---|
| **Solicitada / pagamento pendente** | Reservation criada, aguardando o resultado do Payment. |
| **Confirmada / pagamento autorizado** | Payment autorizado e Reservation confirmada pelo Booking. |
| **Cancelada / pagamento rejeitado** | Payment rejeitado ou timeout compensado; Reservation cancelada. |
| **Não encontrada** | O identificador não corresponde a uma Reservation. É diferente de rejeição de negócio. |
| **Falha temporária** | Não foi possível concluir a comunicação; preservar os dados preenchidos e permitir nova tentativa explícita. |

O frontend não deve inferir um histórico completo de eventos a partir do estado atual. A consulta F02 expõe uma
visão consolidada, não uma auditoria de transições, e não possui atualização automática/tempo real.

## 5. Detalhes técnicos controlados por flag

### Decisão

A seção de detalhes técnicos será controlada por uma flag opcional do frontend:

```text
VITE_SHOW_TECHNICAL_DETAILS=false
```

Valores aceitos: `true` ou `false`, sem diferenciação de maiúsculas/minúsculas. Valor ausente ou inválido deve
ser interpretado como `false` — comportamento seguro e compatível com a experiência de usuário.

### Comportamento

| Flag | Comportamento |
|---|---|
| `false` | Oculta a seção técnica; exibe somente os estados e mensagens de negócio. |
| `true` | Exibe um painel recolhível de diagnóstico na consulta/resultado da Reservation. |

Quando habilitado, o painel pode exibir os dados técnicos já disponíveis no contrato, por exemplo:

- `correlationId` da saga;
- estado atual da Reservation;
- situação atual do Payment;
- `traceId`, quando retornado em uma resposta de erro;
- referência aos eventos relevantes, somente quando existir informação contratada para isso.

O painel não deve exibir stack trace, segredo, credencial ou acesso direto ao banco. Também não deve inventar um
histórico de eventos que a API não fornece.

### Limites da flag

- A flag altera somente a apresentação do frontend.
- Não altera regras de negócio, chamadas, payloads ou contratos OpenAPI.
- Não é mecanismo de autenticação ou autorização.
- Deve permanecer desligada em uma experiência de usuário comum.
- Em desenvolvimento do laboratório, pode ser ativada no `.env.development` ou na execução do Vite.

Exemplo:

```bash
VITE_SHOW_TECHNICAL_DETAILS=true npm run dev
```

Quando a flag for implementada, a leitura deve ficar centralizada em `src/config/env.ts`, e os testes devem
cobrir os dois valores. O componente técnico deve ser renderizado condicionalmente, sem duplicar a lógica de
Booking, Payment ou Catalog.

## 6. Notification e observabilidade

Notification continua sendo um consumidor assíncrono terminal. A confirmação ou o cancelamento da Reservation
devem ser apresentados pelo Booking; falha de notificação não deve reverter o estado da reserva.

Conforme [ADR-003](adr/adr-003-frontend-teste-react.md), a Fase 0 não cria uma tela de leitura do Notification
Worker. A observação da entrega de notificações permanece nos logs do worker ou em ferramentas de diagnóstico.
Caso a flag esteja habilitada, o frontend pode indicar apenas um status de notificação se esse dado vier de um
contrato existente; não deve criar uma API paralela de Notification.

## 7. Regras de usabilidade

- Sempre manter visíveis o período, a quantidade de hóspedes e o valor total da solicitação.
- Preservar os dados do formulário em erros de validação ou indisponibilidade temporária.
- Distinguir visual e textualmente “não encontrada”, “indisponível”, “pagamento rejeitado” e “falha técnica”.
- Desabilitar o CTA durante uma submissão e impedir duplo envio acidental.
- Mostrar confirmação somente quando o Booking retornar `ReservationConfirmed`/estado `confirmada`.
- Depois de uma falha final, oferecer nova tentativa explícita, sem criar automaticamente uma segunda Reservation.
- Garantir que o estado consultado possa ser recuperado após refresh usando o identificador da Reservation.
- Usar dados fictícios e moeda BRL, conforme [PD-001](product-decisions/PD-001-moeda-unica-laboratorio.md).

## 8. Priorização

### Já materializado ou contratado

- Gestão/cadastro de Property em `/properties`.
- Solicitação de Reservation em `/reservations`.
- Consulta pontual de Reservation em `/reservations/consultar`.

### Próximas fatias de UX

1. Busca e resultados usando Catalog.
2. Detalhes da hospedagem e seleção da Accommodation.
3. Checkout integrado à solicitação de reserva.
4. Pagamento simulado e tela de processamento.
5. Resultado final com o painel técnico opcional.

Cada fatia deve gerar ou atualizar seu PRD, TechSpec, contrato e testes correspondentes. Este documento orienta
a experiência e não substitui esses artefatos.
