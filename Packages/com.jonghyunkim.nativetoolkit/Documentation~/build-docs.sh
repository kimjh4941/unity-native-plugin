#!/usr/bin/env bash
set -euo pipefail

# Ensure docfx is on PATH (user must have installed docfx via dotnet tool)
if ! command -v docfx >/dev/null 2>&1; then
  echo "docfx not found. Ensure ~/.dotnet/tools is on PATH and docfx is installed."
  exit 1
fi

# clean previous builds
echo "Cleaning previous builds..."
rm -rf api
rm -rf site

# run metadata and build (cwd = Documentation~)
echo "Generating metadata..."
docfx metadata

echo "Building site..."
docfx build

echo "Docs built at $(pwd)/site"