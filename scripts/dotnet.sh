#!/usr/bin/env sh
set -e
cd "$(dirname "$0")/.."
docker compose run --rm sdk dotnet "$@"
