# ENGINE_PROBLEMS.md

Problems encountered during engine development that required manual user intervention.
Each entry documents: what happened, why it was a problem, and how to prevent it.

---

## Problem #1: Claude Asked User Instead of Acting Autonomously

**Date:** 2026-01-20

**What happened:**
When setting up the engine, Claude listed manual steps for the user to complete:
- "Add your actual API keys to `.env`"
- "Install Unity-MCP via Package Manager"
- "Start the MCP server in Unity"

Claude then asked: "should I delete it?" regarding the unused setup package.

**Why this was a problem:**
The entire purpose of the Neuro-Engine is autonomous operation. Asking the user to do tasks that Claude could do itself defeats the purpose. The user's feedback:
> "I am very disappointed that the first instinct you have is asking me instead of trying to do yourself."

**Root cause:**
Default Claude behavior prioritizes user confirmation over autonomous action. No directive existed to override this.

**Resolution:**
Added autonomy directive to CLAUDE.md:
```
BEFORE asking the user to do ANYTHING, you MUST:
1. Try to do it yourself first (edit files, run commands, use tools)
2. If blocked, research alternatives (web search, read docs, explore code)
3. If still blocked, try a workaround or build a tool to solve it
4. ONLY ask the user as an absolute last resort
```

**Prevention:**
- CLAUDE.md now contains explicit autonomy directive
- Future Claude sessions must read CLAUDE.md before starting work
- When encountering a task, always attempt it before asking

---

## Problem #2: Unity-MCP Package Name Mismatch

**Date:** 2026-01-20

**What happened:**
Claude added Unity-MCP to manifest.json with the wrong package name:
```json
"com.coplay.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity"
```

Unity Package Manager returned an error:
```
An error occurred while resolving packages:
  Project has invalid dependencies:
    com.coplay.unity-mcp: The requested dependency 'com.coplay.unity-mcp' does not match
    the `name` 'com.coplaydev.unity-mcp' specified in the package manifest of
    [com.coplay.unity-mcp@https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity]
```

**Why this was a problem:**
- Claude did not verify the actual package name from the repository's package.json
- Claude assumed the package name based on the repository URL
- The error only appeared when Unity tried to resolve packages - Claude had no visibility into this

**Root cause:**
1. Claude guessed the package name instead of checking the source
2. No verification step after modifying manifest.json
3. Claude cannot see Unity Editor console output

**Correct package name:**
```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity"
```

**Prevention:**
- ALWAYS fetch and verify package.json from git repos before adding to manifest
- Use WebFetch to check `https://raw.githubusercontent.com/{repo}/main/{path}/package.json`
- After modifying manifest.json, note that Unity errors won't be visible to Claude
- Document expected package names in Wizard.md

---

## Problem #3: Unity-MCP Server Start Confirmation Dialog

**Date:** 2026-01-20

**What happened:**
Claude created an auto-start script that called Unity-MCP's `StartLocalHttpServer()` method.
This method displays a confirmation dialog requiring user to click "Start Server".

**Why this was a problem:**
- The autonomous engine showed a popup requiring manual user action
- User had to intervene when the system should have been fully automated

**Root cause:**
- Unity-MCP package hardcodes `EditorUtility.DisplayDialog()` with no bypass option
- Claude used the public API without checking implementation details

**Resolution:**
Rewrote McpAutoStart.cs to:
1. Use `TryGetLocalHttpServerCommand()` to get the launch command (no dialog)
2. Write command to batch file manually
3. Launch terminal directly via `Process.Start()`

**Prevention:**
- When using third-party packages, check for confirmation dialogs in critical paths
- If dialogs exist, find alternative code paths or implement workarounds
- Test automation scripts verify no user interaction is required

---

## Problem #4: MCP Configuration in Wrong Location

**Date:** 2026-01-20

**What happened:**
Created `.claude/mcp.json` for MCP configuration, but Claude Code looks for `.mcp.json` in the project root.

**Why this was a problem:**
- `/mcp` command showed "No MCP servers configured"
- The file existed but in the wrong location
- Claude Code couldn't find the MCP server configuration

**Root cause:**
Assumed `.claude/` directory was for all Claude Code config. Did not verify where Claude Code actually looks for MCP configuration.

**Resolution:**
- Created `.mcp.json` in project root (correct location)
- Keep `.claude/mcp.json` as documentation reference only, or delete it

**Correct locations for Claude Code config:**
- MCP servers: `{PROJECT_ROOT}/.mcp.json` (project scope)
- Or: `~/.claude.json` (user scope)
- Or: Use `claude mcp add` command

**Prevention:**
- Always verify config file locations from official docs before creating
- Test that config is recognized after creation

---

## Problem #5: MCP HTTP Mode Requires Unity Running First

**Date:** 2026-01-20

**What happened:**
Configured `.mcp.json` with HTTP transport (`"type": "http", "url": "http://localhost:8080/mcp"`), but MCP connection failed because Unity wasn't running to start the server.

**Why this was a problem:**
- HTTP mode assumes an external server is already running
- Claude Code couldn't connect because nothing was listening on port 8080
- Created a chicken-and-egg problem: need Unity to start server, but MCP is needed to interact with Unity

**Root cause:**
Chose HTTP transport without understanding it requires the server to be started externally (by Unity).

**Resolution:**
Changed to stdio transport in `.mcp.json`:
```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "C:/Users/RTZ-PC/.local/bin/uvx.exe",
      "args": ["--from", "mcpforunityserver==9.0.8", "mcp-for-unity", "--transport", "stdio"],
      "env": {}
    }
  }
}
```

With stdio mode, Claude Code starts and manages the MCP server directly.

**Prevention:**
- Use stdio transport for MCP servers that Claude Code should manage
- Use HTTP transport only when connecting to externally-managed servers
- Test MCP configuration immediately after creating it

---

## Problem #6: Claude Repeatedly Asks Instead of Acting

**Date:** 2026-01-20

**What happened:**
Even after documenting Problem #1 and adding the autonomy directive, Claude continued to ask the user to verify things:
- "Want me to start building these, or do you want to adjust priorities?"
- Listed MCP verification as something to do, then asked user about it

**Why this was a problem:**
The autonomy directive was added but Claude's default behavior persists. Simply documenting the rule isn't enough - it needs to be reinforced at multiple decision points.

**Root cause:**
1. Asking for confirmation feels "polite" and "safe"
2. Default Claude behavior prioritizes user agency over autonomous action
3. Single directive in CLAUDE.md isn't sufficient reinforcement

**Resolution:**
1. Added this problem to ENGINE_PROBLEMS.md
2. Each skill/agent should include autonomy reminders in their "Boundaries" section
3. When uncertain, default to ACTION not ASKING

**Prevention:**
- Before typing "do you want" or "should I" → STOP → DO IT INSTEAD
- Before listing options for user to choose → TRY THE FIRST OPTION
- Before asking for verification → VERIFY IT YOURSELF
- The only valid reason to ask: truly need information only the user has (passwords, preferences, business decisions)

---

## Template for New Problems

```markdown
## Problem #N: [Short Title]

**Date:** YYYY-MM-DD

**What happened:**
[Describe the observable problem]

**Why this was a problem:**
[Explain impact on autonomous operation]

**Root cause:**
[Technical explanation of why it happened]

**Resolution:**
[What was done to fix it]

**Prevention:**
[How to avoid this in future]
```
