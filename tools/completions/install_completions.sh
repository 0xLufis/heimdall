#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

CURRENT_SHELL="$(basename "${SHELL:-bash}")"

echo "=========================================================="
echo "    Heimdall Shell Completion Installer"
echo "=========================================================="

if [ "$CURRENT_SHELL" = "zsh" ]; then
    RC_FILE="$HOME/.zshrc"
    COMPLETION_SOURCE="$REPO_ROOT/tools/completions/heimdall_completion.zsh"
    ENTRY="[ -f \"$COMPLETION_SOURCE\" ] && source \"$COMPLETION_SOURCE\""
else
    RC_FILE="$HOME/.bashrc"
    COMPLETION_SOURCE="$REPO_ROOT/tools/completions/heimdall_completion.bash"
    ENTRY="[ -f \"$COMPLETION_SOURCE\" ] && source \"$COMPLETION_SOURCE\""
fi

if [ -f "$RC_FILE" ]; then
    if grep -Fq "heimdall_completion" "$RC_FILE"; then
        echo "✓ Shell completion is already registered in $RC_FILE"
    else
        echo "" >> "$RC_FILE"
        echo "# Heimdall CLI script completions" >> "$RC_FILE"
        echo "$ENTRY" >> "$RC_FILE"
        echo "✓ Successfully added Heimdall completions to $RC_FILE"
    fi
else
    echo "Creating $RC_FILE and adding completion entry..."
    echo "$ENTRY" >> "$RC_FILE"
fi

echo ""
echo "To activate immediately in your current terminal session, run:"
echo "  source $COMPLETION_SOURCE"
echo "=========================================================="
