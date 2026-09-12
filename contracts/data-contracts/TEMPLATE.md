# Data Contract — TEMPLATE

> Template reutilizável para os datasets futuros do Localize Stay
> (task 8.0, V-05). Nenhum dataset real existe ainda: para publicar o
> primeiro contrato, copie este arquivo para
> `contracts/data-contracts/<nome_do_dataset>_v1.md` e preencha todas as
> seções marcadas com `<!-- PREENCHER -->`.
>
> Convenção de nome: `<domínio>_<propósito>_v1`
> (ex.: futuros datasets de disponibilidade e calendário de reservas).
> Versionamento: toda mudança incompatível gera um arquivo novo (`_v2`);
> mudanças compatíveis atualizam o mesmo arquivo com entrada no
> "Histórico de versões".

## 1. Identificação

| Campo | Valor |
|-------|-------|
| Nome do dataset | `<!-- PREENCHER: <domínio>_<propósito>_v1 -->` |
| Versão | `v1` |
| Domínio dono | `<!-- PREENCHER: catalog \| booking \| payment \| notification -->` |
| Time responsável | `<!-- PREENCHER -->` |
| Schema PostgreSQL de origem | `<!-- PREENCHER: catalog \| booking \| payment \| integration -->` |
| Estado | `rascunho` |

## 2. Descrição

<!-- PREENCHER: o que o dataset representa, em 1–3 frases, sem jargão de implementação. -->

## 3. Schema

<!-- PREENCHER: uma linha por campo. Tipos: text, integer, numeric(p,s), boolean, date, timestamptz, uuid, jsonb. -->

| Campo | Tipo | Nulo? | Descrição |
|-------|------|-------|-----------|
| `<!-- id -->` | `<!-- uuid -->` | `NÃO` | `<!-- PREENCHER -->` |

Regras:

- A chave primária é `<!-- PREENCHER: campo -->` e é estável entre versões.
- Campos de auditoria obrigatórios: `created_at timestamptz NOT NULL`, `updated_at timestamptz NOT NULL`.
- Nenhum dado pessoal (PII) além de `<!-- PREENCHER: nenhum \| listar e justificar -->`.

## 4. Qualidade (expectativas verificáveis)

| Regra | Severidade | Como verificar |
|-------|-----------|----------------|
| `<!-- PREENCHER: ex. "id único e não nulo" -->` | `<!-- bloqueante \| alerta -->` | `<!-- PREENCHER: query ou teste -->` |

## 5. Acesso

| Campo | Valor |
|-------|-------|
| Leitura concedida a (roles) | `<!-- PREENCHER: ex. booking_role (SELECT no schema integration) -->` |
| Escrita concedida a (role) | `<!-- PREENCHER: apenas a role dona do schema de origem -->` |
| Formato de exposição | `<!-- PREENCHER: tabela no schema integration \| evento \| arquivo -->` |
| Frequência de atualização | `<!-- PREENCHER: ex. transacional \| diária HH:MM TZ -->` |

## 6. Retenção e privacidade

- Retenção: `<!-- PREENCHER: ex. 24 meses, depois anonimiza -->`.
- Dados sensíveis: `<!-- PREENCHER: nenhum \| descrever tratamento -->`.

## 7. Histórico de versões

| Versão | Data | Mudança | Compatível? |
|--------|------|---------|-------------|
| `v1` | `<!-- AAAA-MM-DD -->` | Criação do contrato. | — |
