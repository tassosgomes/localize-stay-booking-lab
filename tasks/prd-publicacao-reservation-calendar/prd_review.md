# Full validation — PRD Publicação Reservation Calendar (revalidação 3)

**Resultado: FULL VALIDATION APROVADA**

- `base_ref`: `9f103c05380abc138765c259f6625092818afd37`
- `validated_commit`: `58be7a022f492b03c8537b93fb12013c41702614`
- `validated_tree`: `6701d874513ffa39388d918b35edc0b3862e0d1b`
- Branch: `feature/prd-publicacao-reservation-calendar`

## Gate obrigatório

Executado: `scripts/ai-flow/gate.sh --base=9f103c05380abc138765c259f6625092818afd37 --all-tests`.

Resultado: `GATE: APROVADO`. A suíte agregada passou após os reparos: o projeto de integração
Catalog passa a copiar o contrato em `tasks/archived/prd-cadastro-property/api-contract.yaml`, e os
testes de query de Booking usam `Confirm`/`Cancel` do aggregate, persistindo `terminal_transition_at`.

## Revisão full

- V-01 preserva o contrato F05: view PostgreSQL regular com exatamente sete colunas, allow-list de
  estados terminais, projeção explícita e `updated_at` persistido da transição terminal; a suíte
  Testcontainers cobre schema, elegibilidade, período, unicidade, estabilidade e idempotência.
- V-02 concede apenas `SELECT` às três roles, prova negação de escrita/`CREATE`/leitura direta de
  `booking.reservations`, e mantém o script SQL complementar coerente com os testes.
- Não há endpoint, evento, dependência ou abstração de runtime adicional. A solução SQL direta é
  proporcional; nenhum padrão adicional é justificado pelo escopo atual.

## Pendência manual não bloqueante

O procedimento de ingestão e confirmação de owner/lineage no OpenMetadata permanece documentado em
`scripts/openmetadata/README.md`, mas ainda depende do ambiente e das credenciais reais; não foi
alegado como executado pelo gate local.

Bloqueantes: 0. Recomendações: 0. A validação abrange o commit de reparos `58be7a0` e sua árvore
acima; não houve alteração semântica após a revisão.
