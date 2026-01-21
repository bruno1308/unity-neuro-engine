# Layer 1: Code-First Foundation - Examples

> Code samples for Dependency Injection, UI Toolkit, and Data-Oriented State

---

## 1. Dependency Injection (VContainer)

### GameLifetimeScope.cs — The Application Map

```csharp
// GameLifetimeScope.cs — The "Map" of the application, readable by AI
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Services
        builder.Register<IInputService, InputSystemWrapper>(Lifetime.Singleton);
        builder.Register<IInventoryService, InventoryManager>(Lifetime.Scoped);
        builder.Register<IAudioService, AudioManager>(Lifetime.Singleton);

        // Entry points
        builder.RegisterEntryPoint<GameLoop>();
    }
}
```

**AI Advantage:**
- AI reads `GameLifetimeScope.cs` to understand available services instantly
- No hallucinating where logic lives
- Enables mock injection for isolated testing

---

## 2. UI Toolkit (UXML/USS)

### MainMenu.uxml — Structure

```xml
<!-- MainMenu.uxml -->
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="root" class="menu-container">
        <ui:Label name="title" text="My Game" class="title-text"/>
        <ui:Button name="StartButton" text="Start Game" class="menu-button"/>
        <ui:Button name="OptionsButton" text="Options" class="menu-button"/>
        <ui:Button name="QuitButton" text="Quit" class="menu-button"/>
    </ui:VisualElement>
</ui:UXML>
```

### MainMenu.uss — Style

```css
/* MainMenu.uss */
.menu-container {
    flex-direction: column;
    align-items: center;
    justify-content: center;
}
.menu-button {
    width: 200px;
    height: 50px;
    margin: 10px;
}
```

**AI Advantage:**
- LLMs excel at generating XML/HTML structures
- Complete UI restyling via single text file edit
- Clean separation of structure, style, and logic

---

## 3. Data-Oriented State

### Option A: Unity ECS (DOTS)

```csharp
// State is just data in structs
public struct Health : IComponentData { public int Current; public int Max; }
public struct Position : IComponentData { public float3 Value; }

// Query produces flat, parseable data
var query = EntityManager.CreateEntityQuery(typeof(Health), typeof(Position));
```

### Option B: Runtime Scene Serialization

```json
{
  "scene_name": "Gameplay",
  "entities": [
    {
      "id": "player_001",
      "components": {
        "Health": {"current": 85, "max": 100},
        "Transform": {"position": [0, 1, 5]}
      }
    },
    {
      "id": "enemy_001",
      "components": {
        "Health": {"current": 50, "max": 50},
        "AIState": {"behavior": "patrol"}
      }
    }
  ]
}
```

**AI Advantage:**
- Debug via text analysis, not breakpoints
- "Enemy not dying" → Read JSON → See `Health: 0` but `State: Alive` → Logic bug identified

---

## References

- [VContainer GitHub](https://github.com/hadashiA/VContainer)
- [UI Toolkit Documentation](https://docs.unity3d.com/Manual/UIElements.html)
- [Unity ECS/DOTS](https://unity.com/ecs)
- [Runtime Scene Serialization](https://docs.unity3d.com/Packages/com.unity.runtime-scene-serialization@0.3/)
