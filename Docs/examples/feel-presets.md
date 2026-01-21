# Feel Presets Library - Examples

> Reference game parameters and genetic optimization for game feel

---

## 1. Reference Game Presets

```yaml
# feel_presets.yaml
presets:
  doom_1993:
    description: "Original Doom - fast, floaty, powerful"
    movement:
      max_speed: 23.0         # Units/sec (Doom was FAST)
      acceleration: 85.0
      deceleration: 85.0       # Instant stop
      air_control: 0.25
      gravity: 35.0
      jump_force: 0            # No jump in original
    camera:
      fov: 90
      bob_intensity: 0.02
      bob_speed: 10
      view_height: 1.6
    combat:
      weapon_bob: 0.015
      screen_shake_intensity: 3.0
      screen_shake_duration: 0.1
      hitstop_duration: 0
      muzzle_flash_duration: 0.05

  quake_1996:
    description: "Quake - tight, bunny-hop enabled"
    movement:
      max_speed: 14.0
      acceleration: 100.0
      deceleration: 100.0
      air_control: 0.7          # High for bunny hopping
      gravity: 32.0
      jump_force: 8.5
    camera:
      fov: 90
      bob_intensity: 0.01
      bob_speed: 8
      view_height: 1.7
    combat:
      weapon_bob: 0.01
      screen_shake_intensity: 5.0
      screen_shake_duration: 0.15
      hitstop_duration: 0.02
      muzzle_flash_duration: 0.04

  modern_boomer:
    description: "DUSK/Ultrakill style - extremely fast, responsive"
    movement:
      max_speed: 28.0
      acceleration: 150.0
      deceleration: 150.0
      air_control: 0.85
      gravity: 40.0
      jump_force: 10.0
      slide_speed_boost: 1.5
      wall_jump: true
    camera:
      fov: 100
      bob_intensity: 0.008
      bob_speed: 12
      view_height: 1.6
      fov_speed_scaling: true
    combat:
      weapon_bob: 0.005
      screen_shake_intensity: 8.0
      screen_shake_duration: 0.08
      hitstop_duration: 0.03
      muzzle_flash_duration: 0.03
      time_scale_on_kill: 0.8

  call_of_duty:
    description: "Modern military - weighty, realistic"
    movement:
      max_speed: 6.0
      acceleration: 25.0
      deceleration: 30.0
      air_control: 0.1
      gravity: 20.0
      jump_force: 5.0
      sprint_multiplier: 1.5
    camera:
      fov: 65
      bob_intensity: 0.025
      bob_speed: 6
      view_height: 1.8
      ads_fov: 45
    combat:
      weapon_bob: 0.02
      screen_shake_intensity: 2.0
      screen_shake_duration: 0.2
      hitstop_duration: 0
      muzzle_flash_duration: 0.03
      recoil_pattern: true
```

---

## 2. Feel Preset Applicator (C#)

```csharp
// FeelPresetApplicator.cs
[CreateAssetMenu(fileName = "FeelPreset", menuName = "Game/Feel Preset")]
public class FeelPreset : ScriptableObject
{
    [Header("Movement")]
    public float maxSpeed = 14f;
    public float acceleration = 100f;
    public float deceleration = 100f;
    public float airControl = 0.5f;
    public float gravity = 30f;
    public float jumpForce = 8f;

    [Header("Camera")]
    public float fov = 90f;
    public float bobIntensity = 0.01f;
    public float bobSpeed = 10f;
    public float viewHeight = 1.7f;

    [Header("Combat")]
    public float weaponBob = 0.01f;
    public float screenShakeIntensity = 5f;
    public float screenShakeDuration = 0.1f;
    public float hitstopDuration = 0.02f;

    public void ApplyToCharacter(PlayerController player, CameraController camera, CombatFeedback combat)
    {
        // Movement
        player.MaxSpeed = maxSpeed;
        player.Acceleration = acceleration;
        player.Deceleration = deceleration;
        player.AirControl = airControl;
        player.Gravity = gravity;
        player.JumpForce = jumpForce;

        // Camera
        camera.BaseFOV = fov;
        camera.BobIntensity = bobIntensity;
        camera.BobSpeed = bobSpeed;
        camera.ViewHeight = viewHeight;

        // Combat
        combat.WeaponBob = weaponBob;
        combat.ScreenShakeIntensity = screenShakeIntensity;
        combat.ScreenShakeDuration = screenShakeDuration;
        combat.HitstopDuration = hitstopDuration;
    }
}
```

---

## 3. Genetic Optimization for Feel

```python
class FeelOptimizer:
    """Uses genetic algorithms + VLM feedback to optimize feel."""

    def __init__(self, target_description: str, base_preset: dict):
        self.target = target_description
        self.base = base_preset
        self.population_size = 20
        self.generations = 50

    async def optimize(self) -> dict:
        """Evolve parameters toward target feel."""

        population = self.initialize_population()

        for gen in range(self.generations):
            # Evaluate each individual
            fitness_scores = []
            for individual in population:
                score = await self.evaluate_feel(individual)
                fitness_scores.append(score)

            # Select best performers
            sorted_pop = sorted(zip(population, fitness_scores),
                               key=lambda x: x[1], reverse=True)

            # Check if we've reached target
            if sorted_pop[0][1] >= 8.5:
                return sorted_pop[0][0]

            # Evolve next generation
            population = self.evolve(sorted_pop)

        return sorted_pop[0][0]

    async def evaluate_feel(self, parameters: dict) -> float:
        """Apply parameters, record gameplay, have VLM rate the feel."""

        apply_feel_parameters(parameters)
        video = record_gameplay(duration=30)

        result = await gemini.analyze_video(
            video=video,
            prompt=f"""
            TARGET FEEL: {self.target}

            Watch this gameplay and rate how well it matches.
            Consider: movement responsiveness, combat impact, overall feel.
            Rate 1-10 where 10 = perfectly matches target feel.
            """
        )

        return result["score"]

    def evolve(self, sorted_population: list) -> list:
        """Create next generation through selection, crossover, mutation."""
        new_pop = []

        # Elitism: keep top 2
        new_pop.append(sorted_population[0][0])
        new_pop.append(sorted_population[1][0])

        # Crossover and mutation for rest
        while len(new_pop) < self.population_size:
            parent1 = self.tournament_select(sorted_population)
            parent2 = self.tournament_select(sorted_population)
            child = self.crossover(parent1, parent2)
            child = self.mutate(child)
            new_pop.append(child)

        return new_pop

    def mutate(self, individual: dict, mutation_rate: float = 0.1) -> dict:
        """Randomly adjust parameters slightly."""
        mutated = individual.copy()
        for key, value in mutated.items():
            if random.random() < mutation_rate:
                mutated[key] = value * random.uniform(0.8, 1.2)
        return mutated
```

---

## References

- [Doom Wiki - Player movement](https://doom.fandom.com/wiki/Player)
- [Quake Wiki - Movement physics](https://quakewiki.org/wiki/Movement)
