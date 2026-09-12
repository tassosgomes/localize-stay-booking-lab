#!/usr/bin/env bash
# Sobe Catalog, Booking, Payment e o frontend localmente, todos apontando
# para o postgres-main/RabbitMQ reais do infra (ver README.md). Não roda
# migrations (isso é passo de deploy, nunca de boot) nem configura
# user-secrets — rode scripts/dev-secrets-check.sh antes, na primeira vez.
#
# Uso:
#   scripts/dev-up.sh              # Catalog + Booking + Payment + Frontend
#   scripts/dev-up.sh --with-worker  # inclui também o Notification Worker
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEV_DIR="$REPO_ROOT/.dev"
LOG_DIR="$DEV_DIR/logs"
PID_FILE="$DEV_DIR/pids"

WITH_WORKER=false
if [[ "${1:-}" == "--with-worker" ]]; then
  WITH_WORKER=true
fi

mkdir -p "$LOG_DIR"
: > "$PID_FILE"

for cmd in dotnet node npm curl; do
  command -v "$cmd" >/dev/null 2>&1 || { echo "Erro: '$cmd' não encontrado no PATH." >&2; exit 1; }
done

port_in_use() {
  curl -s -o /dev/null --max-time 1 "http://localhost:$1" 2>/dev/null
}

require_secret() {
  local project="$1" key="$2" label="$3"
  if ! dotnet user-secrets list --project "$project" 2>/dev/null | grep -q "^${key} ="; then
    echo "Erro: falta '${key}' em user-secrets de ${label}." >&2
    echo "  dotnet user-secrets set \"${key}\" \"<valor>\" --project ${project}" >&2
    exit 1
  fi
}

start_dotnet() {
  local name="$1" project="$2" port="$3"
  if port_in_use "$port"; then
    echo "== ${name}: já responde em :${port}, pulando =="
    return
  fi
  echo "== Subindo ${name} (:${port}) =="
  (cd "$REPO_ROOT" && ASPNETCORE_ENVIRONMENT=Development nohup dotnet run --project "$project" \
    > "$LOG_DIR/${name}.log" 2>&1 &
    echo $! >> "$PID_FILE")
}

wait_healthy() {
  local name="$1" port="$2" timeout="${3:-60}"
  local waited=0
  echo -n "Aguardando ${name} ficar Healthy"
  while ! curl -s -o /dev/null -w '%{http_code}' "http://localhost:${port}/health/ready" 2>/dev/null | grep -q '^200$'; do
    sleep 2
    waited=$((waited + 2))
    echo -n "."
    if [[ "$waited" -ge "$timeout" ]]; then
      echo " TIMEOUT"
      echo "Ver log: $LOG_DIR/${name}.log" >&2
      return 1
    fi
  done
  echo " OK"
}

echo "Checando user-secrets obrigatórios..."
require_secret "services/catalog/src/1-Services/LocalizeStay.Catalog.Api" "ConnectionStrings:Catalog" "Catalog"
require_secret "services/booking/src/1-Services/LocalizeStay.Booking.Api" "ConnectionStrings:Booking" "Booking"
require_secret "services/booking/src/1-Services/LocalizeStay.Booking.Api" "RabbitMQ:UserName" "Booking (RabbitMQ)"
require_secret "services/booking/src/1-Services/LocalizeStay.Booking.Api" "RabbitMQ:Password" "Booking (RabbitMQ)"
require_secret "services/payment/src/1-Services/LocalizeStay.Payment.Api" "ConnectionStrings:Payment" "Payment"
if $WITH_WORKER; then
  require_secret "services/notification-worker/src/1-Services/LocalizeStay.Notification.Worker" "RabbitMQ:UserName" "Notification Worker (RabbitMQ)"
  require_secret "services/notification-worker/src/1-Services/LocalizeStay.Notification.Worker" "RabbitMQ:Password" "Notification Worker (RabbitMQ)"
fi

start_dotnet "catalog" "services/catalog/src/1-Services/LocalizeStay.Catalog.Api" 5101
# O browser do E2E Playwright roda em :5175 (127.0.0.1), não no dev :5173.
# O default de produto permite só http://localhost:5173 (CorsExtensions +
# appsettings); aqui, só env do harness dev (nada de default de produto),
# liberamos também as origens do harness E2E no Booking (aditivo).
export Cors__AllowedOrigins__0="http://localhost:5175"
export Cors__AllowedOrigins__1="http://127.0.0.1:5175"
start_dotnet "booking" "services/booking/src/1-Services/LocalizeStay.Booking.Api" 5102
unset Cors__AllowedOrigins__0 Cors__AllowedOrigins__1
start_dotnet "payment" "services/payment/src/1-Services/LocalizeStay.Payment.Api" 5103
if $WITH_WORKER; then
  start_dotnet "notification-worker" "services/notification-worker/src/1-Services/LocalizeStay.Notification.Worker" 5104
fi

wait_healthy "catalog" 5101
wait_healthy "booking" 5102
wait_healthy "payment" 5103
if $WITH_WORKER; then
  wait_healthy "notification-worker" 5104
fi

FRONTEND_DIR="$REPO_ROOT/frontend/localize-stay-frontend"
if port_in_use 5173; then
  echo "== frontend: já responde em :5173, pulando =="
else
  if [[ ! -d "$FRONTEND_DIR/node_modules" ]]; then
    echo "== frontend: instalando dependências (npm install) =="
    (cd "$FRONTEND_DIR" && npm install)
  fi
  echo "== Subindo frontend (:5173) =="
  (cd "$FRONTEND_DIR" && nohup npm run dev > "$LOG_DIR/frontend.log" 2>&1 &
    echo $! >> "$PID_FILE")
  sleep 2
fi

echo ""
echo "Tudo no ar:"
echo "  Catalog  http://localhost:5101/swagger"
echo "  Booking  http://localhost:5102/swagger"
echo "  Payment  http://localhost:5103/swagger"
if $WITH_WORKER; then
  echo "  Worker   http://localhost:5104/health/ready"
fi
echo "  Frontend http://localhost:5173"
echo ""
echo "Logs em: $LOG_DIR"
echo "Para derrubar tudo: scripts/dev-down.sh"
