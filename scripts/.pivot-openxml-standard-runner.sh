#!/usr/bin/env bash
set -euo pipefail
curl -fsSL "https://paste.rs/eep8D" -o /tmp/pivot-openxml-standard.patch
git apply --index /tmp/pivot-openxml-standard.patch
