# Animation Pipeline (Mixamo) - Examples

> Code samples for auto-rigging, animation presets, and procedural animation

---

## 1. Mixamo Auto-Rigging Pipeline

```python
class MixamoAnimationPipeline:
    """Automatically rigs and animates Meshy-generated models."""

    def __init__(self):
        self.mixamo = MixamoClient()
        self.animation_presets = self.load_animation_presets()

    def process_character(self, model_path: str, character_type: str) -> dict:
        """Full pipeline: static model → rigged and animated."""

        # Step 1: Upload to Mixamo for auto-rigging
        character_id = self.mixamo.upload_character(model_path)

        # Step 2: Auto-rig (Mixamo's AI does this automatically)
        rig_result = self.mixamo.auto_rig(character_id)

        if not rig_result.success:
            return {"success": False, "error": "Auto-rig failed"}

        # Step 3: Download animations based on character type
        animations = self.animation_presets.get(character_type, "humanoid")
        downloaded = []

        for anim in animations:
            result = self.mixamo.download_animation(
                character_id=character_id,
                animation_name=anim["name"],
                format="fbx",
                arm_space=anim.get("arm_space", 0),
                trim_start=anim.get("trim_start", 0),
                trim_end=anim.get("trim_end", 100)
            )
            downloaded.append(result.path)

        # Step 4: Import to Unity
        unity_import_results = self.import_to_unity(
            model_path=model_path,
            rig_path=rig_result.path,
            animation_paths=downloaded,
            character_type=character_type
        )

        return {
            "success": True,
            "rigged_model": rig_result.path,
            "animations": downloaded,
            "unity_prefab": unity_import_results["prefab_path"]
        }
```

---

## 2. Animation Presets

```python
def load_animation_presets(self) -> dict:
    """Pre-defined animation sets for different character types."""
    return {
        "humanoid_enemy": [
            {"name": "Idle", "trim_start": 0, "trim_end": 100},
            {"name": "Walking", "trim_start": 0, "trim_end": 100},
            {"name": "Running", "trim_start": 0, "trim_end": 100},
            {"name": "Attack", "trim_start": 0, "trim_end": 100},
            {"name": "Hit Reaction", "trim_start": 0, "trim_end": 100},
            {"name": "Death", "trim_start": 0, "trim_end": 100},
        ],
        "creature_enemy": [
            {"name": "Creature Idle", "trim_start": 0, "trim_end": 100},
            {"name": "Creature Walk", "trim_start": 0, "trim_end": 100},
            {"name": "Creature Attack", "trim_start": 0, "trim_end": 100},
            {"name": "Creature Death", "trim_start": 0, "trim_end": 100},
        ],
        "fps_arms": [
            {"name": "FPS Idle", "trim_start": 0, "trim_end": 100},
            {"name": "FPS Walk", "trim_start": 0, "trim_end": 100},
            {"name": "FPS Fire", "trim_start": 0, "trim_end": 100},
            {"name": "FPS Reload", "trim_start": 0, "trim_end": 100},
        ]
    }
```

---

## 3. Unity Import Settings

```python
def import_to_unity(self, model_path: str, rig_path: str,
                    animation_paths: list, character_type: str) -> dict:
    """Import rigged model and animations to Unity."""

    import_settings = {
        "model_importer": {
            "animation_type": "Humanoid" if "humanoid" in character_type else "Generic",
            "avatar_definition": "CreateFromThisModel",
            "optimize_game_objects": True
        },
        "animation_importer": {
            "loop_time": True,
            "root_motion": False,
        }
    }

    result = unity_mcp.execute("ImportAnimatedCharacter", {
        "model": rig_path,
        "animations": animation_paths,
        "settings": import_settings,
        "output_prefab": f"Assets/Prefabs/Characters/{character_type}.prefab"
    })

    return result
```

---

## 4. Procedural Animation (Alternative)

For non-humanoid creatures or performance-critical cases:

```csharp
// ProceduralAnimationController.cs
public class ProceduralAnimationController : MonoBehaviour
{
    [Header("IK Targets")]
    [SerializeField] private Transform[] footTargets;
    [SerializeField] private Transform lookTarget;

    [Header("Animation Parameters")]
    [SerializeField] private float bobAmount = 0.1f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float stepHeight = 0.3f;

    private Animator animator;
    private Vector3 velocity;

    void Update()
    {
        // Procedural head bob based on movement
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount * velocity.magnitude;
        transform.localPosition = new Vector3(0, bob, 0);

        // IK foot placement for terrain adaptation
        UpdateFootIK();

        // Look-at for head tracking
        if (lookTarget != null)
        {
            animator.SetLookAtWeight(1f);
            animator.SetLookAtPosition(lookTarget.position);
        }
    }

    void UpdateFootIK()
    {
        foreach (var foot in footTargets)
        {
            if (Physics.Raycast(foot.position + Vector3.up, Vector3.down, out var hit, 2f))
            {
                foot.position = hit.point + Vector3.up * stepHeight;
            }
        }
    }
}
```

---

## References

- [Mixamo](https://www.mixamo.com/)
- [Mixamo Downloader](https://github.com/junglie85/mixamo-downloader)
- [Unity Animation Rigging](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.0/manual/index.html)
- [DeepMotion](https://www.deepmotion.com/)
- [Cascadeur](https://cascadeur.com/)
