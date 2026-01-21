# Unity Package Specification: com.neuroengine.setup

## Overview

A Unity Editor package that provides a graphical setup wizard for Neuro-Engine Protocol projects.

**Repository:** `github.com/[your-org]/neuroengine-setup`
**Package Name:** `com.neuroengine.setup`
**Unity Version:** 2021.3 LTS+

---

## Features

### 1. Setup Wizard Window

**Location:** Window → Neuro-Engine → Setup Wizard

**UI Layout:**
```
┌─────────────────────────────────────────────────────────────┐
│  🧠 Neuro-Engine Setup Wizard                          [X]  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Step 1: Prerequisites                          [✓] [⟳]    │
│  ├─ Node.js 18+                                 ✓ v20.0.0  │
│  ├─ Python 3.10+                                ✓ v3.13.9  │
│  └─ Git 2.25+                                   ✓ v2.40.0  │
│                                                             │
│  Step 2: Unity Packages                         [⏳] [⟳]   │
│  ├─ VContainer                                  ✓ Installed │
│  └─ Unity-MCP                                   ⏳ Installing│
│                                                             │
│  Step 3: API Configuration                      [ ] [Edit]  │
│  ├─ Meshy.ai                                    ✗ Not set   │
│  ├─ ElevenLabs                                  ✗ Not set   │
│  └─ Google Gemini                               ✗ Not set   │
│                                                             │
│  Step 4: Project Structure                      [ ] [Create]│
│  ├─ hooks/ directory                            ✗ Missing   │
│  ├─ CLAUDE.md                                   ✗ Missing   │
│  └─ .env file                                   ✗ Missing   │
│                                                             │
│  Step 5: Connection Test                        [ ] [Test]  │
│  └─ MCP Server                                  ⏳ Not tested│
│                                                             │
├─────────────────────────────────────────────────────────────┤
│  [← Back]                              [Run All] [Next →]   │
└─────────────────────────────────────────────────────────────┘
```

### 2. Prerequisite Checker

**Class:** `PrerequisiteChecker.cs`

```csharp
public class PrerequisiteChecker
{
    public PrerequisiteResult CheckNodeJs();    // Runs: node --version
    public PrerequisiteResult CheckPython();    // Runs: python --version
    public PrerequisiteResult CheckGit();       // Runs: git --version

    public struct PrerequisiteResult
    {
        public bool IsInstalled;
        public string Version;
        public string Error;
        public string InstallUrl;
    }
}
```

### 3. Package Installer

**Class:** `PackageInstaller.cs`

```csharp
public class PackageInstaller
{
    public async Task InstallVContainer();
    public async Task InstallUnityMCP();

    public bool IsPackageInstalled(string packageName);
    public string GetPackageVersion(string packageName);
}
```

**Package URLs:**
- VContainer: `https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer`
- Unity-MCP: `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity`

### 4. API Key Configuration UI

**Class:** `ApiKeyConfigWindow.cs`

**UI:**
```
┌─────────────────────────────────────────────────────────────┐
│  🔑 API Key Configuration                              [X]  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Meshy.ai (3D Model Generation)                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ ●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●          │   │
│  └─────────────────────────────────────────────────────┘   │
│  [Get Key](https://www.meshy.ai/api)           [Test] [✓]   │
│                                                             │
│  ElevenLabs (Audio Generation)                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ ●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●●          │   │
│  └─────────────────────────────────────────────────────┘   │
│  [Get Key](https://elevenlabs.io/api)          [Test] [✓]   │
│                                                             │
│  Google Gemini (Video Analysis)                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │                                                     │   │
│  └─────────────────────────────────────────────────────┘   │
│  [Get Key](https://ai.google.dev/)             [Test] [✗]   │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                            [Save to .env]   │
└─────────────────────────────────────────────────────────────┘
```

**Features:**
- Password-masked input fields
- "Get Key" links open browser to API portals
- "Test" button validates key with simple API call
- Saves to `.env` file in project root

### 5. Project Structure Generator

**Class:** `ProjectStructureGenerator.cs`

