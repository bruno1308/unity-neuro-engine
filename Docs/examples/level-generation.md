# Level Generation System - Examples

> Code samples for graph-based levels, Wave Function Collapse, and VLM review

---

## 1. Level as Graph (AI-Friendly)

```yaml
# level_graph.yaml - AI can generate and reason about this
level:
  name: "E1M1_Hangar"
  theme: "tech_base"
  difficulty: "easy"
  estimated_playtime: "5 minutes"

  rooms:
    - id: "start"
      type: "spawn_room"
      size: "small"  # 10x10 units
      connections: ["hallway_1"]
      enemies: []
      items: ["shotgun"]
      lighting: "bright"

    - id: "hallway_1"
      type: "corridor"
      size: "medium_long"  # 5x20 units
      connections: ["start", "arena_1"]
      enemies: [{"type": "imp", "count": 2, "placement": "ambush"}]
      items: ["ammo_small"]
      lighting: "dim"

    - id: "arena_1"
      type: "combat_arena"
      size: "large"  # 25x25 units
      connections: ["hallway_1", "secret_1", "hallway_2"]
      enemies:
        - {"type": "imp", "count": 3, "placement": "scattered"}
        - {"type": "demon", "count": 2, "placement": "central"}
      items: ["health_large", "armor_green"]
      cover_points: 4
      lighting: "dramatic"
      set_pieces: ["pillar_cluster", "crate_cover"]

    - id: "secret_1"
      type: "secret_room"
      size: "tiny"
      connections: ["arena_1"]
      hidden: true
      trigger: "wall_push"
      items: ["megahealth", "ammo_large"]
      lighting: "eerie"

    - id: "exit"
      type: "exit_room"
      size: "small"
      connections: ["hallway_2"]
      trigger: "level_complete"

  flow:
    critical_path: ["start", "hallway_1", "arena_1", "hallway_2", "exit"]
    pacing_curve: ["calm", "tension", "combat_peak", "cooldown", "exit"]
    backtracking_allowed: true

  constraints:
    max_enemies_visible: 5
    min_cover_in_arenas: 2
    secret_ratio: 0.15
```

---

## 2. Graph-to-Geometry Pipeline

```python
class LevelGenerator:
    """Converts AI-generated level graph to Unity geometry."""

    def __init__(self, level_kit_path: str):
        self.level_kit = self.load_level_kit(level_kit_path)
        self.wfc_solver = WaveFunctionCollapse()

    def generate_level(self, level_graph: dict) -> str:
        """Full pipeline: graph → geometry → Unity scene."""

        # Step 1: Generate room layouts
        room_layouts = {}
        for room in level_graph["rooms"]:
            layout = self.generate_room_layout(room)
            room_layouts[room["id"]] = layout

        # Step 2: Connect rooms with corridors
        connections = self.generate_connections(level_graph, room_layouts)

        # Step 3: Apply WFC for detail geometry
        detailed_geometry = self.apply_wfc_details(room_layouts, connections)

        # Step 4: Place entities (enemies, items)
        entity_placements = self.place_entities(level_graph, detailed_geometry)

        # Step 5: Apply lighting
        lighting = self.generate_lighting(level_graph, detailed_geometry)

        # Step 6: Export to Unity scene
        scene_path = self.export_to_unity(
            detailed_geometry,
            entity_placements,
            lighting,
            level_graph["name"]
        )

        return scene_path

    def generate_room_layout(self, room: dict) -> RoomLayout:
        """Generate room geometry based on type and size."""
        room_type = room["type"]
        size = self.parse_size(room["size"])

        template = self.level_kit.get_template(room_type)

        layout = template.instantiate(
            width=size[0],
            height=size[1],
            door_positions=self.calculate_door_positions(room),
            cover_points=room.get("cover_points", 0),
            set_pieces=room.get("set_pieces", [])
        )

        return layout

    def apply_wfc_details(self, rooms: dict, connections: list) -> DetailedGeometry:
        """Use Wave Function Collapse to add consistent detail."""
        rules = self.level_kit.get_wfc_rules()

        for room_id, layout in rooms.items():
            self.wfc_solver.solve(
                grid=layout.detail_grid,
                rules=rules,
                constraints=layout.constraints
            )

        return DetailedGeometry(rooms, connections)

    def place_entities(self, graph: dict, geometry: DetailedGeometry) -> list:
        """Place enemies and items according to graph spec."""
        placements = []

        for room in graph["rooms"]:
            room_geo = geometry.rooms[room["id"]]

            # Place enemies
            for enemy_spec in room.get("enemies", []):
                positions = self.calculate_enemy_positions(
                    room_geo,
                    enemy_spec["count"],
                    enemy_spec["placement"]
                )
                for pos in positions:
                    placements.append({
                        "type": "enemy",
                        "prefab": f"Enemies/{enemy_spec['type']}",
                        "position": pos,
                        "room": room["id"]
                    })

            # Place items
            for item in room.get("items", []):
                pos = self.calculate_item_position(room_geo, item)
                placements.append({
                    "type": "item",
                    "prefab": f"Items/{item}",
                    "position": pos,
                    "room": room["id"]
                })

        return placements
```

---

## 3. VLM Level Review

```python
async def review_generated_level(level_path: str, level_graph: dict) -> dict:
    """Use VLMs to evaluate generated level quality."""

    # Capture top-down view
    top_down = capture_level_topdown(level_path)

    # Capture first-person walkthrough video
    walkthrough = record_level_walkthrough(level_path, duration=60)

    # Claude: Static layout review
    layout_review = await claude.analyze_image(
        image=top_down,
        prompt=f"""
        This is a top-down view of a generated FPS level.

        INTENDED DESIGN:
        {yaml.dump(level_graph)}

        EVALUATE:
        1. Does the layout match the intended room connections?
        2. Are there clear sight lines for combat?
        3. Is there adequate cover in arena rooms?
        4. Are secret areas hidden but findable?
        5. Does the flow guide players naturally?

        Rate 1-10 and suggest improvements.
        """
    )

    # Gemini: Walkthrough review
    walkthrough_review = await gemini.analyze_video(
        video=walkthrough,
        prompt=f"""
        Watch this walkthrough of a generated FPS level.

        EVALUATE:
        1. Does navigation feel natural?
        2. Are combat encounters well-paced?
        3. Is the lighting appropriate for the mood?
        4. Any visual issues (z-fighting, gaps, floating objects)?
        5. Does it feel like a cohesive, designed space?

        Rate 1-10 and note specific timestamps with issues.
        """
    )

    return {
        "layout_score": layout_review["score"],
        "walkthrough_score": walkthrough_review["score"],
        "combined_score": (layout_review["score"] + walkthrough_review["score"]) / 2,
        "issues": layout_review["issues"] + walkthrough_review["issues"],
        "approved": (layout_review["score"] >= 7 and walkthrough_review["score"] >= 7)
    }
```

---

## References

- [Wave Function Collapse](https://github.com/mxgmn/WaveFunctionCollapse)
- [WFC Unity Implementation](https://github.com/selfsame/unity-wave-function-collapse)
- [Unity ProBuilder](https://docs.unity3d.com/Packages/com.unity.probuilder@5.0/manual/index.html)
- [Edgar for Unity](https://github.com/OndrejNepozitek/Edgar-Unity)
