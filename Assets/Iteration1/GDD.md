# Game Design Document: Target Clicker

## Overview

A minimal 3D shooting gallery where the player clicks on targets that appear at random positions. The goal is to hit 10 targets to win. This game serves as the first validation of the Neuro-Engine's Layers 1-5.

## Win/Lose Conditions

- **Win**: Player clicks 10 targets successfully
- **Lose**: None (no fail state for simplicity)

## Core Mechanics

1. **Target Spawning**: Targets appear one at a time at random positions within a defined play area
2. **Target Clicking**: Player clicks on a target with the mouse to "shoot" it
3. **Score Tracking**: Each hit increments the score by 1
4. **Target Destruction**: When clicked, the target is destroyed and a new one spawns
5. **Win State**: When score reaches 10, display "You Win!" and stop spawning

## Controls

| Input | Action |
|-------|--------|
| Left Mouse Click | Shoot (raycast from camera to click point) |

## UI Elements

- **Score Display**: Text showing "Score: X / 10" (UI Toolkit)
- **Win Message**: "You Win!" panel shown when score = 10

## Scene Requirements

- **Camera**: Main Camera looking at play area
- **Light**: Directional light for visibility
- **Play Area**: Invisible bounds where targets can spawn (e.g., 10x10 unit area at z=5)
- **Targets**: Red cube primitives (1x1x1 unit)

## Architecture Requirements (Layer 1)

### Services (VContainer DI)

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| ScoreService | IScoreService | Track score, detect win |
| TargetSpawnerService | ITargetSpawnerService | Spawn/destroy targets |
| GameStateService | IGameStateService | Expose game state as JSON |

### VContainer Lifetime Scope

- `Iteration1LifetimeScope.cs` - registers all services
- Must be in scene for DI to work

### UI (UI Toolkit)

- `GameHUD.uxml` - Score display
- `GameHUD.uss` - Styling (optional)

## Testable Success Criteria

These criteria map to specific Layer capabilities:

### Layer 1: Code-First
- [ ] All services registered via VContainer (DI works)
- [ ] UI displays correctly via UI Toolkit
- [ ] Game state serializable to JSON

### Layer 2: Observation
- [ ] `CaptureWorldState` returns target position
- [ ] `GetUIState` returns current score value
- [ ] `AnalyzeSpatial` detects target in play area bounds
- [ ] `ScanMissingReferences` returns zero issues

### Layer 3: Interaction
- [ ] `SimulateInput` click at target position destroys target
- [ ] AI can play through full game via input simulation

### Layer 4: Persistence
- [ ] Game state snapshots saved to `hooks/iterations/Iteration1/`
- [ ] Transcript logs all state changes

### Layer 5: Evaluation
- [ ] Syntactic: Zero compilation errors
- [ ] State: Assert `score.Value == previousScore + 1` after click
- [ ] State: Assert `gameState == "Won"` when `score >= 10`

## Out of Scope

- Sound effects
- Particle effects
- Multiple difficulty levels
- Timer/time pressure
- Miss tracking
- Animations
- Menu screens
- Pause functionality

## Technical Constraints

- Unity 6+ (URP not required, built-in RP is fine)
- VContainer for DI
- UI Toolkit for UI (no UGUI)
- No Inspector-serialized references where DI can be used

## File Structure

```
Assets/Iteration1/
├── GDD.md                          # This file
├── Scripts/
│   ├── Services/
│   │   ├── IScoreService.cs
│   │   ├── ScoreService.cs
│   │   ├── ITargetSpawnerService.cs
│   │   ├── TargetSpawnerService.cs
│   │   ├── IGameStateService.cs
│   │   └── GameStateService.cs
│   ├── Components/
│   │   ├── Target.cs               # Click handler on target
│   │   ├── TargetClicker.cs        # Raycast input handler
│   │   └── GameHUDController.cs    # Binds UI to services
│   └── Iteration1LifetimeScope.cs  # VContainer registrations
├── Scenes/
│   └── Iteration1Scene.unity
├── Prefabs/
│   └── Target.prefab               # Red cube with Target component
└── UI/
    ├── GameHUD.uxml
    └── GameHUD.uss
```

## State Schema

The game state (for Layer 2 observation and Layer 4 persistence):

```json
{
  "iteration": "Iteration1",
  "gameName": "TargetClicker",
  "state": {
    "score": 0,
    "targetCount": 10,
    "gameState": "Playing",
    "currentTargetPosition": { "x": 0, "y": 0, "z": 5 }
  }
}
```

`gameState` values: `"Playing"`, `"Won"`

## Acceptance Test (End-to-End)

```
1. Load Iteration1Scene
2. Assert: Score displays "Score: 0 / 10"
3. Assert: One target exists in scene
4. Get target world position via CaptureWorldState
5. SimulateInput click at target position
6. Assert: Target destroyed
7. Assert: Score now "Score: 1 / 10"
8. Assert: New target spawned
9. Repeat steps 4-8 nine more times
10. Assert: "You Win!" message displayed
11. Assert: No new targets spawn
```
