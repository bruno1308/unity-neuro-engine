#!/bin/bash
#
# Install Neuro-Engine git hooks
#
# Usage: ./githooks/install.sh
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

echo "Installing Neuro-Engine git hooks..."
echo ""

# Set the hooks path to use our custom hooks directory
cd "$REPO_ROOT"
git config core.hooksPath githooks

echo "Git hooks path set to: githooks/"
echo ""

# Make hooks executable (for Unix-like systems)
if [[ "$OSTYPE" != "msys" ]] && [[ "$OSTYPE" != "win32" ]]; then
    chmod +x "$SCRIPT_DIR/pre-commit"
    chmod +x "$SCRIPT_DIR/pre-push"
    chmod +x "$SCRIPT_DIR/commit-msg"
    echo "Made hooks executable"
fi

echo ""
echo "Installed hooks:"
echo "  - pre-commit  : Blocks commits with anti-patterns in engine code"
echo "  - pre-push    : Checks for layer review before pushing"
echo "  - commit-msg  : Suggests layer tags for commit messages"
echo ""
echo "Protected paths:"
echo "  - Packages/com.neuroengine.core/Runtime/Core/"
echo "  - Packages/com.neuroengine.core/Runtime/Services/"
echo "  - Packages/com.neuroengine.core/Editor/"
echo "  - Packages/com.neuroengine.core/Tests/"
echo ""
echo "To bypass hooks (not recommended):"
echo "  git commit --no-verify"
echo "  git push --no-verify"
echo ""
echo "Done!"
