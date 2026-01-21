# Layer 3: Interaction System (Hands) - Examples

> Code samples for Selenium-for-Unity, input simulation, and headless execution

---

## 1. Selenium-for-Unity API

### AI-Driven Playtest Script

```python
from unity_driver import UnityDriver, By, Keys

driver = UnityDriver.connect("localhost:9999")

# Find and click UI elements
button = driver.find_element(By.NAME, "StartButton")
assert button.is_visible(), "Button not visible"
assert not button.is_blocked(), f"Button blocked by {button.blocked_by}"
button.click()

# Wait for scene transition
driver.wait_until(lambda: driver.current_scene == "Gameplay", timeout=5)

# Simulate gameplay input
driver.press_key(Keys.SPACE)  # Jump
driver.wait_for_frames(10)

# Query game state
player_y = driver.query("/player/transform/position/y")
assert float(player_y) > 1.0, "Jump did not elevate player"
```

---

## 2. Integration Options

| Tool | Strength | Use Case |
|------|----------|----------|
| [GameDriver](https://www.gamedriver.io/) | Full automation API, Claude integration | Production testing |
| [AltTester](https://alttester.com/) | Open source, UI focus | UI flow verification |
| [Unity-MCP](https://github.com/CoplayDev/unity-mcp) | MCP protocol native | Direct AI agent control |
| Custom HTTP Server | Maximum control | Bespoke implementations |

---

## 3. Headless Execution

### Linux with Virtual Framebuffer

```bash
# Linux with virtual framebuffer for rendering
Xvfb :99 -screen 0 1920x1080x24 &
export DISPLAY=:99
Unity -batchmode -executeMethod AutoTester.Run -logFile build.log
```

### Docker Container Setup

```dockerfile
FROM unityci/editor:ubuntu-2022.3.0f1

# Install virtual display
RUN apt-get update && apt-get install -y xvfb

# Run with virtual framebuffer
CMD ["xvfb-run", "--auto-servernum", "unity", "-batchmode", "-runTests"]
```

---

## References

- [GameDriver](https://www.gamedriver.io/)
- [AltTester](https://alttester.com/)
- [Unity-MCP](https://github.com/CoplayDev/unity-mcp)
- [GameCI (Docker for Unity)](https://game.ci/)
- [Unity Headless Mode](https://docs.unity3d.com/6000.3/Documentation/Manual/desktop-headless-mode.html)
