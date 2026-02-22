#!/usr/bin/env bash
set -euo pipefail

# Publish versioned documentation and refresh docs/latest from the highest version.
#
# Usage:
#   ./scripts/publish_docs.sh <version>
#
# Examples:
#   ./scripts/publish_docs.sh 1.0.0

usage() {
  cat <<'USAGE'
Usage:
  ./scripts/publish_docs.sh <version>

Creates/updates:
  docs/<version>/...
  docs/latest/...
  (latest is refreshed from the highest version under docs/)

Manual source is expected at:
  manual/<version>/
USAGE
}

VERSION="${1:-}"

if [[ -z "${VERSION}" ]]; then
  usage
  exit 1
fi

echo "publish_docs.sh initialized"
echo "- version: ${VERSION}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DOCS_DIR="${REPO_ROOT}/docs"
MANUAL_VERSION_DIR="${REPO_ROOT}/manual/${VERSION}"
TARGET_VERSION_DIR="${DOCS_DIR}/${VERSION}"
LATEST_DIR="${DOCS_DIR}/latest"

copy_version_docs() {
  if [[ ! -d "${MANUAL_VERSION_DIR}" ]]; then
    echo "Error: manual source not found: ${MANUAL_VERSION_DIR}"
    echo "Hint: place manual docs under manual/${VERSION}/"
    exit 1
  fi

  mkdir -p "${DOCS_DIR}"
  rm -rf "${TARGET_VERSION_DIR}"
  mkdir -p "${TARGET_VERSION_DIR}"

  echo "[publish] copy manual/${VERSION} -> docs/${VERSION}"
  cp -R "${MANUAL_VERSION_DIR}/." "${TARGET_VERSION_DIR}/"
}

find_highest_version_dir() {
  if [[ ! -d "${DOCS_DIR}" ]]; then
    return 1
  fi

  local highest
  highest="$({
    find "${DOCS_DIR}" -mindepth 1 -maxdepth 1 -type d -print \
      | xargs -n 1 basename \
      | grep -E '^[0-9]+\.[0-9]+\.[0-9]+$' \
      | sort -V
  } 2>/dev/null | tail -n 1)"

  if [[ -z "${highest}" ]]; then
    return 1
  fi

  echo "${highest}"
}

refresh_latest_from_highest() {
  local highest_version
  if ! highest_version="$(find_highest_version_dir)"; then
    echo "Error: no version directories found under docs/ (expected x.y.z format)"
    exit 1
  fi

  local highest_dir="${DOCS_DIR}/${highest_version}"
  rm -rf "${LATEST_DIR}"
  mkdir -p "${LATEST_DIR}"

  echo "[publish] refresh docs/latest from docs/${highest_version}"
  cp -R "${highest_dir}/." "${LATEST_DIR}/"

  printf '%s\n' "${highest_version}" > "${LATEST_DIR}/VERSION.txt"
}

copy_version_docs
refresh_latest_from_highest

echo
echo "Done"
echo "- published: docs/${VERSION}"
echo "- refreshed: docs/latest"
