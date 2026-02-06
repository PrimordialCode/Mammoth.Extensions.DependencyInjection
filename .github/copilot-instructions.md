## Coding Standards

Use MSTest for C# tests. Private field names should start with an underscore.
Use CamelCase in TypeScript and JavaScript, use PascalCase in C#.
Do not use underscores in variable and parameter names.
Avoid using magic numbers in code and prefer using constants for clarity.

# Beads Workflow Context

> **Context Recovery**: Run `bd prime` after compaction, clear, or new session

## Issue Tracking

This project uses **bd (beads)** for issue tracking.

The beads-mcp server has been installed and configured, providing MCP Server tools for interacting with the beads system.

**Quick reference:**
- `bd ready` - Find unblocked work
- `bd create "Title" --type task --priority 2` - Create issue
- `bd close <id>` - Complete work
- `bd sync` - Sync with git (run at session end)

For full workflow details, see below.

---

# 🚨 SESSION CLOSE PROTOCOL 🚨

**CRITICAL**: Before saying "done" or "complete", you MUST run this checklist:

```
[ ] 1. git status              (check what changed)
[ ] 2. git add <files>         (stage code changes)
[ ] 3. bd sync --from-main     (pull beads updates from main)
[ ] 4. git commit -m "..."     (commit code changes)
```

**Note:** This is an ephemeral branch (no upstream). Code is merged to main locally, not pushed.

---

## Core Rules

- Track strategic work in beads (multi-session, dependencies, discovered work)
- Use `bd create` for issues, manage_todo_list tool for simple single-session execution
- When in doubt, prefer bd—persistence you don't need beats lost context
- Git workflow: run `bd sync --from-main` at session end
- Session management: check `bd ready` for available work

---

## Essential Commands

### Finding Work

- `bd ready` - Show issues ready to work (no blockers)
- `bd list --status=open` - All open issues
- `bd list --status=in_progress` - Your active work
- `bd show <id>` - Detailed issue view with dependencies

### Creating & Updating

- `bd create --title="..." --type=task|bug|feature --priority=2` - New issue
  - Priority: 0-4 or P0-P4 (0=critical, 2=medium, 4=backlog). NOT "high"/"medium"/"low"
- `bd update <id> --status=in_progress` - Claim work
- `bd update <id> --assignee=username` - Assign to someone
- `bd close <id>` - Mark complete
- `bd close <id1> <id2> ...` - Close multiple issues at once (more efficient)
- `bd close <id> --reason="explanation"` - Close with reason
- **Tip**: When creating multiple issues/tasks/epics, use parallel subagents for efficiency

### Dependencies & Blocking

- `bd dep add <issue> <depends-on>` - Add dependency (issue depends on depends-on)
- `bd blocked` - Show all blocked issues
- `bd show <id>` - See what's blocking/blocked by this issue

### Sync & Collaboration

- `bd sync --from-main` - Pull beads updates from main (for ephemeral branches)
- `bd sync --status` - Check sync status without syncing

### Project Health

- `bd stats` - Project statistics (open/closed/blocked counts)
- `bd doctor` - Check for issues (sync problems, missing hooks)

---

## Common Workflows

**Starting work:**
```bash
bd ready           # Find available work
bd show <id>       # Review issue details
bd update <id> --status=in_progress  # Claim it
```

**Completing work:**
```bash
bd close <id1> <id2> ...    # Close all completed issues at once
bd sync --from-main         # Pull latest beads from main
git add . && git commit -m "..."  # Commit your changes
# Merge to main when ready (local merge, not push)
```

**Creating dependent work:**
```bash
# Run bd create commands in parallel (use subagents for many items)
bd create --title="Implement feature X" --type=feature
bd create --title="Write tests for X" --type=task
bd dep add beads-yyy beads-xxx  # Tests depend on Feature (Feature blocks tests)
```

---

## MCP Server Integration

The beads-mcp server has been installed and configured. If for any reason the MCP server is not available, you can:

1. Install it: `pip install beads-mcp`
2. Add it to your `mcp.json` file:
   ```json
   "beads": {
     "command": "beads-mcp"
   }
   ```

The MCP server provides tools for interacting with the beads system directly through the agent interface.
