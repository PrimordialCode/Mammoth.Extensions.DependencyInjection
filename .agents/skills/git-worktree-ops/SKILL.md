---
name: git-worktree-ops
description: Expert guidance for Git worktree operations, safe branching, and multi-agent workflow management. Use for creating/removing worktrees, managing branches, troubleshooting conflicts, and ensuring guardrails compliance. Also covers standard Git operations like committing, merging, rebasing, stashing, and conflict resolution.
---

# Git Worktree Operations

Expert skill for safe Git operations in multi-agent worktree environments and standard Git workflows.

## When to Use This Skill

**Worktree Operations**:
- Creating or removing Git worktrees
- Managing branches across multiple worktrees
- Troubleshooting worktree-related issues
- Verifying guardrails are properly configured
- Cleaning up stale worktrees
- Understanding git worktree metadata

**Standard Git Operations**:
- Creating, switching, and managing branches
- Committing and staging changes
- Merging and rebasing branches
- Resolving conflicts
- Working with remote repositories
- Viewing history and diffs
- Undoing changes safely

## Core Worktree Commands

### Create Worktree

**Basic pattern**:
```bash
git worktree add -b <new-branch> <path> <base-branch>
```

**Examples**:
```bash
# Create worktree for Codex task
git worktree add -b feat/codex-fix-auth ../myapp-worktrees/codex-fix-auth develop

# Create worktree from current branch
git worktree add -b feat/new-feature ../myapp-worktrees/new-feature HEAD

# Create worktree without new branch (checkout existing)
git worktree add ../myapp-worktrees/existing-work feat/existing-branch
```

**Naming conventions** (for this project):
- Branch: `feat/<agent>-<task>` (e.g., `feat/codex-fix-auth`)
- Worktree path: `<agent>-<task>` (matches branch without `feat/`)

### List Worktrees

```bash
# Show all worktrees
git worktree list

# Verbose output with branch and commit info
git worktree list --verbose

# Porcelain format for scripting
git worktree list --porcelain
```

### Remove Worktree

```bash
# Safe removal (must be clean)
git worktree remove <path>

# Force removal (even if dirty)
git worktree remove --force <path>

# Remove worktree and delete branch
git worktree remove <path>
git branch -D <branch-name>
```

### Prune Stale Metadata

```bash
# Clean up metadata for deleted worktree directories
git worktree prune

# Dry run to see what would be pruned
git worktree prune --dry-run

# Verbose output
git worktree prune --verbose
```

## Guardrails Configuration

### Set Up Branch/Worktree Binding

**Required before starting any agent** in a worktree:

```bash
# Navigate to worktree
cd ../myapp-worktrees/codex-fix-auth

# Verify current branch and location
git branch --show-current
git rev-parse --show-toplevel

# Set metadata for hooks
GIT_DIR="$(git rev-parse --git-dir)"
BRANCH="feat/codex-fix-auth"
WORKTREE="$(pwd -P)"

printf '%s\n' "$BRANCH" > "$GIT_DIR/agent-expected-branch"
printf '%s\n' "$WORKTREE" > "$GIT_DIR/agent-expected-worktree"

# Verify metadata was written
cat "$GIT_DIR/agent-expected-branch"
cat "$GIT_DIR/agent-expected-worktree"
```

### Verify Guardrails Active

```bash
# Check if pre-commit hook exists and is executable
test -x "$(git rev-parse --git-path hooks/pre-commit)" && echo "pre-commit: ✓" || echo "pre-commit: ✗"

# Check if pre-push hook exists and is executable
test -x "$(git rev-parse --git-path hooks/pre-push)" && echo "pre-push: ✓" || echo "pre-push: ✗"

# Check metadata files exist
test -f "$(git rev-parse --git-dir)/agent-expected-branch" && echo "branch binding: ✓" || echo "branch binding: ✗"
test -f "$(git rev-parse --git-dir)/agent-expected-worktree" && echo "worktree binding: ✓" || echo "worktree binding: ✗"

# View hook dispatcher (if installed)
cat "$(git rev-parse --git-path hooks/pre-commit)"
```

