# Layer 6: Agent Orchestration (Governance) - Examples

> Code samples for agent roles, convoy system, and safety controls

---

## 1. Agent Roles

| Agent | Responsibility | Persistence | Lifespan |
|-------|---------------|-------------|----------|
| **Mayor** | Orchestrates work, reads GDD, assigns tasks | Full context | Long-running |
| **Script Polecat** | Writes C# scripts | Hook per task | Task duration |
| **Scene Polecat** | Modifies scenes, prefabs, hierarchy | Hook per task | Task duration |
| **UI Polecat** | Creates Canvas, UI elements | Hook per task | Task duration |
| **Asset Polecat** | Generates 3D models, textures, audio via APIs | Hook per task | Task duration |
| **Eyes Polecat** | Observes and reports state | Continuous | Always running |
| **Test Polecat** | Writes and runs automated tests | Hook per suite | Test duration |
| **Evaluator** | Grades outcomes, flags regressions | Per evaluation | Evaluation duration |

---

## 2. The Convoy System

Tasks are grouped into Convoys for coordinated delivery:

```yaml
# convoys/inventory-system.yaml
convoy:
  id: "convoy-inventory-001"
  name: "Inventory System"
  source: "GDD Section 4.2"
  status: "in_progress"

  tasks:
    - id: "inv-001"
      title: "Create InventoryItem ScriptableObject"
      type: "script"
      assigned_to: "script-polecat-1"
      status: "complete"

    - id: "inv-002"
      title: "Create InventoryManager singleton"
      depends_on: ["inv-001"]
      status: "complete"

    - id: "inv-003"
      title: "Create InventoryUI Canvas"
      depends_on: ["inv-002"]
      status: "in_progress"

    - id: "inv-004"
      title: "Wire UI to Manager"
      depends_on: ["inv-002", "inv-003"]
      status: "pending"
      requires_human_verification: true

  completion_criteria:
    - all_tasks_complete: true
    - eval_pass_rate: ">= 0.9"
```

---

## 3. Safety Controls

```yaml
# safety_config.yaml
safety:
  max_iterations_per_task: 50
  max_parallel_runs: 10
  auto_rollback_on_regression: true

  human_approval_required_for:
    - deleting_files
    - modifying_player_data
    - production_deployment

  budget_limits:
    max_tokens_per_task: 500000
    max_api_cost_per_hour: $10
```

**Rule:** Agents never self-approve. Cross-verification is mandatory.

---

## References

- [GasTown Multi-Agent Orchestration](https://github.com/steveyegge/gastown)
