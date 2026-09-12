#!/usr/bin/env bash
#
# Exporta o OpenAPI real de cada serviço (task 8.0, V-05).
#
# Sobe cada API localmente com configuração descartável de exportação
# (connection string e credenciais RabbitMQ fictícias — o documento Swagger
# é gerado a partir do código, sem tocar em Postgres/RabbitMQ reais) e
# captura /swagger/v1/swagger.json em contracts/openapi/.
#
# Uso (a partir da raiz do repo):
#   scripts/contracts/export-openapi.sh
#
# Requer: dotnet SDK 10, curl. Não requer broker ou banco acessíveis.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$REPO_ROOT"

OUT_DIR="contracts/openapi"
mkdir -p "$OUT_DIR"

DUMMY_CS="Host=localhost;Database=export_dummy;Username=export_dummy;Password=export_dummy"
export ConnectionStrings__Catalog="$DUMMY_CS"
export ConnectionStrings__Booking="$DUMMY_CS"
export ConnectionStrings__Payment="$DUMMY_CS"
export RabbitMQ__HostName="localhost"
export RabbitMQ__UserName="guest"
export RabbitMQ__Password="export_dummy"

declare -A SERVICES=(
  [catalog]="services/catalog/src/1-Services/LocalizeStay.Catalog.Api/LocalizeStay.Catalog.Api.csproj http://localhost:5101"
  [booking]="services/booking/src/1-Services/LocalizeStay.Booking.Api/LocalizeStay.Booking.Api.csproj http://localhost:5102"
  [payment]="services/payment/src/1-Services/LocalizeStay.Payment.Api/LocalizeStay.Payment.Api.csproj http://localhost:5103"
)

LOG_DIR="$(mktemp -d)"
PIDS=()
cleanup() {
  for pid in "${PIDS[@]:-}"; do kill "$pid" 2>/dev/null || true; done
  wait 2>/dev/null || true
  rm -rf "$LOG_DIR"
}
trap cleanup EXIT

for name in catalog booking payment; do
  # shellcheck disable=SC2206
  parts=(${SERVICES[$name]})
  csproj="${parts[0]}"
  base_url="${parts[1]}"

  dotnet run --project "$csproj" --no-build >"$LOG_DIR/$name.log" 2>&1 &
  PIDS+=($!)
done

for name in catalog booking payment; do
  # shellcheck disable=SC2206
  parts=(${SERVICES[$name]})
  base_url="${parts[1]}"

  for _ in $(seq 1 30); do
    if curl -sf -o /dev/null "$base_url/swagger/v1/swagger.json"; then break; fi
    sleep 2
  done

  curl -sf "$base_url/swagger/v1/swagger.json" -o "$OUT_DIR/$name.json"
  echo "exportado: $OUT_DIR/$name.json (de $base_url/swagger/v1/swagger.json)"
done
