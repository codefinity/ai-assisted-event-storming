#!/usr/bin/env sh
# Creates the demo account, team and sample board. Safe to run again.
#   ./scripts/seed.sh           uses the Docker stack if it is up, otherwise runs the API locally
#   SEED_DEMO_PASSWORD=... ./scripts/seed.sh
set -eu
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

if docker compose ps --status running --services 2>/dev/null | grep -qx api; then
  docker compose run --rm -e SEED_DEMO_PASSWORD="${SEED_DEMO_PASSWORD:-}" api seed
else
  echo "The Docker stack is not running; seeding with the local API against the configured MongoDB."
  dotnet run --project server/src/driving-adapters/EventStorming.Host -- seed
fi
