#!/usr/bin/env bash
set -euo pipefail
export SCRIPT_DIR="$(cd "$(dirname "$0")" >/dev/null 2>&1 && pwd)"

# echo "[post-create] Installing system dependencies"
# apt-get update && apt-get install -y curl tar wget postgresql-client && apt-get clean

echo "[post-create] Installing OpenShift CLI"
source "${SCRIPT_DIR}/installOcCli"

echo "[post-create] Installing testConnection"
source "${SCRIPT_DIR}/installTestConnection"

echo "[post-create] Restoring packages"
source "${SCRIPT_DIR}/restoreDependencies"
