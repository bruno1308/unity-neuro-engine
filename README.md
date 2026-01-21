# Unity Neuro-Engine

Autonomous AI-driven Unity game development. Build complete games from Game Design Documents (GDDs) with minimal human intervention.

## What is Neuro-Engine?

Neuro-Engine is a framework that enables AI (Claude Code) to autonomously develop Unity games. It provides:

- **7-Layer Architecture**: From code generation to asset creation
- **VContainer DI**: Clean dependency injection for testable code
- **AI Evaluation**: Automated quality checks (syntactic, state, polish)
- **Asset Generation**: Integration with Meshy.ai (3D) and ElevenLabs (audio)
- **Orchestration**: Multi-agent system with specialized "Polecats"

## Installation

### 1. Install Unity Package

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.neuroengine.core": "https://github.com/bruno1308/unity-neuro-engine.git?path=Packages/com.neuroengine.core"
  }
}
```

### 2. Install Claude Plugin

```bash
claude plugin marketplace add bruno1308/unity-neuro-engine-plugin
```

### 3. Install Dependencies

The package requires:
- [VContainer](https://github.com/hadashiA/VContainer) - Dependency injection
- [Unity-MCP](https://github.com/CoplayDev/unity-mcp) - AI ↔ Unity communication

Add to `manifest.json`:
```json
{
  "dependencies": {
    "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer",
    "com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity"
  }
}
```

### 4. Configure (Optional)

For AI asset generation, create `.env` in your project root:
```env
MESHY_API_KEY=your_key
ELEVENLABS_API_KEY=your_key
```

### 5. Verify

Open your project in Claude Code and run:
```
/neuro-engine:validate
```

## Quick Start

```bash
# Create a new game iteration
/neuro-engine:iteration create "My Game"

# Edit the generated GDD
# Assets/Iteration1/GDD.md

# Start autonomous development
/neuro-engine:orchestrate start Assets/Iteration1/GDD.md
```

## Architecture

```
L7: Generative Assets    [Meshy 3D, ElevenLabs audio]
L6: Agent Orchestration  [Mayor, Polecats]
L5: Evaluation Framework [Syntactic, State, Polish graders]
L4: Persistent Artifacts [hooks/ directory]
L3: Interaction          [Input detection, playtesting]
L2: Observation          [Scene state capture]
L1: Code-First           [VContainer DI, interfaces]
```

## Existing Projects

You can integrate Neuro-Engine into existing Unity projects:

**Option A: Full Integration** - Migrate to VContainer DI for full benefits
**Option B: Lightweight** - Just add the package for AI observation/evaluation

See [package documentation](Packages/com.neuroengine.core/README.md) for migration guides.

## Commands

| Command | Description |
|---------|-------------|
| `/neuro-engine:validate` | Check setup and prerequisites |
| `/neuro-engine:iteration` | Manage game iterations |
| `/neuro-engine:evaluate` | Run quality evaluations |
| `/neuro-engine:orchestrate` | Start autonomous development |
| `/neuro-engine:blocker` | Report blockers with layer attribution |

## Requirements

- Unity 2022.3 LTS or newer
- Claude Code with plugin support
- Node.js (for MCP server)

## License

MIT License - see [LICENSE](LICENSE)

## Related

- [Claude Plugin](https://github.com/bruno1308/unity-neuro-engine-plugin) - Claude Code integration
- [Unity-MCP](https://github.com/CoplayDev/unity-mcp) - MCP bridge for Unity
- [VContainer](https://github.com/hadashiA/VContainer) - Dependency injection