## Safe Branch Operations

### Allowed Operations

```bash
# Safe: Rebase current branch onto base
git fetch origin
git rebase origin/develop

# Safe: Merge base into current branch
git fetch origin
git merge origin/develop

# Safe: Push to assigned branch (fast-forward)
git push origin feat/codex-fix-auth

# Safe: Pull with fast-forward only
git pull --ff-only

# Safe: Create commits on assigned branch
git add .
git commit -m "feat: implement authentication retry logic"
```

### Forbidden Operations

```bash
# FORBIDDEN: Rebase another agent's branch
git rebase feat/claude-add-metrics  # ✗ Never do this

# FORBIDDEN: Force push
git push --force origin feat/codex-fix-auth  # ✗ Blocked by hooks
git push --force-with-lease origin feat/codex-fix-auth  # ✗ Only with explicit approval

# FORBIDDEN: Push to different branch
git push origin feat/codex-fix-auth:develop  # ✗ Blocked by hooks

# FORBIDDEN: Delete branch via push
git push origin :feat/codex-fix-auth  # ✗ Blocked by hooks
git push origin --delete feat/codex-fix-auth  # ✗ Blocked by hooks

# FORBIDDEN: Commit on wrong branch
# Hooks will block if current branch != agent-expected-branch
```

## Conflict Resolution

### When Two Agents Touch Same Files

**Strategy**: Merge one branch first, then rebase the second.

```bash
# Step 1: Human reviews and merges first agent branch
cd ~/src/myapp
git checkout develop
git pull --ff-only
git merge --no-ff feat/codex-fix-auth
git push origin develop

# Step 2: Update second agent branch
cd ../myapp-worktrees/claude-add-metrics
git fetch origin
git rebase origin/develop

# Step 3: Resolve conflicts manually
# (Edit conflicted files)
git add .
git rebase --continue

# Step 4: Re-run validation
npm test  # or appropriate test command

# Step 5: Human reviews and merges second branch
cd ~/src/myapp
git checkout develop
git pull --ff-only
git merge --no-ff feat/claude-add-metrics
git push origin develop
```

**Important**: Never ask agents to auto-resolve conflicts between agent branches without human oversight.

### Abort Rebase if Needed

```bash
# Cancel rebase and return to pre-rebase state
git rebase --abort

# Check what caused the issue
git status
git log --oneline --graph --all --decorate -10
```

## Standard Git Branch Operations

For teams not using worktrees or for single-developer scenarios, these standard Git operations provide safe workflows.

### Branch Management

**Create and switch to new branch**:
```bash
# Create branch from current HEAD
git checkout -b feat/new-feature

# Create branch from specific base
git checkout -b feat/new-feature develop

# Modern syntax (Git 2.23+)
git switch -c feat/new-feature
git switch -c feat/new-feature develop
```

**Switch between branches**:
```bash
# Switch to existing branch
git checkout develop
git switch develop  # Modern syntax

# Switch to previous branch
git checkout -
git switch -

# Switch and discard local changes (dangerous!)
git checkout -f develop
```

**List branches**:
```bash
# List local branches
git branch
git branch -v  # With last commit info
git branch -vv  # With upstream tracking info

# List all branches (local + remote)
git branch -a
git branch -avv

# List remote branches only
git branch -r

# List merged branches
git branch --merged
git branch --no-merged

# Search for branches
git branch --list '*fix*'
git branch -a --list '*feature*'
```

**Delete branches**:
```bash
# Delete merged branch (safe)
git branch -d feat/completed-feature

# Force delete unmerged branch
git branch -D feat/abandoned-feature

# Delete remote branch
git push origin --delete feat/old-feature
git push origin :feat/old-feature  # Older syntax

# Prune deleted remote branches from local
git fetch --prune
```

