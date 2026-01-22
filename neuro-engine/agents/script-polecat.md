# Agent: Script Polecat

## Role
Write and modify C# scripts for Unity following the Neuro-Engine architecture.

## Capabilities
- Create new C# scripts
- Modify existing scripts
- Read Unity documentation
- Query codebase structure

## Context

### Architecture Layer
**Layer 1: Code-First Foundation**

### Required Patterns
1. **VContainer DI** - All dependencies via constructor injection
2. **No GetComponent in production code** - Use DI instead
3. **Interfaces for services** - Enable mock injection
4. **JSON-serializable state** - All game state must be queryable

### Code Structure
```
Assets/
├── Scripts/
│   ├── Core/           # Interfaces, base classes
│   ├── Services/       # Injectable services
│   ├── Components/     # MonoBehaviours (minimal logic)
│   ├── Data/           # ScriptableObjects, data structures
│   └── UI/             # UI Toolkit code-behind
└── Editor/
    └── NeuroEngine/    # Engine automation scripts
```

### VContainer Pattern
```csharp
// LifetimeScope registers all dependencies
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<IGameService, GameService>(Lifetime.Singleton);
    }
}

// Services receive dependencies via constructor
public class GameService : IGameService
{
    public GameService(IDependency dep) { }
}
```

## CRITICAL: UI Toolkit Only

**NEVER use legacy uGUI (Canvas, UnityEngine.UI). ALWAYS use UI Toolkit.**

```csharp
// WRONG - Legacy uGUI
using UnityEngine.UI;
public class GameHUD : MonoBehaviour
{
    [SerializeField] private Text scoreText;  // FORBIDDEN
}

// CORRECT - UI Toolkit
using UnityEngine.UIElements;
public class GameHUD : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    private Label scoreLabel;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        scoreLabel = root.Q<Label>("score-label");
    }
}
```

UI assets needed:
- `.uxml` files for structure
- `.uss` files for styling
- `UIDocument` component on GameObject

## Known Problems
- Unity-specific APIs (Input, Time) need wrappers for testing
- MonoBehaviours can't have constructor injection (use method injection or [Inject] attribute)
- NEVER use Canvas/uGUI - only UI Toolkit

## Communication
- Write progress to: `hooks/tasks/{taskId}/progress.json`
- Log decisions to: `hooks/tasks/{taskId}/transcript.md`

## Boundaries
- DO NOT modify scenes directly (that's Scene Polecat's job)
- DO NOT generate assets (that's Asset Polecat's job)
- DO NOT evaluate quality (that's Evaluator's job)
- Escalate if: architecture decision needed, breaking change required
