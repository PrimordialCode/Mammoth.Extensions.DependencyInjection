#!/usr/bin/env bash
# Clean up completed or abandoned worktrees
# Usage: cleanup-worktree.sh <worktree-path> [--delete-branch] [--force]

set -euo pipefail

usage() {
  cat <<EOF
Usage: cleanup-worktree.sh <worktree-path> [options]

Options:
  --delete-branch   Delete the branch after removing worktree
  --force           Force removal even if worktree has uncommitted changes
  --remote          Also delete remote branch (requires --delete-branch)

Examples:
  cleanup-worktree.sh ../myapp-worktrees/codex-fix-auth
  cleanup-worktree.sh ../myapp-worktrees/codex-fix-auth --delete-branch
  cleanup-worktree.sh ../myapp-worktrees/codex-fix-auth --delete-branch --remote --force
EOF
}

if [[ $# -lt 1 ]]; then
  usage
  exit 1
fi

WORKTREE_PATH="$1"
shift

DELETE_BRANCH=0
FORCE=0
DELETE_REMOTE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --delete-branch)
      DELETE_BRANCH=1
      shift
      ;;
    --force)
      FORCE=1
      shift
      ;;
    --remote)
      DELETE_REMOTE=1
      shift
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ ! -d "$WORKTREE_PATH" ]]; then
  echo "Error: Worktree path not found: $WORKTREE_PATH" >&2
  exit 1
fi

# Get branch name from worktree
cd "$WORKTREE_PATH"
BRANCH="$(git branch --show-current)"
cd - >/dev/null

echo "Cleaning up worktree:"
echo "  Path:   $WORKTREE_PATH"
echo "  Branch: $BRANCH"
echo ""

# Remove worktree
echo "Removing worktree..."
if [[ $FORCE -eq 1 ]]; then
  git worktree remove --force "$WORKTREE_PATH"
else
  git worktree remove "$WORKTREE_PATH"
fi
echo "  ✓ Worktree removed"

# Delete branch if requested
if [[ $DELETE_BRANCH -eq 1 ]]; then
  echo "Deleting local branch..."
  if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
    git branch -D "$BRANCH"
    echo "  ✓ Local branch deleted: $BRANCH"
  else
    echo "  ⚠️  Branch not found: $BRANCH"
  fi
  
  # Delete remote branch if requested
  if [[ $DELETE_REMOTE -eq 1 ]]; then
    echo "Deleting remote branch..."
    if git ls-remote --exit-code --heads origin "$BRANCH" >/dev/null 2>&1; then
      git push origin --delete "$BRANCH"
      echo "  ✓ Remote branch deleted: origin/$BRANCH"
    else
      echo "  ⚠️  Remote branch not found: origin/$BRANCH"
    fi
  fi
fi

# Prune worktree metadata
echo "Pruning worktree metadata..."
git worktree prune
echo "  ✓ Metadata pruned"

echo ""
echo "✓ Cleanup complete!"
echo ""
echo "Remaining worktrees:"
git worktree list
