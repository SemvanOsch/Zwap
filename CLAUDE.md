# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ⚠️ Permission rule (read first)

Do **not** make any changes to files, scenes, or assets without the user's specific, explicit permission for that change. It is fine to read, explore, and explain freely, and to propose edits — but wait for a clear "yes, do it" before writing anything. Approval for one change does not carry over to the next.

## Project

Zwap is a 2D mobile game built in **Unity 6000.3.23f1** (Unity 6). The player dodges falling objects; input comes from keyboard (WASD), on-screen touch arrows, and device tilt (accelerometer) simultaneously. Uses the new Input System, the 2D feature set, Cinemachine, and TextMesh Pro.

The Unity project root is the `Zwap/` subfolder (not the repo root). Open `Zwap/` in the Unity Hub. The repo root only holds `Zwap/`, `Packages/`, `ProjectSettings/`, and docs.

## Building & running

There is no CLI build/test flow — this is a Unity Editor project. Open it in the Unity Editor to run, and use **File → Build Profiles** to build. There are currently no automated tests despite `com.unity.test-framework` being present.

## Scenes

Game scenes live in `Zwap/Assets/Scenes/`: `Home-Screen`, `Game-Scene`, `SampleScene`. (The many scenes under `Assets/TextMesh Pro/` are vendor examples — ignore them.)

## Gameplay architecture

Only the scripts directly under `Zwap/Assets/Scripts/` and `Zwap/Assets/PlayerControls.cs` are game code; everything under `Assets/TextMesh Pro/` is third-party sample code and should not be edited.

- **PlayerControls.cs** — auto-generated from `PlayerControls.inputactions`. Do not hand-edit; regenerate from the `.inputactions` asset.
- **PlayerStart.cs** — the player controller (on a `Rigidbody2D`). Sums three input sources every `FixedUpdate` into one movement vector: `moveInput` (touch), `keyboardInput` (Input System Move action), and tilt (`Accelerometer`). Drives an `Animator` bool `IsMoving`. Touch buttons call `AddInput`/`RemoveInput` on press/release. Note: several class names don't match their file (`PlayerStart` class in PlayerStart.cs, `MoveDown` in Object-Movement.cs, `RockSpawner` in Spawner.cs, `TouchControls` in TouchControlsArrow.cs).
- **TouchControlsArrow.cs** (`TouchControls`) — wires the on-screen arrow buttons to `PlayerStart`. `ToggleInverse()` flips control direction and rotates the arrow sprites 180°; `SetNormal()` resets them. Some comments/identifiers are in Dutch.
- **Spawner.cs** (`RockSpawner`) — spawns random prefabs from `itemPrefabs` on a timer at a random lane X (`lanePositions`) using the spawn point's Y/Z.
- **Object-Movement.cs** (`MoveDown`) — moves a spawned object straight down and `Destroy`s it below `despawnY`.
- **BackgroundScroller.cs** — scrolls a `RawImage`'s UV rect for a looping background.

## Conventions

- Inspector-driven: prefabs, transforms, speeds, and lane positions are `[SerializeField]`/public fields set in the Editor, so the `.unity`/`.prefab`/`.meta` files carry as much behavior as the C#. Changing a serialized field's name or type can break existing scene wiring.
- Never delete or rename `.meta` files or change GUIDs — it breaks asset references across scenes.
