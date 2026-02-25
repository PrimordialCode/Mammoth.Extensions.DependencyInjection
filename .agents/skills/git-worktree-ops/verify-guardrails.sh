#!/usr/bin/env bash
# Verify guardrails are properly configured in current worktree
# Usage: verify-guardrails.sh [worktree-path]

set -euo pipefail

WORKTREE="${1:-.}"

cd "$WORKTREE"

echo "Verifying guardrails for: $(pwd)"
echo ""

ERRORS=0
WARNINGS=0

# Check if in a git repository
if ! git rev-parse --git-dir >/dev/null 2>&1; then
  echo "✗ Not a Git repository"
  exit 1
fi

GIT_DIR="$(git rev-parse --git-dir)"
CURRENT_BRANCH="$(git branch --show-current)"
CURRENT_PATH="$(git rev-parse --show-toplevel)"

# Check hooks
echo "Hooks:"
for hook in pre-commit pre-push; do
  HOOK_PATH="$(git rev-parse --git-path hooks/$hook)"
  if [[ -x "$HOOK_PATH" ]]; then
    echo "  ✓ $hook is executable"
  else
    echo "  ✗ $hook is missing or not executable"
    ((ERRORS++))
  fi
done
echo ""

# Check metadata files
echo "Metadata files:"
if [[ -f "$GIT_DIR/agent-expected-branch" ]]; then
  EXPECTED_BRANCH="$(cat "$GIT_DIR/agent-expected-branch")"
  echo "  ✓ agent-expected-branch exists: $EXPECTED_BRANCH"
  
  if [[ "$CURRENT_BRANCH" != "$EXPECTED_BRANCH" ]]; then
    echo "    ⚠️  WARNING: Current branch ($CURRENT_BRANCH) != expected ($EXPECTED_BRANCH)"
    ((WARNINGS++))
  fi
else
  echo "  ✗ agent-expected-branch is missing"
  ((ERRORS++))
fi

if [[ -f "$GIT_DIR/agent-expected-worktree" ]]; then
  EXPECTED_PATH="$(cat "$GIT_DIR/agent-expected-worktree")"
  echo "  ✓ agent-expected-worktree exists: $EXPECTED_PATH"
  
  if [[ "$CURRENT_PATH" != "$EXPECTED_PATH" ]]; then
    echo "    ⚠️  WARNING: Current path ($CURRENT_PATH) != expected ($EXPECTED_PATH)"
    ((WARNINGS++))
  fi
else
  echo "  ✗ agent-expected-worktree is missing"
  ((ERRORS++))
fi
echo ""

# Check git config
echo "Git configuration:"
PUSH_DEFAULT="$(git config --local --get push.default 2>/dev/null || echo "not set")"
PULL_FF="$(git config --local --get pull.ff 2>/dev/null || echo "not set")"

if [[ "$PUSH_DEFAULT" == "current" ]]; then
  echo "  ✓ push.default = current"
else
  echo "  ⚠️  push.default = $PUSH_DEFAULT (recommended: current)"
  ((WARNINGS++))
fi

if [[ "$PULL_FF" == "only" ]]; then
  echo "  ✓ pull.ff = only"
else
  echo "  ⚠️  pull.ff = $PULL_FF (recommended: only)"
  ((WARNINGS++))
fi
echo ""

# Summary
echo "Summary:"
echo "  Current branch:   $CURRENT_BRANCH"
echo "  Current worktree: $CURRENT_PATH"

if [[ $ERRORS -eq 0 && $WARNINGS -eq 0 ]]; then
  echo ""
  echo "✓ All guardrails properly configured!"
  exit 0
elif [[ $ERRORS -eq 0 ]]; then
  echo ""
  echo "⚠️  $WARNINGS warning(s) found"
  exit 0
else
  echo ""
  echo "✗ $ERRORS error(s) and $WARNINGS warning(s) found"
  echo ""
  echo "To fix, run:"
  echo "  GIT_DIR=\"\$(git rev-parse --git-dir)\""
  echo "  printf '%s\\n' \"$CURRENT_BRANCH\" > \"\$GIT_DIR/agent-expected-branch\""
  echo "  printf '%s\\n' \"$CURRENT_PATH\" > \"\$GIT_DIR/agent-expected-worktree\""
  echo "  git config --local push.default current"
  echo "  git config --local pull.ff only"
  exit 1
fi
