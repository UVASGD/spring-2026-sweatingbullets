# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Sweating Bullets** is a first-person tactical shooter built in Unity 6 (6000.3.1f1) where the player's psychological state (a "Nerves" system, 0-100) directly affects gunplay mechanics like trigger delay, camera sway, and visual distortion. The player uses deliberate revolver mechanics (cock hammer, aim, fire) against enemies in procedurally generated levels.

## Build & Run

This is a Unity project — open in Unity 6 (6000.3.1f1) and use the editor to build/run. The C# solution is `CowboyDueller.sln`. No CI/CD or automated test pipeline exists.

**Scenes:** `Main.unity` (gameplay), `Dev.unity` (testing), `MainMenu.unity` (entry point)

## Architecture

### Core Systems

All gameplay scripts live in `Assets/Scripts/` under these namespaces:

- **`Player`** — Player controller, weapon mechanics, noise emission
- **`Player.Nerves`** — Nerves subsystem (inputs, recovery, audio/visual effects)
- **`Enemy`** / **`Enemy.States`** — AI with hierarchical state machine (HFSM)
- **`Tiles`** — Procedural level generation (WFC algorithm + simple frontier-based)

### Nerves System (central mechanic)

`NervesManager` auto-discovers all `NervesInput` components via `GetComponentsInChildren` and aggregates them each frame. Inputs (e.g., `ADSNervesInput`, `NearMissNervesInput`) contribute nerves; `NervesRecovery` reduces them when the player is crouched/still/not aiming. The accumulated value drives `NervesAudioController` and `NervesVisualEffectsController`.

### Weapon System

`WeaponController` uses Unity's new Input System with three actions: Fire, HammerPull, ADS. Fires raycasts with range 100. Trigger delay scales with nerves (up to 0.15s at max). Publishes `OnWeaponFired` and `OnWeaponShotResolved` events consumed by nerves inputs and camera effects.

### Enemy AI

`EnemyAI` runs a UnityHFSM state machine with states: Patrol, Follow, Shoot, FollowUpToShoot, Investigate, Dead. Vision (120 FOV, 100m range, raycast LOS) and hearing (scales by noise type/intensity from `PlayerNoiseEmitter`) drive state transitions. `EnemyShoot` handles raycast-based weapon execution.

### Level Generation

`WFCManager` implements Wave Function Collapse on a grid with tile compatibility constraints, manual feature placement, NavMesh baking after generation, and enemy spawn point management. `TileManager` is a simpler frontier-based alternative.

After grid generation, `WFCManager` also:
- Spawns a **floor tile** (`floorTilePrefab`) at every grid cell (slightly offset downward to avoid z-fighting with tile geometry)
- Spawns **outside tiles** (`outsideTilePrefab`) in a configurable radius around the grid as impassable scenery
- Sets grid bounds on the **`sandBlendMaterial`** so the `Custom/SandBlend` shader can blend between level and outside textures at the edges

### Sand Blend Shader

`Assets/Shaders/SandBlend.shader` is a custom URP shader that blends two full texture sets (color, normal, AO) using world-space UVs. Blend factor is based on distance from the grid bounds, controlled by `_BlendWidth`. Grid bounds (`_GridMinX/Z`, `_GridMaxX/Z`) are set automatically by `WFCManager` at runtime.

### Camera

`CameraGunplayEffects` handles multi-phase recoil (snap, overcorrect, settle), micro-shake, nerves-based ADS sway, and FOV shift. Camera rotation and gun rotation are handled separately.

## Key Dependencies

- **UnityHFSM v2.2.0** (`Assets/UnityHFSM-v2.2.0/`) — State machine framework for enemy AI
- **SUPER Character Controller** — Third-party player movement controller
- **Unity AI Navigation** (2.0.10) — NavMesh for enemy pathfinding
- **Unity Input System** (1.17.0) — Player input handling
- **URP** (17.3.0) — Rendering pipeline

## Patterns Used

- **Event-driven coupling:** WeaponController events connect to nerves, camera, and audio systems without direct references
- **Component auto-discovery:** NervesManager finds NervesInput children; EnemyAI finds EnemyShoot on weapon object
- **Configuration via SerializeField:** Most gameplay parameters (difficulty 1-10, detection ranges, nerves rates) are tunable in the Inspector
- **HFSM pattern:** Enemy states use condition-based and trigger-based transitions with timer-gated exits
