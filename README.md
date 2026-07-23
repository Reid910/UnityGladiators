# Wave Survival RPG

A third-person action RPG prototype built in Unity. The player fights through progressively harder waves of melee enemies, using camera-relative movement and a simple attack combo, until all waves are cleared or the player is defeated.

## Gameplay Overview

- **Third-person combat** — camera-relative movement, melee attacks, and gravity-driven character controllers for both the player and enemies.
- **Wave-based encounters** — enemies spawn in escalating waves from designated spawn points; each wave adds more enemies than the last.
- **Health & death handling** — shared health system drives damage, death behavior, and animation state for both players and enemies.
- **HUD & end states** — live health, wave count, and enemies-remaining display, plus victory/game-over screens with a restart option.

## Requirements

- [Unity 6000.4.3f1](https://unity.com/releases/editor/archive) (Unity 6 LTS) or later
- Universal Render Pipeline (URP)
- Unity's new [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)

## Getting Started

1. Clone the repository:
   ```bash
   git clone <repo-url>
   ```
2. Open the project folder in **Unity Hub** using Unity `6000.4.3f1` (or allow Hub to install a compatible version).
3. Open `Assets/Scenes/SampleScene.unity`.
4. Press **Play** in the Unity Editor.

## Controls

| Action | Input |
|---|---|
| Move | WASD / Left Stick |
| Attack | Bound via Input Actions (`InputSystem_Actions`) |
| Camera | Mouse |

Input bindings are defined in the project's Input Actions asset and can be remapped there.

## Project Structure

```
Assets/
├── Scripts/
│   ├── PlayerController.cs    # Camera-relative movement & animation blending
│   ├── PlayerCombat.cs        # Player attack input, hit detection, damage
│   ├── EnemyController.cs     # Enemy AI: chase, attack, gravity
│   ├── Health.cs              # Shared health/damage/death system
│   ├── WaveManager.cs         # Wave spawning, progression, win/lose events
│   ├── GameUI.cs              # HUD updates and end-of-game screens
│   ├── ThirdPersonCamera.cs   # Mouse-controlled follow camera
│   └── FaceCamera.cs          # Billboard/UI-facing helper
├── Prefabs/                   # Player, enemy, and UI prefabs
├── Scenes/                    # Game scenes
├── Materials/                 # URP materials
└── Settings/                  # URP render pipeline assets
```

## Core Systems

**Health (`Health.cs`)**
Tracks current/max health, exposes a `Died` event, and drives death behavior (disabling controllers/colliders, triggering death animations, optional destroy/disable-on-death).

**WaveManager (`WaveManager.cs`)**
Spawns enemies in waves from a pool of spawn points, scaling enemy count per wave, and raises `GameWon` / `GameLost` events when the encounter ends.

**PlayerCombat (`PlayerCombat.cs`)**
Reads attack input from the Input System and applies damage to any `Health` component within range on the enemy layer.

## Status

This is an active work-in-progress class/learning project. Expect incomplete art, placeholder assets, and evolving gameplay systems.

## License

No license specified. All rights reserved by the project owner unless stated otherwise.