```csharp
public class ProjectStructureGenerator
{
    public void CreateHooksDirectory();
    public void CreateClaudeDirectory();
    public void CreateEnvFile();
    public void CreateClaudeMd();
    public void CreateGitignore();

    public bool ValidateStructure(); // Returns true if all required files exist
}
```

**Generated Files:**
- `hooks/` directory with subdirectories
- `.claude/settings.json`
- `.claude/mcp.json`
- `.env` (from template)
- `.env.template`
- `.gitignore`
- `CLAUDE.md`
- `Docs/` with Wizard.md and Architecture.md

### 6. Connection Tester

**Class:** `ConnectionTester.cs`

```csharp
public class ConnectionTester
{
    public async Task<ConnectionResult> TestMcpServer();
    public async Task<ConnectionResult> TestMeshyApi(string apiKey);
    public async Task<ConnectionResult> TestElevenLabsApi(string apiKey);
    public async Task<ConnectionResult> TestGeminiApi(string apiKey);

    public struct ConnectionResult
    {
        public bool Success;
        public string Message;
        public float LatencyMs;
    }
}
```

---

## File Structure

```
com.neuroengine.setup/
├── package.json
├── README.md
├── CHANGELOG.md
├── LICENSE
├── Editor/
│   ├── NeuroEngineSetup.asmdef
│   ├── Windows/
│   │   ├── SetupWizardWindow.cs
│   │   └── ApiKeyConfigWindow.cs
│   ├── Core/
│   │   ├── PrerequisiteChecker.cs
│   │   ├── PackageInstaller.cs
│   │   ├── ProjectStructureGenerator.cs
│   │   └── ConnectionTester.cs
│   ├── Utils/
│   │   ├── ProcessRunner.cs        # Run shell commands
│   │   ├── EnvFileHandler.cs       # Read/write .env
│   │   └── EditorHttpClient.cs     # HTTP requests in editor
│   └── Resources/
│       ├── SetupWizardStyles.uss
│       └── Icons/
└── Templates/
    ├── env.template
    ├── gitignore.template
    ├── CLAUDE.md.template
    └── claude_settings.json.template
```

---

## package.json

```json
{
  "name": "com.neuroengine.setup",
  "version": "0.1.0",
  "displayName": "Neuro-Engine Setup",
  "description": "Setup wizard for Neuro-Engine Protocol projects",
  "unity": "2021.3",
  "documentationUrl": "https://github.com/[org]/neuroengine-setup",
  "changelogUrl": "https://github.com/[org]/neuroengine-setup/blob/main/CHANGELOG.md",
  "licensesUrl": "https://github.com/[org]/neuroengine-setup/blob/main/LICENSE",
  "keywords": [
    "ai",
    "automation",
    "neuro-engine",
    "setup",
    "wizard"
  ],
  "author": {
    "name": "Neuro-Engine Team"
  },
  "dependencies": {}
}
```

---

## Usage

### For New Projects

1. Create new Unity project
2. Add package: `https://github.com/[org]/neuroengine-setup.git`
3. Open: Window → Neuro-Engine → Setup Wizard
4. Follow wizard steps
5. Done - project ready for AI development

### For Existing Projects

1. Add package via git URL
2. Run wizard
3. Wizard detects existing config and offers to update/skip

---

## API Key Testing

### Meshy.ai Test

```csharp
// Simple API call to validate key
var response = await httpClient.GetAsync(
    "https://api.meshy.ai/v2/text-to-3d",
    headers: { "Authorization": $"Bearer {apiKey}" }
);
return response.StatusCode != 401;
```

### ElevenLabs Test

```csharp
var response = await httpClient.GetAsync(
    "https://api.elevenlabs.io/v1/user",
    headers: { "xi-api-key": apiKey }
);
return response.StatusCode == 200;
```

### Gemini Test

```csharp
var response = await httpClient.GetAsync(
    $"https://generativelanguage.googleapis.com/v1/models?key={apiKey}"
);
return response.StatusCode == 200;
```

---

## Future Enhancements

- [ ] Auto-update checker for the setup package itself
- [ ] Project health dashboard (show hooks status, API quotas)
- [ ] One-click GDD template generator
- [ ] Integration with Claude Code status bar
- [ ] Backup/restore project configuration
