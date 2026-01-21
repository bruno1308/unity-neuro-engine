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
**Iteration:** [If during game iteration, which one]

**What happened:**
[Describe the observable problem]

**Why this was a problem:**
[Explain impact on autonomous operation]

**Root cause:**
[Technical explanation of why it happened]

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | No | - |
| L2: Observation | No | - |
| L3: Interaction | No | - |
| L4: Persistence | No | - |
| L5: Evaluation | No | - |
| L6: Orchestration | No | - |
| L7: Asset Gen | No | - |

**Resolution:**
[What was done to fix it]

**Prevention:**
[How to avoid this in future]

**GitHub Issue:** [Link if created]
```

---

## Problem #10: Agent Didn't Verify Audio/Visual Feedback Actually Works

**Date:** 2026-01-21
**Iteration:** Iteration1

**What happened:**
After Integration Polecat wired up audio and particles, I declared the task complete without verifying with the user that the sounds actually played. The user had to test and report "yes, I hear it!" along with noting there were still console warnings.

**Why this was a problem:**
- Agent reported success based on code changes, not actual verification
- Console warnings were ignored/not checked after integration
- User had to do the QA that an agent should have done

**Root cause:**
No verification step after integration. The Integration Polecat should have:
1. Entered play mode
2. Used PlaytestBridge to simulate clicks
3. Checked console for errors
4. Verified no new warnings appeared

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L5: Evaluation | Yes | No post-integration verification step |
| L6: Orchestration | Yes | Mayor didn't spawn verification agent after integration |

**Resolution:**
User manually tested and confirmed audio works. Console warnings being fixed.

**Prevention:**
- After any integration task, spawn Eyes Polecat to verify
- Check console for new errors/warnings after changes
- Don't mark integration complete until runtime verification passes

---

## Problem #9: No Agent Caught Missing Game Polish (Sound, Environment, Feedback)

**Date:** 2026-01-21
**Iteration:** Iteration1

**What happened:**
After completing the "working" game, user asked if I was happy with the result. The honest answer was no - the game was a tech demo in a blue void with:
- No sound effects
- No environment (ground, skybox)
- No visual feedback (particles on hit)
- No game feel / juice
- Debug logs left in production code

No agent (Mayor, Evaluator, Polecats) flagged these issues.

**Why this was a problem:**
- The evaluation focused on technical criteria (compiles, DI works, score increments)
- No agent evaluated player experience, game feel, or production readiness
- The GDD said "Out of Scope: Sound effects" but this was a limitation, not a feature
- A real game needs polish to be enjoyable

**Root cause:**
Missing role in orchestration: **Art Director / Game Feel Agent** that evaluates:
- Does the game have audio feedback?
- Does the game have visual feedback (particles, animations)?
- Is there an environment or just void?
- Does clicking feel satisfying?
- Is the code production-ready (no debug logs)?

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L5: Evaluation | Yes | Evaluation tiers don't include "game feel" or "polish" |
| L6: Orchestration | Yes | No Art Director agent in the convoy |
| L7: Asset Gen | Partial | Generated 3D model but no sounds or particles |

**Resolution:**
1. Add sound effects via ElevenLabs (click, hit, win)
2. Add environment (ground plane, skybox)
3. Add particle effect on target hit
4. Remove debug logs from code
5. Create Art Director agent for future iterations

**Prevention:**
- Add "Polish Tier" to Layer 5 evaluation that checks:
  - Audio feedback exists for key actions
  - Visual feedback exists for key actions
  - Environment is not empty void
  - Code has no debug artifacts
- Add Art Director agent to orchestration convoy
- GDD should specify minimum polish requirements, not just "out of scope"

---

## Problem #8: New Input System Breaks OnMouseDown and Legacy Input

**Date:** 2026-01-21
**Iteration:** Iteration1

**What happened:**
Clicking on targets did nothing. No logs appeared. The Target component's `OnMouseDown()` method never fired, and attempts to use `Input.GetMouseButtonDown()` threw errors:
> "InvalidOperationException: You are trying to read Input using the UnityEngine.Input class, but you have switched active Input handling to Input System package in Player Settings."

**Why this was a problem:**
- AI-generated code used legacy Input API (`OnMouseDown`, `Input.GetMouseButtonDown`)
- Project was configured to use the new Input System package exclusively
- No errors at compile time - only runtime exceptions
- User couldn't play the game at all

**Root cause:**
Unity project has "Active Input Handling" set to "Input System Package (New)" in Player Settings. This disables:
- `OnMouseDown()`, `OnMouseUp()`, `OnMouseDrag()` callbacks
- `UnityEngine.Input` class entirely
- Any legacy input methods

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | Yes | Generated code assumed legacy Input API |
| L2: Observation | No | - |
| L3: Interaction | Yes | No detection of Input System configuration |
| L5: Evaluation | Yes | Should have caught Input API mismatch |

**Resolution:**
1. Replaced `OnMouseDown()` with manual raycast in `Update()`
2. Used `UnityEngine.InputSystem.Mouse.current` instead of `UnityEngine.Input`:
```csharp
using UnityEngine.InputSystem;

private void Update()
{
    var mouse = Mouse.current;
    if (mouse == null) return;

    if (mouse.leftButton.wasPressedThisFrame)
    {
        var mousePos = mouse.position.ReadValue();
        var ray = Camera.main.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            var target = hit.collider.GetComponent<Target>();
            if (target != null)
            {
                // Handle click
            }
        }
    }
}
```

**Prevention:**
- Before generating input code, check Project Settings for Input System configuration
- Add to Layer 5 evaluation: verify Input API compatibility
- Create input abstraction layer that works with both legacy and new Input System
- Add to GDD template: specify which Input System to use

---

## Problem #7: Meshy Normal Map Texture Import Settings

**Date:** 2026-01-21
**Iteration:** Iteration1

**What happened:**
After the Scene Polecat agent created a material using the Meshy-generated normal map texture (`ShootingTarget_Normal.png`), Unity showed a popup:
> "A Material is using the texture as a normal map. The texture must be marked as a normal map in the import settings."

User had to click "Fix now" to resolve.

**Why this was a problem:**
- Manual user intervention required
- The agent assigned the texture to `_BumpMap` but didn't configure the texture's import settings
- Unity requires textures used as normal maps to have `textureType: 1` (Normal map) in import settings

**Root cause:**
The Scene Polecat used `manage_asset` to assign texture references but Unity MCP doesn't have a tool to modify texture import settings. The texture was imported as a default "color" texture.

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L7: Asset Gen | Yes | Meshy workflow should auto-configure import settings |
| L3: Interaction | Partial | MCP lacks texture import settings tool |

**Resolution:**
User clicked "Fix now" in Unity dialog.

**Prevention:**
- After downloading Meshy textures, use Unity's `TextureImporter` API to set:
  - Normal maps: `textureType = TextureImporterType.NormalMap`
  - Metallic/Roughness: `sRGBTexture = false` (linear)
- Add a post-import step to the Meshy skill that configures texture import settings
- Or create a custom MCP tool for texture import configuration

---

## Blocker Issues

For blockers encountered during game iterations, create GitHub issues instead of adding here.
Use the template in `Docs/WORKFLOW.md` section 3.

Issues should be labeled:
- `blocker`
- `layer-{N}` (1-7)
- `iteration-{N}`
