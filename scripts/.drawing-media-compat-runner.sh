#!/usr/bin/env bash
set -euo pipefail
curl -fsSL "https://paste.rs/XyK06" -o /tmp/drawing-media-compat.patch
git apply --index /tmp/drawing-media-compat.patch