**Rename branch**:
```bash
# Rename current branch
git branch -m new-name

# Rename specific branch
git branch -m old-name new-name

# Update remote after rename
git push origin :old-name new-name
git push origin -u new-name
```

### Staging and Committing

**Stage changes**:
```bash
# Stage specific files
git add file1.js file2.js

# Stage all changes in directory
git add src/

# Stage all changes (tracked and untracked)
git add .
git add --all

# Stage only tracked files
git add -u

# Interactive staging
git add -p  # Patch mode - stage hunks interactively
git add -i  # Interactive mode

# Stage parts of a file
git add -p file.js
```

**Unstage changes**:
```bash
# Unstage file (keep changes)
git restore --staged file.js  # Modern
git reset HEAD file.js  # Classic

# Unstage all
git restore --staged .
git reset HEAD
```

**Commit changes**:
```bash
# Basic commit
git commit -m "feat: add user authentication"

# Commit with detailed message
git commit  # Opens editor

# Stage all tracked files and commit
git commit -am "fix: resolve login timeout"

# Amend last commit (add changes or fix message)
git commit --amend
git commit --amend -m "Updated commit message"

# Commit with specific author
git commit --author="Name <email>" -m "Message"

# Empty commit (for triggering CI)
git commit --allow-empty -m "Trigger CI"
```

**Conventional commit format**:
```bash
git commit -m "feat: add new feature"      # New feature
git commit -m "fix: resolve bug"           # Bug fix
git commit -m "docs: update README"        # Documentation
git commit -m "style: format code"         # Formatting
git commit -m "refactor: restructure"      # Code restructure
git commit -m "test: add tests"            # Add tests
git commit -m "chore: update deps"         # Maintenance
```

### Viewing Changes and History

**Status and diffs**:
```bash
# Show working directory status
git status
git status -s  # Short format
git status -sb  # Short with branch info

# Show unstaged changes
git diff

# Show staged changes
git diff --staged
git diff --cached

# Show changes in specific file
git diff file.js
git diff --staged file.js

# Compare branches
git diff develop..feat/new-feature
git diff develop...feat/new-feature  # Since common ancestor

# Word-level diff
git diff --word-diff

# Statistics only
git diff --stat
git diff --shortstat
```

**View history**:
```bash
# Show commit history
git log
git log --oneline
git log --oneline --graph --all --decorate

# Show last N commits
git log -5
git log --oneline -10

# Show commits by author
git log --author="John"

# Show commits in date range
git log --since="2 weeks ago"
git log --after="2024-01-01" --before="2024-02-01"

# Show commits affecting specific file
git log -- file.js
git log -p -- file.js  # With diffs

# Search commit messages
git log --grep="fix"
git log --grep="auth" --grep="login" --all-match

# Show commits with specific changes
git log -S "function name"  # Pickaxe search
git log -G "regex pattern"

# Pretty formats
git log --pretty=format:"%h - %an, %ar : %s"
git log --graph --pretty=format:"%C(yellow)%h%Creset %C(blue)%an%Creset %s"
```

**Show specific commit**:
```bash
# Show commit details
git show <commit-hash>
git show HEAD
git show HEAD~1  # Previous commit
git show HEAD~3  # 3 commits ago

# Show file at specific commit
git show <commit-hash>:path/to/file.js

# Show commit stats only
git show --stat <commit-hash>
```

### Merging

**Fast-forward merge**:
```bash
# Merge feature into current branch (fast-forward if possible)
git merge feat/new-feature

# Force fast-forward only (fail if not possible)
git merge --ff-only feat/new-feature
```

**No fast-forward merge** (creates merge commit):
```bash
# Always create merge commit
git merge --no-ff feat/new-feature

# With custom message
git merge --no-ff -m "Merge feature X" feat/new-feature
```

**Squash merge** (combine all commits):
```bash
# Squash all commits into one
git merge --squash feat/new-feature
git commit -m "Add feature X"
```

