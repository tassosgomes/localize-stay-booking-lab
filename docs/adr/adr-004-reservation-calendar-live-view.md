# ADR-004: Publicação do calendário como view PostgreSQL ao vivo

## Status

Accepted

## Identidade e escopo

- Caminho: docs/adr/adr-004-reservation-calendar-live-view.md
- Domínios/componentes afetados: Booking, schema PostgreSQL integration e consumidores do dataset reservation_calendar_v1
- Origem histórica: tasks/prd-publicacao-reservation-calendar
- Substitui: Nenhuma

## Data

2026-09-13

## Contexto

F05 precisa publicar o snapshot atual das Reservations terminais sem permitir acesso dos consumidores às
tabelas internas booking. O contrato exige que uma transição terminal persistida seja visível na próxima
leitura bem-sucedida, sem lote diário, refresh manual, push, histórico ou novo evento.

O baseline já define integration como o schema de exposição para dados compartilhados, impede CREATE para
as roles de serviço e mantém o ownership do schema com o operador de bootstrap. A Fase 0 opera em escala de
laboratório e não tem requisito de latência, throughput ou alta disponibilidade que justifique uma
projeção materializada.

## Decisão

reservation_calendar_v1 será publicado como uma view PostgreSQL regular em
integration.reservation_calendar_v1. A view:

- projeta somente as sete colunas do Data Contract a partir de booking.reservations;
- filtra status em confirmada e cancelada;
- não faz join com reservation_sagas e não expõe detalhes internos;
- expõe booking.reservations.terminal_transition_at como updated_at;
- é criada ou substituída por SQL versionado, executado pelo operador de implantação depois das migrations
  do schema booking;
- recebe GRANT SELECT explícito para catalog_role, booking_role e payment_role; nenhuma role de serviço
  pode criar, substituir ou escrever no objeto.

O uso de uma view regular elimina uma etapa de refresh: uma leitura depois do commit da transição consulta a
fonte atual. Mudanças incompatíveis criam reservation_calendar_v2 ao lado de v1; a definição de v1 só pode
mudar de forma compatível com suas colunas e semântica.

## Alternativas Consideradas

### Alternativa 1: Materialized view

- **Descrição:** armazenar uma cópia física do snapshot em integration e atualizá-la após mudanças na origem.
- **Prós:** leituras potencialmente mais baratas e desacopladas de scans frequentes na tabela de Booking.
- **Contras:** exige refresh, cria uma janela de defasagem e introduz uma nova operação de coordenação que
  não é coberta por F05.
- **Por que rejeitada:** contradiz a visibilidade na próxima leitura sem lote ou republicação manual e
  antecipa uma necessidade de escala não existente na Fase 0.

### Alternativa 2: Tabela publicada preenchida por processo de cópia

- **Descrição:** manter uma tabela em integration e replicar linhas por aplicação, job ou evento.
- **Prós:** permite controlar a carga de leitura e preservar uma cópia independente da origem.
- **Contras:** exige mecanismo de sincronização, idempotência e tratamento de falhas; amplia F05 para
  Outbox, CDC, job ou broker adicional.
- **Por que rejeitada:** o PRD exclui esses mecanismos e o baseline determina que a tabela interna do dono
  é a fonte, enquanto a view contratada é a interface.

## Consequências

### Positivas

- O snapshot fica visível imediatamente após o commit da transição terminal.
- A fronteira de dados é explícita: consumidores recebem somente a allow-list do contrato e não precisam
  de permissão no schema booking.
- A implantação é idempotente e não adiciona um componente de runtime ao serviço Booking.

### Negativas

- Cada leitura executa a projeção sobre booking.reservations; a view não é uma otimização de leitura.
- O dataset depende da disponibilidade e do schema interno de Booking para ser consultado.
- A criação ou alteração da view exige uma etapa de implantação com privilégio de DDL, fora do boot do
  serviço e fora das roles consumidoras.

### Riscos

- Uma alteração incompatível na definição de v1 quebraria consumidores; o gate deve comparar a lista e os
  tipos das colunas, e uma mudança incompatível deve abrir v2.
- Consultas futuras em escala podem pressionar Booking; esse problema deve ser medido antes de trocar para
  materialização em uma nova ADR.

## Notas de Implementação

O SQL de publicação deve executar depois da migration que cria
booking.reservations.terminal_transition_at. Deve usar nomes totalmente qualificados, comentários de view e
colunas para o catálogo, e GRANT SELECT explícito. A execução real contra postgres-main continua manual,
conforme o README de db/bootstrap; o gate usa um PostgreSQL descartável.

## Referências

- context/architecture-baseline.md, seções Regras de Propriedade dos Dados e Padrões de Comunicação
- contracts/data-contracts/reservation_calendar_v1.md
- tasks/prd-publicacao-reservation-calendar/api-contract.yaml
- tasks/prd-publicacao-reservation-calendar/prd.md
