# Unity Neuro-Engine Package

Runtime Unity package for the Neuro-Engine autonomous game development system.

## Overview

This package provides the Unity-side infrastructure for AI-driven game development:

- **Layer 1 (Code-First Foundation)**: VContainer DI integration, UI Toolkit helpers
- **Layer 2 (Observation)**: Scene state capture, component serialization
- **Layer 3 (Interaction)**: Input System detection, playtest simulation
- **Layer 5 (Evaluation)**: Syntactic, state, and polish graders
- **Layer 7 (Asset Generation)**: Meshy texture postprocessor

## Requirements

- Unity 2022.3 LTS or newer
- [VContainer](https://github.com/hadashiA/VContainer) (dependency injection)
- [Unity-MCP](https://github.com/CoplayDev/unity-mcp) (AI communication)
- Input System package (recommended)
- UI Toolkit (included in Unity)

## Installation

### Via Git URL (Recommended)

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.neuroengine.core": "https://github.com/bruno1308/unity-neuro-engine.git"
  }
}
```

### Via Local Path (Development)

```json
{
  "dependencies": {
    "com.neuroengine.core": "file:../path/to/unity-neuro-engine"
  }
}
```

## Claude Plugin

This package is designed to work with the **Neuro-Engine Claude Plugin**:

- **Repository**: https://github.com/bruno1308/neuro-engine-claude-plugin
- **Purpose**: Provides commands, skills, and agents for Claude Code

Install both for full autonomous development capabilities.

## Quick Start

### 1. Install Dependencies

```json
{
  "dependencies": {
    "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer",
    "com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity",
    "com.neuroengine.core": "https://github.com/bruno1308/unity-neuro-engine.git"
  }
}
```

### 2. Create LifetimeScope

```csharp
using VContainer;
using VContainer.Unity;
using NeuroEngine.Core;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Register your services
        builder.Register<IScoreService, ScoreService>(Lifetime.Singleton);
        builder.Register<IPlayerService, PlayerService>(Lifetime.Singleton);

        // Register entry points
        builder.RegisterEntryPoint<GameController>();
    }
}
```

### 3. Use Dependency Injection

```csharp
using VContainer;

public class GameController : MonoBehaviour
{
    [Inject] private IScoreService _scoreService;
    [Inject] private IPlayerService _playerService;

    // Services are automatically injected by VContainer
}
```

## Existing Projects

### Integration Options

#### Option A: Full Integration (Recommended for new features)

Gradually migrate to VContainer DI:

1. **Create LifetimeScope** for your project
2. **Extract interfaces** from existing services
3. **Register services** in the container
4. **Add `[Inject]`** to classes that need dependencies
5. **Use `IObjectResolver.Instantiate()`** for runtime object creation

#### Option B: Lightweight Integration (Observation only)

Use AI tooling without refactoring:

1. **Add the package** to manifest.json
2. **Configure Claude plugin** with `.mcp.json`
3. AI can observe scenes, run evaluations, generate assets
4. No code changes required

### Migration Guide

#### Step 1: Identify Service Classes

Find singleton managers, game controllers, systems:

```csharp
// Before: Singleton pattern
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    void Awake() => Instance = this;
}
```

#### Step 2: Extract Interface

```csharp
public interface IGameManager
{
    void StartGame();
    void EndGame();
    GameState CurrentState { get; }
}
```

#### Step 3: Register in LifetimeScope

```csharp
protected override void Configure(IContainerBuilder builder)
{
    builder.Register<IGameManager, GameManager>(Lifetime.Singleton);
}
```

#### Step 4: Inject Instead of Singleton Access

```csharp
// Before
GameManager.Instance.StartGame();

// After
[Inject] private IGameManager _gameManager;
_gameManager.StartGame();
```

#### Step 5: Handle Runtime Instantiation

```csharp
// Before: Object.Instantiate doesn't inject
var enemy = Instantiate(enemyPrefab);

