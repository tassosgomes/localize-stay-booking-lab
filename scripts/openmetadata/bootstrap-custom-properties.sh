#!/usr/bin/env bash
#
# Cria as 4 Custom Properties usadas pelo publicador de Data Contracts
# (scripts/openmetadata/register-data-contracts) na entidade `table` do
# OpenMetadata. EXECUÇÃO MANUAL ÚNICA pelo dono do homelab, feita uma vez
# antes da primeira execução real do job `catalog-metadata` — o ci.yml NUNCA
# chama este script (mudar o schema de tipos a cada push não tem valor e
# adiciona risco).
#
# Alternativa equivalente: Settings > Custom Properties > Table na UI do
# OpenMetadata, criando os mesmos 4 campos manualmente.
#
# Uso:
#   export OPENMETADATA_BASE_URL="https://openmetadata.lab.tasso.dev.br/api"
#   export OPENMETADATA_INGESTION_JWT="<jwt do bot de ingestão>"
#   scripts/openmetadata/bootstrap-custom-properties.sh
#
# Idempotente na intenção (mesmo nome = mesma propriedade), mas o payload
# exato de `propertyType` varia entre versões do OpenMetadata — confirme na
# instância real (build 2.0.1) e ajuste este script se a API rejeitar.

set -euo pipefail

: "${OPENMETADATA_BASE_URL:?defina OPENMETADATA_BASE_URL (ex.: https://openmetadata.lab.tasso.dev.br/api)}"
: "${OPENMETADATA_INGESTION_JWT:?defina OPENMETADATA_INGESTION_JWT (JWT do bot de ingestão, escopo mínimo de escrita)}"

create_property() {
  local name="$1" description="$2"
  echo "Criando custom property '$name' na entidade table..."
  curl -sf -X POST "${OPENMETADATA_BASE_URL}/v1/metadata/types/table/customProperties" \
    -H "Authorization: Bearer ${OPENMETADATA_INGESTION_JWT}" \
    -H "Content-Type: application/json" \
    -d "{\"name\": \"${name}\", \"description\": \"${description}\", \"propertyType\": {\"type\": \"string\"}}" \
    && echo "  ok" \
    || echo "  falhou (pode já existir — confirme na UI: Settings > Custom Properties > Table)"
}

create_property "dataContractRef" "Caminho do arquivo Data Contract (contracts/data-contracts/*.md) que descreve este dataset."
create_property "dataContractVersion" "Versão do Data Contract (campo Versão da tabela de Identificação)."
create_property "dataContractStatus" "Estado do Data Contract (campo Estado da tabela de Identificação: rascunho, estável, etc.)."
create_property "dataContractOwnerDomain" "Domínio dono do dataset (campo Domínio dono da tabela de Identificação)."

echo "Confirme na UI (Settings > Custom Properties > Table) que as 4 propriedades existem antes de rodar register-data-contracts contra o ambiente real."
