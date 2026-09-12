# PD-002: Identificação de atores sem autenticação

- **Status**: Accepted
- **Escopo**: Global
- **Data**: 2026-09-12
- **Responsável pela decisão**: Tasso Gomes (autor do laboratório)
- **Origem**: Discovery do PRD de Booking F01 — Solicitação de Reserva
- **Tags**: identidade, guest, host, booking, catalog
- **Substitui**: Não aplicável
- **Substituído por**: Não aplicável

## Contexto

`context/architecture-baseline.md` define que a Fase 0 não implementa autenticação/autorização, mas
nenhum documento anterior explica como uma feature de produto referencia um ator (Guest, Host) sem
um mecanismo de identidade real. `domains/catalog/domain.md` já assume "identidade de Host já
resolvida por outro mecanismo" para RN-07, e `domains/booking/domain.md` exige que toda Reservation
tenha um "Guest de referência" (RN-01). Sem uma regra explícita, cada PRD poderia inventar uma forma
diferente de tratar esse identificador, quebrando consistência entre domínios.

## Decisão

Enquanto a Fase 0 não tiver autenticação/autorização, todo ator de negócio (Guest, Host) é
referenciado por um identificador informado pelo próprio solicitante da operação (ex.: ao criar uma
Reservation, quem chama informa qual é o Guest de referência). Esse identificador é tratado como
dado de referência simples: o sistema não valida que o identificador corresponde a uma identidade
real, não autentica quem o está enviando, e não impede um solicitante de informar qualquer valor.
Este é um limite deliberado do laboratório nesta fase, não uma falha a ser corrigida por cada
feature individualmente.

## Alternativas consideradas

- **Modelar um domínio mínimo de Identity/Guest/Host já na Fase 0** — rejeitado por antecipar
  tecnologia/escopo sem um problema atual que a justifique (regra do roadmap da Vision) e por
  `context/domain-map.md` já registrar explicitamente que Identity/Auth não é modelado nesta etapa.
- **Bloquear a feature até existir mecanismo de identidade** — rejeitado por inviabilizar todo o
  escopo funcional congelado da Fase 0 (Booking, Catalog e Payment dependem de referenciar atores).

## Consequências

- **Positivas**: destrava a especificação de RF que referenciam Guest/Host sem reabrir a decisão de
  autenticação em cada PRD; mantém consistência entre Catalog e Booking.
- **Negativas ou riscos**: qualquer solicitante pode se passar por qualquer Guest/Host; aceitável
  apenas porque não há dados reais, PII real nem pagamento real em jogo (non-goals da Vision).
- **Impacto em futuros PRDs**: todo PRD que referencie um ator deve tratá-lo como identificador de
  referência não autenticado, sem inventar validação de identidade própria; a introdução de
  autenticação real é uma decisão de arquitetura nova (ADR), não uma extensão silenciosa deste PD.

## Termos e documentos afetados

- **Termos canônicos**: "Guest de referência" (Booking) e "Host responsável" (Catalog) significam
  identificador informado pelo solicitante, sem validação de identidade.
- **Vision/Domain Docs**: consistente com `context/architecture-baseline.md` (Princípios de
  Segurança) e `context/domain-map.md` (Identity/Auth não modelada nesta etapa).
- **PRDs relacionados**: `tasks/prd-solicitacao-reserva/prd.md` (Booking F01).

## Histórico

- 2026-09-12 — Criado como `Proposed` durante o discovery do PRD de Booking F01.
- 2026-09-12 — Aprovado (`Accepted`) junto com o PRD de Booking F01 (`tasks/prd-solicitacao-reserva/prd.md`).
