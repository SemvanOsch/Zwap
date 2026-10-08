# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Permission rule (read first)

Do **not** make any changes to files, scenes, or assets without the user's specific, explicit permission for that change. Reading, exploring, explaining and proposing edits is fine, but wait for a clear "yes, do it" before writing anything. Approval for one change does not carry over to the next.

## Project

Zwap is a 2D mobile game made in **Unity 6000.3.23f1** (Unity 6) by Zwap-Studio. You play a fish swimming up a river and dodge falling objects (rocks, tree stumps, trees, bears, herons). Every few seconds the control scheme switches to a different one, so the player has to keep adapting. The score goes up over time and the game speeds up as it does.

Packages used: new Input System, the 2D feature set, Cinemachine, TextMesh Pro, and Native Share (yasirkula, pulled from git) for sharing scores. The Terresquall Virtual Joystick asset lives in `Assets/VirtualJoystick/`.

The Unity project root is the `Zwap/` subfolder, not the repo root. Open `Zwap/` in Unity Hub.

## Building and running

There is no CLI build or test flow. Open the project in the Unity Editor to run it and use **File > Build Profiles** to build. `com.unity.test-framework` is installed but there are no tests.

Start play mode from `Home-Screen`. `GameData` and `SaveManager` are created there and kept with `DontDestroyOnLoad`, so starting `Game-Scene` directly works but logs warnings and skips some score/save logic.

## Scenes

In `Zwap/Assets/Scenes/`, in build order:

1. `Home-Screen` - main menu, skin select, high score, intro sequence before the game starts.
2. `Game-Scene` - the actual game.
3. `Game-over` - final score, high score, share button, back to menu.
4. `SampleScene` - leftover template scene.

Ignore the scenes under `Assets/TextMesh Pro/` and `Assets/VirtualJoystick/Demo.unity`, those are vendor examples.

## Code layout

Game code is everything directly in `Zwap/Assets/Scripts/` plus `Zwap/Assets/Scripts/Editor/`. Do not edit third party code in `Assets/TextMesh Pro/` or `Assets/VirtualJoystick/`.

`Zwap/Assets/PlayerControls.cs` is generated from `PlayerControls.inputactions`. Do not edit it by hand, regenerate it from the asset.

Several class names do not match their file name. Unity needs the class name to stay the same as what scenes reference, so don't rename these without asking:

| File | Class |
| --- | --- |
| Object-Movement.cs | `MoveDown` |
| TouchControlsArrow.cs | `TouchControls` |
| Pausecontrol.cs | `PauseManager` |
| Performancesettings.cs | `PerformanceSettings` (static, no component) |
| RockObject.cs | `RockWaterEffect` |
| Scene-manager.cs | `SceneButtonHandler` |
| ShockAnimationScript.cs | `SimpleAnimation` |
| TextUpdates.cs | `ScoreDisplay` |
| Savemanager.cs | `SaveManager` |

## Gameplay architecture

### Controls

- **ControlSwitcher** (singleton, sits on the player) - the core of the game. Every `switchInterval` seconds (default 10) it switches to the next control. Control types are `Tilt`, `Touch`, `Follow`, `Joystick`, `Slider`, `Slingshot`. Some controls also have an inverted variant (listed in `invertibleControls`). Each control/inverted combo is tagged Easy, Medium or Hard, and the next control is picked from the pool that matches `difficultyOrder` (Easy, Medium, Hard, then wraps). It shows only the panel for the current control, updates the "next control" icon and color, and spawns the shock effect so its bang lands on the switch. Other scripts listen to `OnControlChanged`.
- **ControlType enum order matters.** New values are appended at the end so serialized values in scenes keep their meaning. Never insert or reorder.
- **PlayerStart** - the fish controller (`Rigidbody2D`, moved with `MovePosition`). Reads the current control from `ControlSwitcher` and gets a movement target from the matching mode (tilt via `Accelerometer`, touch arrows, follow finger, joystick, sliders, slingshot). Movement speed scales with score. Also handles skins (frame animation from PlayerPrefs `SelectedSkin`), the can shield, getting hit by objects tagged `Entity`, dying when pushed off the bottom, and game over (saves high score and run count, then loads `Game-over`). `ForceFatalHit()` is the instant kill used by enemies. Keyboard input is still bound but not used for movement right now.
- **TouchControls** - on-screen arrow buttons. Buttons call `OnUpPress`/`OnUpRelease` etc. and the summed vector is pushed into `PlayerStart`. The inverted panel is wired to the opposite handlers, so no negation in code.
- **SliderControls** - two sliders (bottom = X, right = Y) for Slider mode.
- **GodMode** - static toggle from the pause menu that makes the player unkillable. Resets on every scene load.

