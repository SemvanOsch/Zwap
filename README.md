# Zwap

A 2D mobile game by Zwap-Studio, made in Unity 6.

You're a fish swimming up a river. Dodge rocks, tree stumps, trees, bears and herons for as long as you can. The catch: every few seconds the controls change. One moment you're tilting your phone, the next you're using arrow buttons, a joystick, sliders, dragging your finger, or slingshotting the fish. Some controls also come in an inverted version. The longer you survive, the faster everything gets.

## Features

- 6 control schemes that rotate during a run: tilt, touch arrows, follow finger, joystick, sliders and slingshot, some with inverted variants
- Controls are picked in rounds of easy, medium and hard
- Every control has its own look for the map and obstacles, with a circle transition when it switches
- An icon shows which control is coming next, with a shock animation right before the switch
- Cans that give you a one hit shield
- Bears with a full width swipe attack and herons with a kill zone
- Unlockable fish skins
- High score saving and sharing your score
- Help popups the first time you see a control, and a help menu in the pause screen

## Getting started

1. Install **Unity 6000.3.23f1** through Unity Hub.
2. Clone this repo.
3. In Unity Hub, add the `Zwap/` folder (not the repo root) as a project and open it.
4. Open `Assets/Scenes/Home-Screen` and press Play.

To build, use **File > Build Profiles** and pick Android or iOS. Sharing scores only works on a real device, not in the editor.

## Project structure

```
Zwap/
  Assets/
    Scenes/          Home-Screen, Game-Scene, Game-over
    Scripts/         all game code
    Prefabs/         spawner, obstacles, controls, backgrounds
    Sprites/
    Audio/
    VirtualJoystick/ third party joystick asset
  Packages/
  ProjectSettings/
```

## Third party

- [Native Share](https://github.com/yasirkula/UnityNativeShare) by yasirkula
- Virtual Joystick Pack by Terresquall
- TextMesh Pro, Cinemachine and the Input System from Unity
