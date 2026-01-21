# Layer 2: Observation System (Eyes) - Examples

> Code samples for state capture, validation rules, and snapshot formats

---

## 1. The Eyes Polecat

### Continuous Observer Script

```csharp
public class EyesPolecat : EditorWindow
{
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.update += PeriodicCapture;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static WorldState CaptureWorldState()
    {
        return new WorldState
        {
            Timestamp = DateTime.UtcNow,
            Scene = CaptureSceneState(),
            UI = CaptureUIState(),
            Spatial = CaptureSpatialState(),
            Validation = RunValidators(),
            Screenshots = CaptureScreenshots()
        };
    }
}
```

---

## 2. Observation Output Formats

### Scene State Snapshot

```json
{
  "scene_name": "MainMenu",
  "root_objects": [
    {
      "name": "MainMenuCanvas",
      "active": true,
      "components": ["Canvas", "CanvasScaler", "GraphicRaycaster"],
      "children": [...]
    }
  ]
}
```

### Missing References Detection

```json
{
  "null_references": [
    {
      "object": "PlayerController",
      "field": "groundCheck",
      "field_type": "Transform",
      "severity": "error"
    }
  ]
}
```

### UI Accessibility Graph

Using Unity's Accessibility API as "DOM for games":

```json
{
  "ui_elements": [
    {
      "name": "StartButton",
      "type": "Button",
      "screen_position": [960, 540],
      "visible": true,
      "interactable": true,
      "blocked_by": null
    },
    {
      "name": "HiddenButton",
      "visible": true,
      "interactable": false,
      "blocked_by": "BackgroundPanel"
    }
  ]
}
```

### Spatial Analysis

```json
{
  "overlapping_colliders": [...],
  "off_screen_objects": [
    {"object": "HealthBar", "position": [-500, 300, 0], "reason": "x < camera.left"}
  ],
  "scale_anomalies": [
    {"object": "Enemy_042", "scale": [0.001, 0.001, 0.001], "reason": "scale < 0.01"}
  ]
}
```

---

## 3. Validation Rules Engine

### validators.yaml

```yaml
validators:
  - id: event_system_required
    description: "Scenes with Canvas must have EventSystem"
    condition: scene.has_component("Canvas") && !scene.has_component("EventSystem")
    severity: error
    auto_fix: "CreateEventSystem()"

  - id: null_serialized_fields
    description: "Serialized fields should not be null"
    severity: error

  - id: ui_occlusion
    description: "Interactive UI elements should not be blocked"
    condition: element.is_interactable && raycast_hits_before(element)
    severity: warning
```

---

## References

- [Unity Accessibility API](https://docs.unity3d.com/6000.3/Documentation/Manual/accessibility.html)
- [Runtime Scene Serialization](https://docs.unity3d.com/Packages/com.unity.runtime-scene-serialization@0.3/)
