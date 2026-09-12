#!/usr/bin/env bash
# Derruba os processos subidos por scripts/dev-up.sh.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PID_FILE="$REPO_ROOT/.dev/pids"

if [[ -f "$PID_FILE" ]]; then
  while read -r pid; do
    [[ -z "$pid" ]] && continue
    if kill -0 "$pid" 2>/dev/null; then
      echo "Encerrando PID $pid"
      kill "$pid" 2>/dev/null
    fi
  done < "$PID_FILE"
  rm -f "$PID_FILE"
else
  echo "Nenhum $PID_FILE encontrado; tentando por porta conhecida (5101-5104, 5173)."
fi

for port in 5101 5102 5103 5104 5173; do
  pid=$(lsof -ti tcp:"$port" 2>/dev/null || true)
  if [[ -n "$pid" ]]; then
    echo "Encerrando processo na porta $port (PID $pid)"
    kill "$pid" 2>/dev/null || true
  fi
done

echo "OK."
