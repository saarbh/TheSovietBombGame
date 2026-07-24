# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**The Hot War** (repo `GMTKJam2026`, README title `TheSovietBombGame`) — a first-person Cold-War bunker puzzle game for GMTK Jam 2026. Unity **6000.3.15f1**, **URP 17.3.0**, built for **WebGL** (see the `Web - Desktop - Release` build profile).

The core mechanic: a constant **7-minute countdown** (`TOTAL_TIME_SECONDS = 420f`) with four passcode-locked rooms that only unlock at specific elapsed times. The painted plates on the doors **lie** about those times — `RoomConfig` carries both `actualUnlockTimeMinutes` and `expectedUnlockTimeMinutes`, and the discrepancy is the puzzle. Solving all four rooms unlocks the "Report False Alarm" phone option, which is the only path to a victory ending that involves calling.

## Current state — spec-first repo

**Almost nothing in the design docs is implemented yet.** Read this before assuming a class exists:

- The only gameplay-adjacent code is [Assets/assets/HotWarLevelKit.cs](Assets/assets/HotWarLevelKit.cs) — a level-geometry generator, not runtime logic.
- The rest of `Assets/` is Unity's URP template (`TutorialInfo/`, `SampleScene.unity`, `InputSystem_Actions.inputactions`).
- Every class in [overview.md](overview.md) and [architecture.mermaid](architecture.mermaid) — `GameManager`, `WatchManager`, `PlayerController`, `DoorLockController`, `BasePuzzle<T>`, `AudioManager`, etc. — **is a target design, not existing code**. Treat those two files as the authoritative build order and API contract when creating new systems.
- Scenes named in the spec (`BunkerGameScene`, `MainMenuScene`) do not exist yet — see [Scenes and team workflow](#scenes-and-team-workflow).

### Dependencies

[SKILL.md](SKILL.md) mandates **UniTask** (all async), **DOTween** (all tweening), and **VContainer** (DI over singletons). Current state:

| Package | Source | Status |
| --- | --- | --- |
| `com.cysharp.unitask` 2.5.11 | OpenUPM scoped registry | In [manifest.json](Packages/manifest.json) |
| `jp.hadashikick.vcontainer` 1.19.0 | OpenUPM scoped registry | In [manifest.json](Packages/manifest.json) |
| DOTween | Unity Asset Store (free) | Imported manually into `Assets/` |

DOTween has no official UPM registry or git URL — Demigiant ships it through the Asset Store only. The OpenUPM hits for "dotween" are third-party republishes; **do not add one to the manifest**. It arrives via Package Manager → My Assets, lands under `Assets/Demigiant/`, and needs its setup panel (`Tools → Demigiant → DOTween Utility Panel` → *Setup DOTween...*) run once after import or the generated links file will be missing.

## Scenes and team workflow

Four people are working in parallel, **one scene per person**, specifically to avoid merge conflicts on `.unity` files — Unity scene YAML merges badly and a conflicted scene usually means losing work. Respect that boundary: do not edit someone else's scene to wire up your system.

- `Assets/Scenes/SampleScene.unity` is the shared **graybox** scene — level layout and blockout only, driven by `HotWarLevelKit`.
- The spec's `BunkerGameScene` and `MainMenuScene` are **created later**, once the code and assets that populate them exist. Don't scaffold empty scenes ahead of their content.
- When a change spans scenes, prefer prefabs and ScriptableObjects as the handoff unit rather than editing multiple scenes.

`Packages/manifest.json` and `ProjectSettings/` *are* shared across all four branches and are the usual conflict points — validate the manifest parses after any merge (it has already taken one bad resolution that duplicated a key).

## Open design questions

Unresolved divergences between the design doc and what is built. Raise these before building on either side, and flag it if a new room hits the same fork.

- **Room access: combo locks vs. the global countdown.** `DoorComboLock` gates rooms behind a Helldivers-style arrow sequence, but the design doc's own correction states room access is controlled **entirely** by the global countdown, with a visible per-door timer and no puzzle to get in. `RoomConfig` also still carries a `correctPasscode` from a third, earlier scheme. Three access models exist in the repo at once. *Team is deciding; not settled as of 2026-07-24.*
- **Puzzle count.** `PuzzleTracker.TOTAL_ROOM_PUZZLES` is 4; the doc settles on five essential rooms plus two stretch rooms. The all-solved gate fires a room early until these agree.
- **Ending gate.** `GameManager.EvaluateEnding` resolves two endings from `AreAllPuzzlesSolved()` + phone choice. The doc's finale assembles a multi-character code ordered by verification stage and describes four endings.

### Doors: two mechanisms, pick by ownership

- [SlidingDoor.cs](Assets/Scripts/Puzzles/SlidingDoor.cs) — the room owns its own panel. Slides along the panel's **own** local X (`transform.right`), not `localPosition.x`, which follows the parent's axes and ignores the panel's rotation.
- [PuzzleDoorOpener.cs](Assets/Scripts/Puzzles/PuzzleDoorOpener.cs) — the doorway already has someone else's `RoomDoor`. Opens that instead of adding a second leaf.

Both hang off `IPuzzleResolution.OnResolved`, which fires on a **filed answer, right or wrong** — a wrong answer still ends the player's business in the room, and sealing them in would be a dead end with the clock running.

## Assembly definitions

**Deliberately not used** — this is a jam, and asmdefs are not worth the wiring cost. Everything compiles into `Assembly-CSharp`. Editor-only code goes in an `Editor/` folder or is wrapped in `#if UNITY_EDITOR`, as [HotWarLevelKit.cs](Assets/assets/HotWarLevelKit.cs) does. Do not introduce `.asmdef` files without asking; adding one forces every other script that references it to be sorted into assemblies too.

## Commands

There is no build script, test script, or CI in this repo. Work is normally done in the open Unity Editor.

```powershell
# Editor path for this project's Unity version
$UNITY = "C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe"

# Compile check / import without opening the GUI (fails if the Editor already has the project open)
& $UNITY -batchmode -quit -projectPath "d:\Unity Projects\GMTKJam2026" -logFile -

# EditMode tests (none exist yet; com.unity.test-framework 1.6.0 is installed)
& $UNITY -batchmode -runTests -projectPath "d:\Unity Projects\GMTKJam2026" -testPlatform EditMode -testResults results.xml -logFile -

# A single test / fixture
& $UNITY -batchmode -runTests -projectPath "d:\Unity Projects\GMTKJam2026" -testPlatform EditMode -testFilter "TheHotWar.Tests.WatchManagerTests" -testResults results.xml -logFile -
```

Unity holds a project lock — batchmode and the open Editor cannot both run. Prefer MCP against the live Editor.

### MCP for Unity

`com.coplaydev.unity-mcp` (10.1.0, from the `package.openupm.com` scoped registry) is installed, and a **`unity-mcp-skill`** is available. Use it for anything touching the live Editor — creating GameObjects, wiring serialized references, reading console errors after a script change, entering play mode. It is faster and far more reliable than editing `.unity`/`.prefab` YAML by hand. **Never hand-edit scene or prefab YAML** when an MCP call can do it.

## Level geometry — `HotWarLevelKit`

[HotWarLevelKit.cs](Assets/assets/HotWarLevelKit.cs) (namespace `TheHotWar`) is a declarative, auto-rebuilding level generator. You specify *what* each room is — name, `size` (width × depth), `shape` (Rectangle/LShape), `feature` (None / PlatformWithStairs / Basement), optional side and gap — and it resolves *where* everything goes: corridor placement (staggered left/right, auto-lengthening), door openings, which walls are shared and omitted, L-notch position, stairs, basement stairwells and floor holes.

Two conventions matter more than anything else in that file:

- **`HOTWAR_ROOMS` is disposable output.** `RebuildLevel()` destroys and regenerates the whole root. Never put hand-authored work or gameplay components inside it.
- **`HOTWAR_PROPS` is hand-polish and is never touched by a rebuild.** Anything authored by hand — real props, gameplay scripts, spawn points — goes here or in a separate root.

`OnValidate` triggers a debounced rebuild (`rebuildDelay`, default 0.35s) on every Inspector change while `autoRebuild` is on. Context-menu entries: `Rebuild Level`, `Load Doc Layout` (the canonical 4-room layout with the lying plates), `Append Example Rooms`, `Spawn Doc Control-Room Props`, `Apply Mood Lighting`, `Clear Rooms`, `Clear Props`.

Internally it compiles specs into `InternalRoom` via a door-relative `Frame` (u = across the door wall, v = away from the door), so feature placement logic is written once and mapped to whichever wall the door lands on. Output is plain primitives with no scripts attached.

## Architecture

Five feature modules, detailed in [overview.md](overview.md) with the full class diagram in [architecture.mermaid](architecture.mermaid). The load-bearing patterns:

- **Thin MonoBehaviour / composed facade.** `PlayerController` owns `PlayerMovement`, `CameraController`, and `PlayerInteraction` as sub-controllers and exposes the one-way `IsControlRoomDeparted` flag. `WatchManager` and `PuzzleTracker` are plain C# controllers, not MonoBehaviours — keep them scene-independent and unit-testable.
- **`IInteractable` is the universal interaction contract** (`Interact(PlayerController)`, `GetPrompt()`). `PlayerInteraction` raycasts for it and fires `OnInteractableTargetChanged`; both `CrosshairUI` and `InteractionPromptUI` subscribe to that one event rather than polling.
- **Per-door state, not a central door manager.** Each door GameObject carries its own `DoorLockController` + `RoomConfig` ScriptableObject. `CanUnlockAtCurrentTime()` gates whether the keypad opens at all; `ValidateCode()` checks the passcode. `RoomDoor` is purely the visual/pivot view owned by the lock controller.
- **`BasePuzzle<T>` is generic and abstract.** `CheckSolve()` compares `currentState` to `targetState` via `EqualityComparer<T>.Default`. Concrete subclasses must close the generic (`class RadarPuzzle : BasePuzzle<float>`) — an open generic cannot be attached to a GameObject.
- **`GameManager` owns all ending evaluation** (`EvaluateEnding(bool leftControlRoom, PhoneCallChoice)`), is a scene singleton in the gameplay scene, and is destroyed on reload. `AudioManager` is the only `DontDestroyOnLoad` singleton, with three enum-indexed `AudioClip[]` arrays (`SFXType`/`BGMType`/`MusicType`) mapped to three separate `AudioSource` channels — index by casting the enum, keep array order in sync with the enum.

Ending matrix (two endings): victory = stay in the control room and don't call, *or* solve all 4 and don't call, *or* solve all 4 and report a false alarm. Everything else — leaving without calling, or reporting an incoming nuke (the only option available when puzzles are unsolved) — is nuclear war.

## Conventions

[SKILL.md](SKILL.md) is the full Unity C# standard for this project — read it before writing gameplay code. The rules most easily violated:

- **Never use `?.`, `??`, or `is null` / `is not null` on `UnityEngine.Object` types.** Pattern matching bypasses Unity's overridden `==` and fails to detect destroyed objects. Use `if (obj == null)` / `if (obj)`. Pure C# types get the modern operators.
- Prefer `var`; Allman braces; always braced bodies; `[SerializeField] private` over public fields, grouped under `[Header(...)]`.
- Subscribe/unsubscribe to events only in `OnEnable`/`OnDisable`. No `GetComponent`, allocations, LINQ, or string concatenation in `Update`.
- Assign references via serialized fields in the Inspector. Runtime lookups (`GetComponent`, `FindObjectOfType`) are a last resort.
- New Input System only — never `Input.GetKeyDown`. The template `InputSystem_Actions.inputactions` already has a `Player` map (Move, Look, Interact, Crouch, Jump, Sprint…) and a `UI` map; extend it rather than adding a second asset. Guard callbacks with `if (Time.timeScale == 0) return;`.
- UniTask over coroutines and `System.Threading.Tasks`; always pass a `CancellationToken` (`this.GetCancellationTokenOnDestroy()`), and `ignoreTimeScale: true` for anything that must tick while paused. DOTween over manual lerps in `Update`; `.SetUpdate(true)` for tweens that play while paused.
- VContainer over new singletons. No `LifetimeScope` exists yet — the first system that needs injection creates it, and registers to interfaces rather than concrete types. `AudioManager` is the one sanctioned persistent singleton.

## Repo notes

- Branch `shay` is the active working branch; `main` is the PR target.
- The `*.csproj`/`*.sln` files at the root are Unity-generated and get rewritten on every reimport. Never edit them by hand; they are noise in `git status`.
