#!/usr/bin/env bash
#
# One-shot local validation for CapitalismSim: restore, build, test, and a
# headless smoke test. Portable to any Linux environment with the .NET 10
# SDK installed. See AGENTS.md and README.md for individual commands.

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

echo "==> dotnet --info"
dotnet --info

echo "==> dotnet restore"
dotnet restore

echo "==> dotnet build -c Release --no-restore"
dotnet build -c Release --no-restore

echo "==> dotnet test -c Release --no-build"
dotnet test -c Release --no-build

echo "==> Headless smoke test"
dotnet run --project src/Simulation.Headless -c Release --no-build -- \
  --seed 123456 --duration-days 30

echo "==> Determinism smoke check"
dotnet run --project src/Simulation.Headless -c Release --no-build -- \
  --seed 123456 --duration-days 30 --verify-determinism

echo "==> All checks passed."
