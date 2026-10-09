#!/usr/bin/env bash
# Builds the FlightCrewStudio image and pushes it to Docker Hub.
#   ./build-push.sh [repository] [platform]
#   e.g. ./build-push.sh richp999999/flightcrewstudio linux/amd64
# Run `docker login` once first. Then on the server: docker compose pull && docker compose up -d
set -euo pipefail
cd "$(dirname "$0")"
REPO="${1:-richp999999/flightcrewstudio}"
PLATFORM="${2:-linux/amd64}"
VERSION="$(date +%Y%m%d-%H%M)"
echo "Building $REPO ($VERSION, $PLATFORM)..."
docker buildx build --platform "$PLATFORM" -t "$REPO:latest" -t "$REPO:$VERSION" --push .
echo "Pushed $REPO:latest and $REPO:$VERSION"