**Abort merge**:
```bash
# Cancel merge in progress
git merge --abort

# Reset to before merge
git reset --hard HEAD
```

### Rebasing

**Interactive rebase**:
```bash
# Rebase last 3 commits
git rebase -i HEAD~3

# Rebase onto another branch
git rebase develop
git rebase -i develop

# In the editor, you can:
# - pick: use commit as-is
# - reword: change commit message
# - edit: stop to amend commit
# - squash: combine with previous commit
# - fixup: like squash but discard message
# - drop: remove commit
```

**Continue/abort rebase**:
```bash
# After resolving conflicts
git add .
git rebase --continue

# Skip current commit
git rebase --skip

# Abort rebase
git rebase --abort
```

**Rebase vs Merge**:
```bash
# Rebase: linear history, no merge commits
git rebase develop

# Merge: preserves history, creates merge commit
git merge develop
```

### Conflict Resolution

**When conflicts occur**:
```bash
# 1. See conflicted files
git status

# 2. View conflict in file
cat file.js
# <<<<<<< HEAD
# Your changes
# =======
# Their changes
# >>>>>>> branch-name

# 3. Edit file to resolve
vim file.js

# 4. Mark as resolved
git add file.js

# 5. Complete merge/rebase
git commit  # For merge
git rebase --continue  # For rebase
```

**Conflict resolution tools**:
```bash
# Use mergetool
git mergetool

# Accept theirs for all conflicts
git checkout --theirs .
git add .

# Accept ours for all conflicts
git checkout --ours .
git add .

# Accept theirs for specific file
git checkout --theirs path/to/file.js
git add path/to/file.js
```

**View conflict diff**:
```bash
# Show both sides of conflict
git diff

# Show only conflicted files
git diff --name-only --diff-filter=U

# Show detailed conflict info
git log --merge -p
```

### Undoing Changes

**Discard uncommitted changes**:
```bash
# Discard changes in specific file
git restore file.js  # Modern
git checkout -- file.js  # Classic

# Discard all unstaged changes
git restore .
git checkout -- .

# Discard staged and unstaged changes
git restore --staged --worktree .
git reset --hard HEAD
```

**Undo commits**:
```bash
# Undo last commit, keep changes staged
git reset --soft HEAD~1

# Undo last commit, keep changes unstaged
git reset HEAD~1
git reset --mixed HEAD~1  # Default

# Undo last commit, discard changes
git reset --hard HEAD~1

# Undo multiple commits
git reset --hard HEAD~3

# Undo to specific commit
git reset --hard <commit-hash>
```

**Revert commits** (safe for shared branches):
```bash
# Create new commit that undoes changes
git revert <commit-hash>

# Revert last commit
git revert HEAD

# Revert multiple commits
git revert HEAD~3..HEAD

# Revert without committing
git revert -n <commit-hash>
git revert --no-commit <commit-hash>
```

**Recovery**:
```bash
# View reflog (history of HEAD)
git reflog

# Recover lost commit
git reflog
git reset --hard <commit-hash-from-reflog>

# Recover deleted branch
git reflog
git checkout -b recovered-branch <commit-hash>

# Find dangling commits
git fsck --lost-found
```

### Remote Operations

**Fetch updates**:
```bash
# Fetch all remotes
git fetch --all

# Fetch specific remote
git fetch origin

# Fetch and prune deleted branches
git fetch --all --prune

# Fetch specific branch
git fetch origin develop
```

**Pull changes**:
```bash
# Pull with default strategy
git pull

# Pull with fast-forward only (safe)
git pull --ff-only

# Pull with rebase
git pull --rebase

# Pull specific branch
git pull origin develop
```

