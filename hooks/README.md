# Hooks Directory - Persistent Agent Memory

This directory stores persistent state for AI agents working on this project.
It survives context window resets, agent crashes, and session restarts.

## Directory Structure

```
hooks/
├── scenes/          # Scene state snapshots (JSON)
├── tasks/           # Individual task assignments and progress
├── convoys/         # Grouped task deliverables
├── messages/        # Inter-agent communication
├── snapshots/       # Point-in-time project snapshots
├── validation/      # Validation results and error reports
└── assets/          # Asset generation registry and tracking
```

## Usage

### For AI Agents
- Read from hooks to restore context after restart
- Write to hooks after completing significant work
- Use validation/ to track and fix errors

### For Humans
- Review tasks/ to see what agents are working on
- Check validation/ for current issues
- Inspect convoys/ to see feature progress

## Git Tracking

This directory is partially tracked by Git:
- Structure and README: Tracked
- Scene states, tasks, convoys: Tracked
- Snapshots, screenshots, recordings: Ignored (too large)
