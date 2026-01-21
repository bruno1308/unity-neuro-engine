# Skill: state-query

## Purpose
Query game state via Unity-MCP bridge.

## When to Use
- Debugging game issues
- Verifying game state matches expectations
- Finding objects in scene
- Checking component values

## Context

### Prerequisites
- Unity-MCP package installed
- MCP server running (auto-started by McpAutoStart.cs)
- Unity Editor open with project loaded

### MCP Endpoint
Default: `http://localhost:8080/mcp`

### Available Queries (via MCP tools)
Depends on Unity-MCP implementation. Common operations:
- Get scene hierarchy
- Find GameObjects by name/tag
- Read component values
- Execute editor commands

## Procedure

### Check MCP Connection

```bash
curl -s http://localhost:8080/health
# Expected: OK or JSON status
```

### Query Scene State

Use MCP tools (when available) to:
1. List all GameObjects in scene
2. Get specific object properties
3. Read component data as JSON

### Debugging Flow

1. Identify what state you need
2. Query via MCP
3. Compare against expected values
4. Log discrepancies to hooks/validation/

## Verification
- MCP server responds to health check
- Query returns valid JSON
- State matches expected structure

## Limitations
- Cannot query during Play mode transitions
- Large scenes may timeout
- Binary data (textures, meshes) not queryable