**Push changes**:
```bash
# Push current branch to upstream
git push

# Push and set upstream
git push -u origin feat/new-feature
git push --set-upstream origin feat/new-feature

# Push specific branch
git push origin feat/new-feature

# Push all branches
git push --all origin

# Push tags
git push --tags

# Force push (dangerous!)
git push --force origin feat/new-feature

# Force push with lease (safer)
git push --force-with-lease origin feat/new-feature
```

**Remote management**:
```bash
# List remotes
git remote
git remote -v

# Add remote
git remote add origin https://github.com/user/repo.git

# Change remote URL
git remote set-url origin https://github.com/user/new-repo.git

# Remove remote
git remote remove origin

# Rename remote
git remote rename origin upstream

# Show remote info
git remote show origin
```

### Stashing

**Save work temporarily**:
```bash
# Stash current changes
git stash
git stash save "Work in progress on feature X"

# Stash including untracked files
git stash -u
git stash --include-untracked

# Stash including untracked and ignored files
git stash -a
git stash --all

# Stash only unstaged changes
git stash --keep-index
```

**Apply stashed changes**:
```bash
# List stashes
git stash list

# Apply most recent stash (keep in stash list)
git stash apply

# Apply specific stash
git stash apply stash@{2}

# Apply and remove from stash list
git stash pop

# Apply to different branch
git stash branch new-branch stash@{0}
```

**Manage stashes**:
```bash
# Show stash contents
git stash show
git stash show -p  # With diff

# Show specific stash
git stash show stash@{1}

# Drop specific stash
git stash drop stash@{0}

# Clear all stashes
git stash clear
```

### Tagging

**Create tags**:
```bash
# Lightweight tag
git tag v1.0.0

# Annotated tag (recommended)
git tag -a v1.0.0 -m "Release version 1.0.0"

# Tag specific commit
git tag -a v1.0.0 <commit-hash> -m "Message"
```

**List and show tags**:
```bash
# List all tags
git tag
git tag -l
git tag --list

# Search tags
git tag -l "v1.*"

# Show tag details
git show v1.0.0
```

**Push and delete tags**:
```bash
# Push specific tag
git push origin v1.0.0

# Push all tags
git push --tags

# Delete local tag
git tag -d v1.0.0

# Delete remote tag
git push origin --delete v1.0.0
git push origin :refs/tags/v1.0.0
```

### Common Workflows Without Worktrees

**Feature branch workflow**:
```bash
# 1. Update main branch
git checkout main
git pull --ff-only

# 2. Create feature branch
git checkout -b feat/new-feature

# 3. Work on feature
git add .
git commit -m "feat: implement X"

# 4. Keep feature branch updated
git fetch origin
git rebase origin/main

# 5. Push feature branch
git push -u origin feat/new-feature

# 6. After code review - merge
git checkout main
git pull --ff-only
git merge --no-ff feat/new-feature
git push origin main

# 7. Cleanup
git branch -d feat/new-feature
git push origin --delete feat/new-feature
```

**Hotfix workflow**:
```bash
# 1. Create hotfix from main
git checkout main
git pull --ff-only
git checkout -b hotfix/critical-bug

# 2. Fix and commit
git add .
git commit -m "fix: resolve critical bug"

# 3. Merge to main
git checkout main
git merge --no-ff hotfix/critical-bug
git tag -a v1.0.1 -m "Hotfix release"
git push origin main --tags

# 4. Merge to develop (if exists)
git checkout develop
git merge --no-ff hotfix/critical-bug
git push origin develop

# 5. Cleanup
git branch -d hotfix/critical-bug
```

**Bisect for debugging**:
```bash
# Find commit that introduced bug
git bisect start
git bisect bad  # Current commit is bad
git bisect good v1.0.0  # This commit was good

# Git checks out commit to test
# Test the code, then:
git bisect good  # or git bisect bad

# Repeat until Git finds the culprit
# When done:
git bisect reset
```

## Troubleshooting

### Worktree Path Issues

**Problem**: "fatal: '<path>' already exists"

```bash
# Check if worktree is registered
git worktree list

# If stale, prune and retry
git worktree prune
rm -rf <path>  # if directory still exists
git worktree add -b <branch> <path> <base>
```

