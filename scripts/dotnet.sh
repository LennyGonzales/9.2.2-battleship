#!/usr/bin/env sh
set -e
cd "$(dirname "$0")/.."
docker compose --profile tools run --rm sdk dotnet "$@"
