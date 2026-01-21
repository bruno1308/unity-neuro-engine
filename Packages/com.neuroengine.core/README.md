# Neuro-Engine Core

Core infrastructure package for autonomous AI-driven Unity game development.

## Installation

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.neuroengine.core": "https://github.com/your-org/neuroengine-core.git"
  }
}
```

Or copy the `com.neuroengine.core` folder to your project's `Packages/` directory.

## Features

### Layer 1: Configuration
- `IEnvConfig` / `EnvConfigService` - Read API keys from `.env` file

### Layer 2: Observation
- `ISceneStateCapture` / `SceneStateCaptureService` - Capture scene hierarchy as JSON
- `IStateProvider` - Interface for queryable state

### Layer 4: Persistence
- `IHooksWriter` / `HooksWriterService` - Write to `hooks/` directory

### Layer 7: Generative Assets
- `IMeshyClient` / `MeshyClientService` - Meshy.ai 3D generation
- `IElevenLabsClient` / `ElevenLabsClientService` - ElevenLabs audio generation

## Usage

1. Add `NeuroEngineLifetimeScope` component to a GameObject in your scene
2. Inject services via VContainer:

```csharp
public class MyService
{
    private readonly ISceneStateCapture _capture;
    private readonly IHooksWriter _hooks;

    public MyService(ISceneStateCapture capture, IHooksWriter hooks)
    {
        _capture = capture;
        _hooks = hooks;
    }

    public async Task CaptureAndSave()
    {
        var snapshot = _capture.CaptureScene();
        await _hooks.WriteAsync("scenes", "snapshot.json", snapshot);
    }
}
```

## Dependencies

- VContainer (included)
- Unity 2021.3+

## Documentation

See `Docs/Final.md` in the project root for full architecture documentation.