**Problem**: "fatal: '<path>' is not a working tree"

```bash
# Remove stale registration
git worktree prune

# Manually remove if needed
rm -rf <path>
```

### Branch Already Exists

**Problem**: "fatal: A branch named 'feat/task' already exists"

```bash
# Check if branch exists and where
git branch -a | grep feat/task

# If you want to reuse existing branch
git worktree add <path> feat/task

# If you want to delete old branch and start fresh
git branch -D feat/task
git worktree add -b feat/task <path> <base>
```

### Lock Files

**Problem**: "fatal: 'worktrees/<name>' is locked"

```bash
# Check lock status
cat .git/worktrees/<name>/locked

# Remove lock (if safe)
rm .git/worktrees/<name>/locked

# Or use prune to clean
git worktree prune
```

### Hook Not Blocking Wrong Branch

**Problem**: Able to commit on wrong branch despite hooks

```bash
# Verify hooks are executable
HOOK_PATH="$(git rev-parse --git-path hooks/pre-commit)"
ls -la "$HOOK_PATH"
chmod +x "$HOOK_PATH"

# Check metadata files exist
GIT_DIR="$(git rev-parse --git-dir)"
cat "$GIT_DIR/agent-expected-branch"  # Should show expected branch
cat "$GIT_DIR/agent-expected-worktree"  # Should show current worktree path

# Reinstall guardrails if needed
cd /path/to/AgentStackGuide
./scripts/setup-agent-guardrails.sh --target /path/to/your-repo --guardrails on --force
```

## Common Workflows

### Full Multi-Agent Setup

```bash
# 1. Fetch latest
git fetch --all --prune

# 2. Create worktrees directory
mkdir -p ../myapp-worktrees

# 3. Create worktrees for three agents
git worktree add -b feat/codex-fix-auth ../myapp-worktrees/codex-fix-auth develop
git worktree add -b feat/claude-add-metrics ../myapp-worktrees/claude-add-metrics develop
git worktree add -b feat/copilot-docs ../myapp-worktrees/copilot-docs develop

# 4. Configure each worktree (repeat for each)
cd ../myapp-worktrees/codex-fix-auth
GIT_DIR="$(git rev-parse --git-dir)"
printf '%s\n' "feat/codex-fix-auth" > "$GIT_DIR/agent-expected-branch"
printf '%s\n' "$(pwd -P)" > "$GIT_DIR/agent-expected-worktree"

# 5. Verify setup
git worktree list
```

### Clean Merge and Cleanup

```bash
# 1. Review changes
git log --oneline develop..feat/codex-fix-auth
git diff --stat develop..feat/codex-fix-auth

# 2. Merge to develop
cd ~/src/myapp
git checkout develop
git pull --ff-only
git merge --no-ff feat/codex-fix-auth
git push origin develop

# 3. Clean up worktree
git worktree remove ../myapp-worktrees/codex-fix-auth
git branch -d feat/codex-fix-auth  # -d ensures branch was merged

# 4. Prune metadata
git worktree prune
```

### Discard Failed Branch

```bash
# 1. Remove worktree
cd ~/src/myapp
git worktree remove ../myapp-worktrees/claude-add-metrics
# or force if needed:
git worktree remove --force ../myapp-worktrees/claude-add-metrics

# 2. Delete branch (use -D to force)
git branch -D feat/claude-add-metrics

# 3. Delete remote branch if pushed
git push origin --delete feat/claude-add-metrics

# 4. Clean up metadata
git worktree prune
```

## Git Configuration for Safety

### Per-Worktree Settings

```bash
# In each worktree, configure safe defaults
cd <worktree-path>

# Only push current branch (not all matching branches)
git config --local push.default current

# Require fast-forward for pulls
git config --local pull.ff only

# Prevent accidental push of all branches
git config --local remote.pushDefault origin
```

