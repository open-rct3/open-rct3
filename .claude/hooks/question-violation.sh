#!/bin/bash
# Detect plain text question tool violations on stop/completion

# Delegate to Node script for consistent parsing and cross-platform support
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec node "$SCRIPT_DIR/question-violation.js"