// After: Use resolver for injection
[Inject] private IObjectResolver _resolver;
var enemy = _resolver.Instantiate(enemyPrefab);
```

### What Works Without Migration

| Feature | Works? | Notes |
|---------|--------|-------|
| Scene observation | Yes | Captures all GameObjects |
| Console monitoring | Yes | Reads errors/warnings |
| Script validation | Yes | Checks for issues |
| Asset generation | Yes | 3D models, audio |
| Input detection | Yes | Detects Input System mode |
| Polish evaluation | Partial | Some checks need patterns |

## Architecture

### Layer 1: Code-First Foundation

```
Runtime/Core/
├── NeuroEngineLifetimeScope.cs  # DI container setup
└── Interfaces/                   # Core interfaces
```

### Layer 2: Observation

```
Runtime/Services/
├── SceneStateCaptureService.cs   # Scene → JSON snapshots
└── Interfaces/
    └── ISceneStateCapture.cs
```

### Layer 3: Interaction

```
Editor/
├── InputSystemDetector.cs        # Detect input configuration
└── MCPTools/
    ├── CheckInputSystem.cs       # MCP tool for input guidance
    └── PlaytestClick.cs          # Simulate clicks for testing
```

### Layer 5: Evaluation

```
Runtime/Services/
├── SyntacticGraderService.cs     # Code quality checks
├── StateGraderService.cs         # Runtime state verification
└── PolishGraderService.cs        # Game feel evaluation

Editor/MCPTools/
├── EvaluateSyntactic.cs
├── EvaluateState.cs
└── EvaluatePolish.cs
```

### Layer 7: Asset Generation

```
Editor/
└── MeshyTexturePostprocessor.cs  # Auto-configure Meshy textures
```

## MCP Tools

The package exposes tools via Unity-MCP:

| Tool | Description |
|------|-------------|
| `check_input_system` | Get input configuration and code guidance |
| `evaluate_syntactic` | Run syntactic validation on scripts |
| `evaluate_state` | Verify runtime state against criteria |
| `evaluate_polish` | Check game feel (audio, particles, environment) |
| `playtest_click` | Simulate mouse click at position |

## Hooks Directory

The engine uses `hooks/` for persistent state:

```
hooks/
├── scenes/           # Scene snapshots
├── compiler/         # Compilation results
├── tests/            # Test results
├── validation/       # Verification reports
├── assets/           # Generated asset records
└── iterations/       # Per-iteration state
```

## Troubleshooting

### "VContainer not found"

Add VContainer to manifest.json:
```json
"jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer"
```

### "Injection not working"

1. Ensure GameObject has a LifetimeScope in parent hierarchy
2. Check that service is registered in Configure()
3. Use `[Inject]` attribute on fields/properties
4. For runtime objects, use `IObjectResolver.Instantiate()`

### "Input System errors"

Run `/neuro-engine:validate` to check input configuration, or:
```csharp
var mode = InputSystemDetector.GetProjectInputMode();
Debug.Log(InputSystemDetector.GetRecommendedInputCode(mode));
```

### "Meshy textures look wrong"

The `MeshyTexturePostprocessor` should auto-configure. If not:
1. Check texture is in `Models/Textures/` folder
2. Verify filename ends with `_Normal`, `_Metallic`, etc.
3. Reimport the texture

## Contributing

1. Fork this repository
2. Make changes following the layer architecture
3. Run `/neuro-engine:evaluate` to verify
4. Submit pull request

For Claude plugin changes, contribute to [neuro-engine-claude-plugin](https://github.com/bruno1308/neuro-engine-claude-plugin).

## License

MIT License - see [LICENSE](LICENSE)

## Related

- [Neuro-Engine Claude Plugin](https://github.com/bruno1308/neuro-engine-claude-plugin) - Claude Code integration
- [VContainer](https://github.com/hadashiA/VContainer) - Dependency injection
- [Unity-MCP](https://github.com/CoplayDev/unity-mcp) - MCP bridge for Unity
