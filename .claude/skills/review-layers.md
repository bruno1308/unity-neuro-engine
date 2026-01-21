# Skill: review-layers

## Purpose
Invoke the code-reviewer-layers agent to review changes to engine infrastructure.

## When to Use
- Before committing changes to `Packages/com.neuroengine.core/`
- During PR review of engine code
- When architectural validation is needed
- Before merging any layer-touching code

## Context

### Protected Paths
Any changes to these paths **require** this review:
```
Packages/com.neuroengine.core/Runtime/Core/
Packages/com.neuroengine.core/Runtime/Services/
Packages/com.neuroengine.core/Editor/
Packages/com.neuroengine.core/Tests/
```

### The 7 Layers
| Layer | Name | Key Files |
|-------|------|-----------|
| 1 | Code-First Foundation | NeuroEngineLifetimeScope, *Service.cs |
| 2 | Observation System | SceneStateCapture, *Detector, *Analysis |
| 3 | Interaction System | InputSimulationService |
| 4 | Persistent Artifacts | HooksWriter, TranscriptWriter, TaskManager |
| 5 | Evaluation Framework | ValidationRulesEngine |
| 6 | Agent Orchestration | .claude/agents/* |
| 7 | Generative Pipeline | Asset generation, style enforcement |

## Procedure

### 1. Identify Changed Files
```bash
# For uncommitted changes
git diff --name-only

# For staged changes
git diff --cached --name-only

# For PR review
git diff main...HEAD --name-only
```

### 2. Filter to Protected Paths
```bash
git diff --name-only | grep "Packages/com.neuroengine.core/"
```

### 3. Determine Affected Layers
Map each file to its layer(s):
- `Runtime/Core/I*.cs` → Interface contracts (all layers)
- `Runtime/Services/*Service.cs` → Implementation (specific layer)
- `Tests/Layer{N}/*` → Test coverage (Layer N)

### 4. Run Layer-Specific Checks

**Layer 1 - Dependency Injection**:
```bash
# Check for anti-patterns
grep -rn "GetComponent<" Packages/com.neuroengine.core/
grep -rn "FindObjectOfType<" Packages/com.neuroengine.core/
grep -rn "\[SerializeField\].*Service" Packages/com.neuroengine.core/
```

**Layer 2 - Observation Purity**:
```bash
# Check observation code doesn't mutate
grep -rn "SetActive\|Destroy\|Instantiate" Packages/com.neuroengine.core/Runtime/Services/*Capture*.cs
grep -rn "SetActive\|Destroy\|Instantiate" Packages/com.neuroengine.core/Runtime/Services/*Detector*.cs
```

**Layer 3 - Input State Tracking**:
```bash
# Verify held keys tracked
grep -rn "_heldKeys" Packages/com.neuroengine.core/Runtime/Services/InputSimulationService.cs
```

**Layer 4 - Atomic Writes**:
```bash
# Check for atomic write pattern
grep -rn "\.tmp\|File\.Move" Packages/com.neuroengine.core/Runtime/Services/*Writer*.cs
```

### 5. Generate Review Report

Write to: `hooks/reviews/layer-review-{timestamp}.json`

```json
{
  "reviewer": "code-reviewer-layers",
  "timestamp": "2026-01-21T12:00:00Z",
  "files_reviewed": [
    "Packages/com.neuroengine.core/Runtime/Services/NewService.cs"
  ],
  "layers_affected": [1, 2],
  "verdict": "APPROVED|CHANGES_REQUESTED|BLOCKED",
  "findings": [
    {
      "severity": "error|warning|suggestion",
      "layer": 1,
      "file": "path/to/file.cs",
      "line": 42,
      "rule": "rule-id",
      "message": "Description",
      "suggestion": "How to fix"
    }
  ],
  "approval_conditions": []
}
```

### 6. Verdict Criteria

**APPROVED** (can merge):
- No error-severity findings
- All affected layers pass checklist
- Test coverage adequate

**CHANGES_REQUESTED** (needs fixes):
- Warning-severity findings
- Missing tests
- Minor pattern violations

**BLOCKED** (cannot merge):
- Error-severity findings
- Interface contract broken
- Security issues
- Architectural regression

## Quick Commands

```bash
# Full layer review of staged changes
git diff --cached --name-only | grep "neuroengine" && echo "REVIEW REQUIRED"

# Check for common anti-patterns
grep -rn "GetComponent<\|FindObjectOfType<" Packages/com.neuroengine.core/Runtime/

# Verify all services have interfaces
for f in Packages/com.neuroengine.core/Runtime/Services/*Service.cs; do
  class=$(basename "$f" .cs)
  interface="I${class%Service}"
  grep -l "$interface" Packages/com.neuroengine.core/Runtime/Core/ || echo "Missing interface: $interface"
done
```

## Verification
- All affected layers reviewed
- Review report written to hooks/reviews/
- Verdict clearly stated
- Findings actionable
