#!/usr/bin/env bash

set -euo pipefail

cd "$(dirname "$0")"
export BootstrapDatabase="${BootstrapDatabase:-false}"
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://localhost:5080}"

backend_pid=
frontend_pid=

cleanup() {
  exit_code=$?
  trap - EXIT INT TERM

  for pid in "$backend_pid" "$frontend_pid"; do
    if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
    fi
  done

  wait "$backend_pid" "$frontend_pid" 2>/dev/null || true
  exit "$exit_code"
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

dotnet run --project backend/src/ORP.Api --no-launch-profile &
backend_pid=$!

npm --prefix frontend run dev &
frontend_pid=$!

wait -n "$backend_pid" "$frontend_pid"
