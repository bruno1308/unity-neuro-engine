# ENGINE_PROBLEMS.md

Problems encountered during engine development that required manual user intervention.
Each entry documents: what happened, why it was a problem, and how to prevent it.

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

## Blocker Issues

For blockers encountered during game iterations, create GitHub issues instead of adding here.
Use the template in `Docs/WORKFLOW.md` section 3.

Issues should be labeled:
- `blocker`
- `layer-{N}` (1-7)
- `iteration-{N}`
