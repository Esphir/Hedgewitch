# Vacuum system

A physics-driven suction mechanic in the spirit of the Poltergust. Objects are pulled by real
forces every physics step — nothing is teleported, nothing is deleted by a raycast — and they keep
their normal collision behaviour the whole time they are in the airflow.

## Architecture

Four independent systems, orchestrated by `VacuumController`:

| System | Script | Responsibility |
| --- | --- | --- |
| Nozzle | `Core/VacuumNozzle.cs` | Origin and direction of the airflow. Aims independently of the body carrying it, with pitch limits and turn speed. |
| Detection | `Core/VacuumDetector.cs` | Overlap-sphere broadphase, then separate horizontal/vertical cone angles plus a near-field intake, then a line-of-sight ray. |
| Suction force | `Core/VacuumSuctionField.cs` | Distance and cone attenuation, mass advantage, resistance, spool. Hands each target a context; applies nothing itself. |
| Capture | `Core/VacuumCaptureSystem.cs` | What happens at the mouth. Drives the swallow motion; the object's handlers decide the outcome. |

The controller only ever talks to `IVacuumAffectable` and `IVacuumCaptureHandler`, so a new kind of
vacuumable thing never requires a change to the core.

```
VacuumController (FixedUpdate)
  ├── spool intensity toward 0/1
  ├── VacuumDetector.Scan           every rescanInterval (default 50 ms)
  ├── VacuumDetector.Evaluate       every physics step, for already-tracked targets
  ├── VacuumSuctionField.Apply      -> IVacuumAffectable.OnSuctionTick
  ├── VacuumCaptureSystem.Evaluate  -> IVacuumAffectable.TryBeginCapture
  └── VacuumCaptureSystem.Tick      -> OnCaptureProgress / OnCaptureCompleted
```

## Setting it up on an existing character

1. Add an empty child to the character — the **rig** — and put `VacuumController` on it.
   `VacuumDetector`, `VacuumSuctionField` and `VacuumCaptureSystem` are added automatically.
2. Add another empty child under the rig — the **nozzle** — with `VacuumNozzle` on it. Set the
   controller's `nozzle` field to it. The mouth is the nozzle transform plus `mouthOffset`.
3. Assign a `VacuumSettings` asset (`Create ▸ Hedgewitch ▸ Vacuum ▸ Vacuum Settings`). Without one
   the controller falls back to a runtime default rather than doing nothing.
4. Put `VacuumCollection` on the character if you want captures tallied.
5. Drive it from input with `controller.SetSuction(bool)`, and aim it with
   `controller.AimAt(worldPoint)` or `nozzle.AimTowards(direction)`.
6. Optional feedback: `Feedback/VacuumAudio` and `Feedback/VacuumVisuals` on the rig. Both read the
   controller's own state, so they are correct however suction was triggered, and every reference
   on them is optional.

## Making an object vacuumable

Add a `Rigidbody` and `Objects/VacuumableBody`. Either set its fields directly or assign a
`VacuumableProfile` asset, which is the intended route — paper, coins, crates and furniture are all
the same component with a different preset.

| Property | Meaning |
| --- | --- |
| `vacuumMass` | How hard it is to pull, **independent of rigidbody mass**. The pull is `baseForce / vacuumMass` m/s², so 0.1 is paper and 300 is a wardrobe. |
| `suctionResistance` | 0 to 1. A straight multiplier reduction; 1 is immune. |
| `canBeCaptured` | Whether reaching the mouth swallows it. Furniture sets this false and just rattles. |
| `captureRadiusBonus` | Added to the nozzle's own capture radius. |
| `liftMultiplier` | How much gravity the airflow cancels. Above 1 and it leaves the floor eagerly. |
| `swirlMultiplier` | Tangential force; makes objects spiral in rather than track a straight line. |
| `wobbleMultiplier` | Perlin-noise tumbling torque. |
| `funnelMultiplier` | Scales the cross-flow damping that pulls the object onto the mouth. Raise it for light, fast things that would otherwise sling past; drop it for heavy things that should plough their own line. |
| `captureOutcome` | `Destroy`, `Deactivate` (poolable) or `KeepInWorld`. |

Outcomes beyond that are capture handlers — components implementing `IVacuumCaptureHandler` on the
object. Shipped ones: `VacuumCollectibleOnCapture` (banks value in the `VacuumCollection`),
`VacuumCaptureEvents` (UnityEvents, and can veto capture), `EnemyDefeatOnCapture`. Any handler can
refuse a capture via `AllowCapture`.

## Enemies

`Enemies/EnemySuctionResponse` is a separate response, not a heavier prop:

- The airflow pulls, and the enemy pushes back with **its own force** — both are real, so it slides,
  digs in and stumbles instead of sliding along a line.
- Resistance is `enemyStrength` scaled by remaining `stamina`, which drains while it resists. Hold
  the trigger long enough and it loses; let go and it recovers.
- A **grip** meter tracks who is winning. `CanBeCaptured` is false until grip passes
  `captureGripThreshold`, so an enemy at the mouth with fight left in it simply will not go in.
- Every `escapeInterval` it lunges sideways and away — but only once it is actually losing
  (`escapeGripThreshold`), and the grip a lunge buys back scales with its remaining stamina. A
  spent enemy can still physically lunge clear of the cone, which is the honest escape; it can no
  longer thrash its way out of a hold it has no strength left for. Without that, the struggle never
  converges and the enemy is uncatchable.
