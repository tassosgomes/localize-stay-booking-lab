# PD-001: Moeda única do laboratório

- **Status**: Accepted
- **Escopo**: Global
- **Data**: 2026-09-12
- **Responsável pela decisão**: Tasso Gomes (autor do laboratório)
- **Origem**: Discovery do PRD de Booking F01 — Solicitação de Reserva; pendência registrada em `domains/booking/domain.md` §9
- **Tags**: moeda, pricing, booking, catalog
- **Substitui**: Não aplicável
- **Substituído por**: Não aplicável

## Contexto

`vision.md` já trata "múltiplas moedas" como decisão adiada/non-goal, e `domains/booking/domain.md`
(RN-05) exige que Booking congele "preço por noite e moeda devolvidos pelo Catalog" no momento da
solicitação de reserva, mas nenhum documento anterior define qual é essa moeda. O Domain Doc de
Booking registra explicitamente como questão em aberto a ser resolvida no PRD de F01. Sem essa
decisão, o comportamento de congelamento de preço (RN-05, RN-06) não pode ser especificado de forma
observável.

## Decisão

O laboratório usa uma única moeda fixa em todo o sistema: **Real brasileiro (BRL)**. Todo valor
monetário exibido ou registrado (preço por noite, valor total da Reservation, valor solicitado a
Payment) é sempre em BRL. Não existe seleção, conversão ou exibição de múltiplas moedas em nenhuma
fase congelada do escopo funcional.

## Alternativas consideradas

- **Moeda por Accommodation, retornada dinamicamente pelo Catalog** — permitiria simular um cenário
  multi-moeda, mas contradiz o non-goal explícito da Vision ("múltiplas moedas") e adicionaria
  complexidade de produto sem valor de aprendizado arquitetural correspondente.
- **Dólar americano (USD)** — equivalente em efeito prático a BRL (moeda única fixa); rejeitado
  apenas por preferência de manter os artefatos do laboratório (e valores fictícios) na moeda local
  do autor.

## Consequências

- **Positivas**: remove ambiguidade do RN-05/RN-06 de Booking; simplifica o contrato síncrono entre
  Booking e Catalog (moeda não precisa ser negociada por chamada, apenas assumida).
- **Negativas ou riscos**: se o laboratório algum dia decidir explorar cenários multi-moeda (fora do
  roadmap atual), esta decisão precisará ser revisitada e o PD sucedido.
- **Impacto em futuros PRDs**: qualquer PRD de Catalog, Booking ou Payment que registre ou exiba
  valor monetário deve assumir BRL como moeda fixa, sem campo de moeda variável no comportamento de
  produto.

## Termos e documentos afetados

- **Termos canônicos**: "moeda" nos glossários de Booking/Catalog passa a significar sempre BRL.
- **Vision/Domain Docs**: resolve a pendência aberta em `domains/booking/domain.md` §9 ("Definir no
  PRD de F01 qual moeda única será adotada").
- **PRDs relacionados**: `tasks/prd-solicitacao-reserva/prd.md` (Booking F01).

## Histórico

- 2026-09-12 — Criado como `Proposed` durante o discovery do PRD de Booking F01.
- 2026-09-12 — Aprovado (`Accepted`) junto com o PRD de Booking F01 (`tasks/prd-solicitacao-reserva/prd.md`).
