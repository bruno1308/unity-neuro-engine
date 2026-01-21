# Skill: unity-package

## Purpose
Add, remove, or verify Unity packages in the project manifest.

## When to Use
- Installing new dependencies (VContainer, Unity-MCP, etc.)
- Verifying package installation status
- Removing unused packages

## Context

### Known Problems (from ENGINE_PROBLEMS.md)
**Problem #2: Package Name Mismatch** - The manifest key MUST match the `name` field in the package's package.json. Never guess package names.

### Critical Rule
BEFORE adding any git-based package:
1. Fetch the package.json from the repository
2. Extract the exact `name` field
3. Use that exact name as the manifest key

### Package Locations
- Manifest: `Packages/manifest.json`
- Lock file: `Packages/packages-lock.json` (auto-generated)

### Common Packages
| Package | Manifest Key | URL |
|---------|-------------|-----|
| VContainer | `jp.hadashikick.vcontainer` | `https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer` |
| Unity-MCP | `com.coplaydev.unity-mcp` | `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity` |

## Procedure

### Adding a Package

1. **Verify package name first:**
```
WebFetch: https://raw.githubusercontent.com/{owner}/{repo}/main/{path}/package.json
Extract: "name" field
```

2. **Read current manifest:**
```
Read: Packages/manifest.json
```

3. **Add to dependencies (alphabetical order):**
```json
"exact-package-name": "git-url-or-version"
```

4. **Note for user:** Unity will import on next focus. Errors only visible in Unity console.

### Verifying Installation

1. Check manifest.json contains the package
2. Check Library/PackageCache/ for the package folder (after Unity imports)

### Removing a Package

1. Remove line from manifest.json
2. Unity will remove on next refresh

## Verification
- Package appears in manifest.json with correct name
- No "name mismatch" errors (user must confirm from Unity console)
- Package visible in Window > Package Manager