- `stamina` drains in real seconds while braced, so the number means what it says.
- The AI is never switched off. It implements `IVacuumAwareAgent` and is told what is happening;
  `WantsMovementControl` is how it reports that it has been knocked off its feet.

Bracing is **anchoring, not shoving**: the brace force is capped at the pull, so a strong enemy
holds station and strains rather than out-pushing the vacuum and sailing backwards. Getting away is
the job of the escape lunge, which is weighted sideways — the believable exit from a cone is out of
its edge — and `BasicEnemyAgent` struggles at a fraction of its normal steering effort for the same
reason. If enemies read as being *repelled*, those are the three knobs: `enemyStrength` against
`baseForce / vacuumMass`, `escapeLungeSpeed`, and `struggleSteerEffort`.

`Enemies/BasicEnemyAgent` is a small working example: wanders, chases, and when vacuumed steers away
from the nozzle under its own power until overpowered.

## Demo

`Assets/Scenes/VacuumDemo.unity` is a test room: an isometric camera, a player carrying a nozzle,
props from paper up to immovable furniture, two enemies of different strength, and cover to prove
suction does not reach through walls. Controls: **WASD** move, **mouse** aim, **LMB** or **F** suck,
**Space** jump, **Q/E** turn the camera, **scroll** zoom.

New settings and profile assets are made from `Create ▸ Hedgewitch ▸ Vacuum ▸ …` in the Project
window.

**Live tuning:** the demo scene carries `Demo/VacuumTuningPanel`, a play-mode slider panel
(**F1** to toggle) for reach, force, arrival and enemy strength. Sliders write into the active
settings, so changes land on the next physics step — and in the editor they go into the settings
*asset* and survive leaving play mode, which is what you want while hunting for numbers. **Log
values** prints the whole set in one copyable block.

`Tools ▸ Hedgewitch ▸ Update Vacuum Rig In Open Scene` brings the nozzle placement, the cone angles
and the tuning panel up to date in a scene you have already worked on, leaving everything else in it
untouched. It does write its own cone angles over the settings asset, so numbers hand-tuned there
are replaced; point the controller at your own settings asset to keep them.

## Tests

`Assets/Tests/Vacuum/VacuumSuctionTests.cs` holds play mode tests covering the behaviour the system
claims: that suction pulls with real forces, that `vacuumMass` governs how hard the pull lands, that
walls and the cone bound it, that leaving the airflow releases the object and restores its
rigidbody exactly, and that capture fires for capturable objects and never for the rest. Run them
from `Window ▸ General ▸ Test Runner` (PlayMode), or headless:

```bash
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults results.xml
```

The module is split into `Hedgewitch.Vacuum` (runtime) and `Hedgewitch.Vacuum.Editor` assemblies, so
it can be lifted into another project as a folder.

## Tuning notes

- `baseForce` is an **acceleration** for an object of `referenceMass`. Everything else scales off it.
- **If the player has to aim down at small objects, raise `intakeRadius`.** The mouth sits above
  floor level, so with a pure cone anything small and close is *under* it: at a mouth height of
  1.4 m and a ±22° cone, a coin on the floor is not in the cone until it is 3.4 m away. Inside
  `intakeRadius`, anything in front of the mouth is drawn in whatever the cone says, which is both
  how a real intake behaves and what makes hoovering feel effortless. Its strength tapers with
  distance, so it is an intake rather than a sphere of universal grab. Mounting the nozzle low and
  tilted toward the floor (the demo rig does both) matters as much as the number.
- **`coneHysteresis`** holds an object with slightly wider limits than it was caught with. Without
  it, something converging on the mouth crosses out of the cone mid-flight, gets released, and —
  since nothing decelerates a released object — coasts straight past the player.
- **Getting objects into the intake** rather than past it is three more settings working together,
  and they are the next place to look if things fly by the player:
  `lateralDamping` damps velocity *across* the airflow (this is what bends a fly-past into a
  funnel — at 0, any lateral offset becomes a slingshot); `approachSpeedAtMouth` and
  `approachBrakingDistance` taper the closing-speed cap so objects decelerate into the mouth; and
  `gravityCompensation` at 1 stops gravity biasing every pull into a flat, floor-skimming approach,
  which is what makes a vacuum feel strong sideways but weak upward.
- `maxApproachSpeed` caps only the velocity component heading into the mouth; objects can still be
  flung sideways, and motion *away* from the nozzle is never clamped.
- `forceOverDistance` and `forceOverCone` are curves, so "gentle at range, savage up close" is a
  curve shape rather than a magic number.
- `vacuumablesBlockAirflow` is off by default, so a heap of props is pulled as a heap. Turn it on and
  a wardrobe shields what is behind it.
- Suction is applied with `ForceMode.Acceleration` because `vacuumMass` has already been folded into
  the pull. That keeps "how hard is this to suck" independent of the mass used for collisions.
- While sucked, objects get extra linear and angular damping (restored exactly on release), which is
  what stops jitter and runaway velocity.
- `VacuumNozzle.AimTowards` holds for `aimHoldTime` (0.25 s) rather than a single frame, so aiming
  from `FixedUpdate` or at a low rate is steady. Call `ClearAim()` to hand the nozzle back to rest.
