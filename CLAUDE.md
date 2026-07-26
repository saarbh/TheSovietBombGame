# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**The Hot War** (repo `GMTKJam2026`, README title `TheSovietBombGame`) — a first-person Cold-War bunker puzzle game for GMTK Jam 2026. Unity **6000.3.15f1**, **URP 17.3.0**, built for **WebGL** (see the `Web - Desktop - Release` build profile).

The core mechanic: a constant **7-minute countdown** (`TOTAL_TIME_SECONDS = 420f`) bounding a run through four **passcode-locked rooms**. Solving all four unlocks the "Report False Alarm" phone option, which is the only path to a victory ending that involves calling.

> **Doors are gated on their passcode alone (settled 2026-07-26).** Earlier revisions of this file described rooms unlocking at specific elapsed times behind door plates that **lie** about those times. That mechanic is gone: no door consults the clock, and every keypad is live from the first frame. The countdown still runs and still decides the endings — it just no longer stages which room is reachable when. See [Doors: passcode only](#doors-passcode-only).

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

- **Puzzle count.** `PuzzleTracker.TOTAL_ROOM_PUZZLES` is 4; the doc settles on five essential rooms plus two stretch rooms. The all-solved gate fires a room early until these agree. The *card* side no longer depends on this — `GameManager.requiredStages` is a serialized `VerificationStage[]` (default: the doc's five), so `IsVerificationComplete` follows the rooms actually in the build. `PuzzleTracker` is still on the old constant.
- **Ending gate.** `GameManager.EvaluateEnding` resolves two endings from `AreAllPuzzlesSolved()` + phone choice. The doc's finale assembles a multi-character code ordered by verification stage and describes four endings.

### Evidence cards: rooms produce, the inventory collects

Per the design doc, a confirmed room's card "enters their inventory automatically" — there is no pickup step and no way to discard evidence.

- `VerificationStage` (Detect → Confirm → Classify → Trace → Authenticate → Authorize → Report) — **declaration order is procedural order**, and the finale sorts on it. Rooms unlock in a scrambled order, so room order ≠ code order; that mismatch is the whole point of the central decoder. Reordering the enum silently rewrites every saved `PuzzleConfig`.
- `PuzzleConfig.Stage` decides the slot; `stageLabel` is only the printed noun. `OnValidate` warns when the two disagree, which is what caught `GeneratorRoomConfig` sitting on the default `Detect` after the enum was added.
- **A new room needs no card wiring.** `BasePuzzle.MarkResolved` builds the card from the config and files it before `OnResolved` fires. Override `BuildCard` only for a room whose card isn't fully described by its config.
- `PuzzleCardInventory` is plain C# on `GameManager`, exactly like `PuzzleTracker`. First card per stage wins — a second is refused and logged, since that means two configs claim the same stage.
- Presentation is disposable: subclass `PuzzleCardInventoryView` and the data layer doesn't change. `EvidenceHudView` (`Assets/prefabs/EvidenceHud.prefab`, Tab) is the placeholder; going diegetic means deleting it and writing a clipboard view. `EvidenceLogFormatter` holds the wording so the HUD, a clipboard and the console can't disagree.
- `Assets/prefabs/GameSystems.prefab` carries the `GameManager`. Without it in a scene, rooms still resolve and print — the card just has nowhere to go, which is the supported single-room-test setup.

### Room parts worth reusing before writing new ones

- `IConfirmablePuzzle` — `ConfirmLever` and `ResetLever` are shared by every room and are written once against this. A room implements it (`IsConfirmed`, `CanConfirm`, `ConfirmBlockedReason`, `Confirm`, `CanReset`, `ResetAttempt`) and the levers need no code. Serialize the puzzle's **GameObject** on the lever; both also fall back to `GetComponentInParent`, so a room authored before the field existed still resolves.
- `SelectorSwitch` — a rotary selector with labelled positions, wrapping on each interact. Knows nothing about any puzzle; it reports its index and the room decides what that means. Intended for the Radar knobs, the Radio dial and the Trajectory overlays, not just Identification. `OnSelectionChanged` fires **only on player input**, never on init or reset, which is what lets a room tell an untouched panel from a chosen answer.
- `RadarScopeDisplay` — authored clutter that the dials genuinely filter, so a knob visibly *does* something. Each contact declares which position of each dial reveals it; a dial may have one wildcard position that filters nothing (`CONTINUOUS` / `ALL` / `RAW`, all at index 0, all where the dials start — so the room opens showing full clutter and narrowing it reads as progress). It gates nothing: the answer still lives in the puzzle's combination, and the two are separate data, so use the `Log Contact Matrix` context menu to check the correct combination is one that isolates a single contact.
- Guard the confirm lever so an idle pull can't seal a room the player hasn't attempted — the generators require full power, Identification requires at least one switch moved.
- **Dial option order must match the order the room's chart or guide lists them.** Both the Identification `ALTITUDE` dial and its chart were reordered on 2026-07-26 for this reason: the player reads down the chart while clicking through the dial, and a mismatch makes a solvable room feel random.

**World-space TMP faces its local −Z.** Any `TextMeshPro` a player reads while standing on the room's +Z side needs `localEulerAngles = (0, 180, 0)` or it renders mirrored. World font sizes are around 0.3–1.0, not the 20–40 of UI text.

### Wrong answers are refused, not filed

Changed 2026-07-26, and it reverses the earlier rule that a confirmed wrong answer sealed the room and printed a misleading card. **A player can no longer carry a wrong reading forward.** `SwitchComboPuzzle.rejectWrongAnswers` (default **on**) makes a wrong combination a refusal: the machine states what it thinks it is looking at, the panel locks for `rejectHoldSeconds`, then resets itself to its starting positions and the player tries again. The clock is the only cost.

- **`Confirm()` returns `PuzzleCard?`.** Null means *nothing was filed* — a refusal, or an attempt that wasn't fileable. `ConfirmLever` prints only on a value. A sentinel card here would be indistinguishable from a real one.
- **`OnResolved` no longer means "the player is done with this room"** for a rejecting room; it means solved. Anything that must react to a failure subscribes to the new `IPuzzleResolution.OnAttemptRejected` instead — `PuzzleResolutionAudio` does, or failing would be silent and silence reads as a broken lever.
- **`requireAnyChange` still matters.** After a refusal the panel is back at its start position with `hasPlayerSetAnySwitch` cleared, so the player must actually move something before the lever will fire again.
- **`PuzzleConfig.incorrectOutput` is dormant, not dead.** It is only reachable with `rejectWrongAnswers` off. Keep authoring it — it is the opt-out for a room that wants the wrong-card ending — but it no longer appears in normal play.
- **Author the clues so only the right answer is reachable.** With refusals in play, an ambiguous room is no longer a wrong-but-plausible card, it is a player stuck at a lever burning clock. The evidence in the room must name every criterion the answer needs.
- **Every room refuses now.** `GeneratorPuzzle` has its own `Confirm()` and its own copy of the flow (a misfire drops the generators to idle after `rejectHoldSeconds`, default 1.5s — shorter than the knob rooms, because the respin is already the cost). A new room built on `SwitchComboPuzzle` inherits it for free; a room with a bespoke `Confirm()` has to implement it, and the two existing ones are the pattern to copy.

### Doors: passcode only

Settled 2026-07-26, replacing the three competing access models that used to coexist here (arrow-sequence combo locks, the global countdown, and passcodes). **The passcode is the only gate.** Don't reintroduce a clock check on a new room's door.

- `DoorLockController.isInteractable` defaults to **true** in `Awake` — every keypad is reachable from the first frame. `CanUnlockAtCurrentTime()` is deleted; nothing replaces it.
- `LockManager` is now just a lock registry plus the Editor-only dev hack keys (**1-4** toggle interactable, **Shift+1-4** force unlock). It no longer subscribes to `WatchManager.OnElapsedMinuteChanged` and has no `EvaluateLocks()`.
- `RoomConfig.actualUnlockTimeMinutes` and `expectedUnlockTimeMinutes` are **retained but inert** — kept so the timed design can be restored without re-authoring every asset, and read by nothing. `correctPasscode` is the live field. Worth knowing: the "lying plates" discrepancy was never actually authored — all four `Room*Config` assets shipped with actual == expected.
- **A `RoomConfig` with an empty `correctPasscode` is now a permanently sealed door.** The passcode is the single point of failure; there is no longer a time path that opens it anyway. Current codes: Room1 `1492`, Room2 `2589`, Room3 `3821`, Room4 `4710`.
- `DoorComboLock` (the Helldivers-style arrow sequence) is still in the repo but is **not** the access model. Don't build against it without asking.

### Time skip: hold Q to run the clock at 4x

Built 2026-07-26 for the timed doors, and kept after they were cut because it costs nothing to leave in — but it is now a **playtest tool, not a mechanic**. With doors passcode-only, holding Q only burns the player's clock with nothing to gain.

- Rides the existing `Watch` action (Q / gamepad north), which previously tilted the camera at a wrist that has no watch model. `CameraController.SetWatchViewPose` is now unused, left in for a future real watch.
- `WatchManager.TimeScale` multiplies the tick rather than jumping via `ReduceTime()`. Keep it that way if anything ever re-subscribes to the minute events: a jump can step straight over a minute boundary without raising `OnMinuteChanged` / `OnElapsedMinuteChanged`.
- `PlayerController` force-clears fast-forward in `SetInputEnabled(false)` — the keypad deliberately doesn't pause the countdown, so a held key behind a modal would otherwise burn clock with no way to release it.
- `ClockHudView` (screen-space MM:SS) and `PlayerSmokeEffect` (cigarette smoke while accelerating) exist but are **wired into no scene**. `Tools → The Hot War → Setup Time Skip (Clock HUD + Smoke)` builds and wires both; it is idempotent. Until then the only on-screen clock is the green debug box from `VictoryLoseManager.DrawGUI`.

### Doors: two mechanisms, pick by ownership

- [SlidingDoor.cs](Assets/Scripts/Puzzles/SlidingDoor.cs) — the room owns its own panel. Slides along the panel's **own** local X (`transform.right`), not `localPosition.x`, which follows the parent's axes and ignores the panel's rotation.
- [PuzzleDoorOpener.cs](Assets/Scripts/Puzzles/PuzzleDoorOpener.cs) — the doorway already has someone else's `RoomDoor`. Opens that instead of adding a second leaf.

Both hang off `IPuzzleResolution.OnResolved`, which fires when an answer is **filed**. As of 2026-07-26 a wrong answer is no longer filed at all (see [Wrong answers are refused, not filed](#wrong-answers-are-refused-not-filed)), so for a room with `rejectWrongAnswers` on, resolution means *solved* and the door opens only on a correct reading. `PuzzleDoorOpener.requireCorrectAnswer` is therefore moot for those rooms — leave it off.

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

**When MCP is also unavailable, `dotnet build` is the third option** and the only one that works while the Editor is open:

```powershell
# Real compile check, no project lock, ~35s. Output goes to the gitignored Temp/Bin/Debug/.
dotnet build Assembly-CSharp.csproj -nologo -v q -clp:ErrorsOnly
```

This was needed on 2026-07-26 when the MCP bridge wedged (`Command TCS timed out (N consecutive)` in the Editor log) while the Editor itself stayed open and responsive. Two gotchas: the csproj may predate a brand-new script, so confirm your file is in its `<Compile Include=...>` list before trusting a green build; and it verifies **code only**, never scene or prefab wiring.

### MCP for Unity

`com.coplaydev.unity-mcp` (10.1.0, from the `package.openupm.com` scoped registry) is installed, and a **`unity-mcp-skill`** is available. Use it for anything touching the live Editor — creating GameObjects, wiring serialized references, reading console errors after a script change, entering play mode. It is faster and far more reliable than editing `.unity`/`.prefab` YAML by hand. **Never hand-edit scene or prefab YAML** when an MCP call can do it.

## Level geometry — `HotWarLevelKit`

[HotWarLevelKit.cs](Assets/assets/HotWarLevelKit.cs) (namespace `TheHotWar`) is a declarative, auto-rebuilding level generator. You specify *what* each room is — name, `size` (width × depth), `shape` (Rectangle/LShape), `feature` (None / PlatformWithStairs / Basement), optional side and gap — and it resolves *where* everything goes: corridor placement (staggered left/right, auto-lengthening), door openings, which walls are shared and omitted, L-notch position, stairs, basement stairwells and floor holes.

Two conventions matter more than anything else in that file:

- **`HOTWAR_ROOMS` is disposable output.** `RebuildLevel()` destroys and regenerates the whole root. Never put hand-authored work or gameplay components inside it.
- **`HOTWAR_PROPS` is hand-polish and is never touched by a rebuild.** Anything authored by hand — real props, gameplay scripts, spawn points — goes here or in a separate root.

`OnValidate` triggers a debounced rebuild (`rebuildDelay`, default 0.35s) on every Inspector change while `autoRebuild` is on. Context-menu entries: `Rebuild Level`, `Load Doc Layout` (the canonical 4-room layout; its `realUnlockElapsed` / `shownUnlockElapsed` arrays are leftovers of the cut timed-door design and paint plates nothing reads), `Append Example Rooms`, `Spawn Doc Control-Room Props`, `Apply Mood Lighting`, `Clear Rooms`, `Clear Props`.

Internally it compiles specs into `InternalRoom` via a door-relative `Frame` (u = across the door wall, v = away from the door), so feature placement logic is written once and mapped to whichever wall the door lands on. Output is plain primitives with no scripts attached.

## Architecture

Five feature modules, detailed in [overview.md](overview.md) with the full class diagram in [architecture.mermaid](architecture.mermaid). The load-bearing patterns:

- **Thin MonoBehaviour / composed facade.** `PlayerController` owns `PlayerMovement`, `CameraController`, and `PlayerInteraction` as sub-controllers and exposes the one-way `IsControlRoomDeparted` flag. `WatchManager` and `PuzzleTracker` are plain C# controllers, not MonoBehaviours — keep them scene-independent and unit-testable.
- **`IInteractable` is the universal interaction contract** (`Interact(PlayerController)`, `GetPrompt()`). `PlayerInteraction` raycasts for it and fires `OnInteractableTargetChanged`; both `CrosshairUI` and `InteractionPromptUI` subscribe to that one event rather than polling.
- **Per-door state, not a central door manager.** Each door GameObject carries its own `DoorLockController` + `RoomConfig` ScriptableObject. `ValidateCode()` checks the passcode and is the only thing that opens a door — see [Doors: passcode only](#doors-passcode-only). `RoomDoor` is purely the visual/pivot view owned by the lock controller.
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