### Spawning and obstacles

- **Spawner** - weighted random spawning from a list of `SpawnableItem`s. Each item has a spawn zone (anywhere, edges, left, right, center, perch), weight, batch count, spacing and delay, and can be mirrored on the right side (trees). Spawn rate scales with score. The can has its own separate timer that pauses while the fish still has a shield. Has a custom inspector that shows live spawn percentages.
- **MoveDown** (Object-Movement.cs) - moves an object down in world space and destroys it below `despawnY`.
- **Can** - pickup that gives a one hit shield. Must not be tagged `Entity`.
- **Bear** - screen wide obstacle that drifts down, shows a warning, then does a full width swipe that kills on contact. **BearSwipeHitbox** forwards trigger hits from a child collider to the bear.
- **Heron** - stationary enemy with a kill circle. Shows a red outline and a fill that gets stronger as the player moves in, then kills when the player's hitbox is inside.
- **ObjectVariant** - swaps a falling object's sprite to match the current control and refits the collider.
- **RockWaterEffect** (RockObject.cs) - changes a rock's water swirl color per control. **RockRotation** spins rocks only during the inverted joystick control.

### Visuals and map

- **ControlBackground** - every control has its own "map" (background and rock side borders). On a switch it hands the layers to **BackgroundReveal**, which plays a growing circle transition from the fish. `BackgroundReveal.OnRevealProgress` is used by ObjectVariant, RockWaterEffect and RockRotation so they change in sync with the circle.
- **BackgroundScroller** - scrolls a `RawImage` UV rect for a looping background.
- **SimpleAnimation** (ShockAnimationScript.cs) - the shock effect before a control switch, with sound.

### UI, menus and data

- **ScoreManager** (singleton, Game-Scene) - score goes up over time, faster as score rises (capped multiplier). Shown as meters ("M").
- **GameData** (singleton, created in Home-Screen) - carries the score and high score between scenes.
- **SaveManager** (singleton) - saves `SaveData` (`highScore`, `runs`) as JSON to `Application.persistentDataPath/save.json`. Add new save fields to `SaveData`.
- **HomeScreenUI**, **ScoreDisplay** (TextUpdates.cs) - show high score / final score.
- **SkinPreview** - skin select on the home screen, locked skins show as a flat silhouette with an outline. Saves the choice to PlayerPrefs `SelectedSkin`.
- **IntroSceneManager** - Play button: zooms into the home screen UI, plays a short skippable slideshow, then loads the game scene.
- **PauseManager** (Pausecontrol.cs) - pause/resume with `Time.timeScale` and `AudioListener.pause`.
- **HelpManager** - one time help popup the first time each control shows up, remembered in PlayerPrefs. **HelpMenu** - swipeable help pages opened from the pause menu, opens on the page for the current control.
- **ShareScore** - share the score (optionally with a screenshot) through Native Share. Only works in a real iOS/Android build.
- **SceneButtonHandler** - generic button to load a scene (unfreezes time first) or toggle an object.
- **SceneField** + `Editor/SceneFieldPropertyDrawer.cs` - lets you drag a scene asset into an inspector field instead of typing its name.
- **PerformanceSettings** - runs automatically before the first scene loads. Sets frame rate and vsync, caps the max delta time, and turns off stack traces for `Debug.Log` in release builds.

## Conventions

- Most behavior is set up in the Inspector. Prefabs, speeds, lanes, panels and sprites are `[SerializeField]` fields, so the `.unity`, `.prefab` and `.meta` files matter as much as the C#. Renaming a serialized field or changing its type breaks the scene wiring. Use `[FormerlySerializedAs]` if a rename is really needed.
- Scripts that listen to `ControlSwitcher` or `BackgroundReveal` subscribe in `OnEnable`, try again in `Start` as a safety net for init order, and unsubscribe in `OnDisable`. Follow the same pattern for new listeners. `OnControlChanged` does not fire for the first control, so apply the starting state yourself in `Start`.
- Some comments and identifiers are in Dutch. That's fine, leave them unless asked.
- Never delete or rename `.meta` files or change GUIDs, it breaks asset references across scenes.
- Tags in use: `Entity` (anything that hurts the player on contact).