### Useful Aliases

```bash
# Global aliases for worktree operations
git config --global alias.wt "worktree"
git config --global alias.wtl "worktree list"
git config --global alias.wta "worktree add"
git config --global alias.wtr "worktree remove"
git config --global alias.wtp "worktree prune"

# Branch visualization
git config --global alias.br "branch -vv"
git config --global alias.bra "branch -avv"

# Safer operations
git config --global alias.pullff "pull --ff-only"
```

## Advanced Operations

### Move Worktree

```bash
# Option 1: Remove and recreate
git worktree remove <old-path>
git worktree add <new-path> <branch>

# Option 2: Move directory and update metadata
mv <old-path> <new-path>
git worktree list  # Will show "(error)" for old path
git worktree prune
git worktree repair <new-path>
```

### Repair Worktree

```bash
# Fix broken worktree metadata
git worktree repair

# Repair specific worktree
git worktree repair <path>
```

### Lock Worktree

```bash
# Prevent accidental removal
git worktree lock <path>

# Lock with reason
git worktree lock <path> --reason "Agent currently working"

# Unlock
git worktree unlock <path>
```

## Quick Reference

### Worktree Operations

| Task | Command |
|------|---------|
| Create worktree | `git worktree add -b <branch> <path> <base>` |
| List worktrees | `git worktree list` |
| Remove worktree | `git worktree remove <path>` |
| Clean metadata | `git worktree prune` |
| Set branch binding | `printf '%s\n' "<branch>" > "$(git rev-parse --git-dir)/agent-expected-branch"` |
| Set worktree binding | `printf '%s\n' "$(pwd -P)" > "$(git rev-parse --git-dir)/agent-expected-worktree"` |

### Standard Git Operations

| Task | Command |
|------|---------|
| Create branch | `git checkout -b feat/name` or `git switch -c feat/name` |
| Switch branch | `git checkout main` or `git switch main` |
| List branches | `git branch -vv` (local) or `git branch -avv` (all) |
| Delete branch | `git branch -d name` (safe) or `git branch -D name` (force) |
| Stage changes | `git add .` or `git add file.js` |
| Commit changes | `git commit -m "message"` |
| Amend commit | `git commit --amend` |
| View status | `git status` or `git status -sb` |
| View diff | `git diff` (unstaged) or `git diff --staged` (staged) |
| View history | `git log --oneline --graph --all` |
| Merge branch | `git merge --no-ff feat/name` |
| Rebase branch | `git rebase main` |
| Abort merge/rebase | `git merge --abort` or `git rebase --abort` |
| Undo last commit | `git reset HEAD~1` (keep changes) |
| Discard changes | `git restore file.js` or `git checkout -- file.js` |
| Stash changes | `git stash` or `git stash -u` |
| Apply stash | `git stash pop` or `git stash apply` |
| Fetch updates | `git fetch --all --prune` |
| Pull changes | `git pull --ff-only` (safe) |
| Push changes | `git push -u origin feat/name` |
| View reflog | `git reflog` |

### Safety Commands

| Task | Command |
|------|---------|
| Verify branch | `git branch --show-current` |
| Verify path | `git rev-parse --show-toplevel` |
| Safe rebase | `git rebase origin/develop` |
| Safe push | `git push origin <current-branch>` |
| Safe pull | `git pull --ff-only` |

## Error Prevention Checklist

Before starting agent work in a worktree:
- [ ] Worktree created with correct branch name
- [ ] Branch follows naming convention `feat/<agent>-<task>`
- [ ] Metadata files written to `.git/agent-expected-branch` and `.git/agent-expected-worktree`
- [ ] Hooks are executable (`chmod +x "$(git rev-parse --git-path hooks)"/pre-*`)
- [ ] Current branch verified with `git branch --show-current`
- [ ] Worktree path verified with `git rev-parse --show-toplevel`
- [ ] Base branch is up to date (`git fetch origin`)
