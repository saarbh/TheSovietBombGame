# The Hot War — Technical Architecture Specification

## 1. Architectural Philosophy & Feature Module Design
This document outlines the software architecture for **The Hot War**. The codebase is structured into **5 Feature Modules**, adhering to **SOLID principles**, **Model-View-Controller (MVC)**, and **Thin MonoBehaviour Architecture**:

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                           FEATURE MODULE OVERVIEW                               │
├───────────────────┬───────────────────┬───────────────────┬─────────────────────┤
│ 1. Player System  │ 2. Time & Watch   │ 3. Door & Lock    │ 4. Puzzle & Endings │
│                   │    System         │    System         │    & Phone System   │
│ - PlayerController│ - WatchManager    │ - RoomConfig      │ - GameManager       │
│ - PlayerMovement  │ - WorldWatchView  │ - RoomDoor        │ - BasePuzzle<T>     │
│ - CameraController│ - SkeletonTrigger │ - DoorLockControl │ - PuzzleTracker     │
│ - Interaction     │ - BabushkaDoll    │ - KeypadPopupUI   │ - PuzzleTracker     │
│ - ControlRoomTrig │                   │                   │ - PhoneInteractable │
└───────────────────┴───────────────────┴───────────────────┴─────────────────────┤
│ 5. Audio & UI Presentation: AudioManager (3 enum-indexed arrays),               │
│    UIManager, CrosshairUI, InteractionPromptUI, CutscenePlayer (Timeline),      │
│    MainMenuUI, PauseMenuUI                                                      │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Detailed Feature Module Breakdown

### 2.1 Feature Module 1: Player System (`Unity New Input System + SRP`)
* **Player Input Binding:** Leverages Unity's **New Input System (`PlayerInput` component in Inspector)**. Sub-controllers receive input callbacks directly via Inspector bindings.
* **`PlayerController`** (`<<MonoBehaviour - Gameplay Scene>>`): Scene facade orchestrating movement, camera look, interaction, and exposing `public bool IsControlRoomDeparted { get; private set; }`.
* **`PlayerMovement`** (`<<MonoBehaviour - Sub-Controller>>`): WASD / Left-stick movement, gravity, and `CharacterController`.
* **`CameraController`** (`<<MonoBehaviour - Sub-Controller>>`): First-person mouse look / Right-stick rotation, pitch/yaw limits, and wrist-watch view poses.
* **`PlayerInteraction`** (`<<MonoBehaviour - Sub-Controller>>`): Raycasting and crosshair detection for `IInteractable`. Fires `OnInteractableTargetChanged`.
* **`IInteractable`** (`<<interface>>`): Contract for interactive objects (`Interact()`, `GetPrompt()`).
* **`ControlRoomTrigger`** (`<<MonoBehaviour - Gameplay Scene>>`): Trigger collider on the control room exit. When the player walks through (`OnTriggerExit`), it sets `PlayerController.IsControlRoomDeparted = true`. One-way flag — once departed, cannot be unset.

---

### 2.2 Feature Module 2: Time & Watch System (`7-Minute Constant Countdown`)
* **`WatchManager`** (`<<Controller - Plain C# / UniTask>>`): Countdown engine initialized to **`TOTAL_TIME_SECONDS = 420f` (7 minutes)**. Runs via `StartCountdownAsync(CancellationToken)` UniTask loop. Dispatches `OnTimeUpdated`, `OnMinuteChanged`, and `OnTimeExpired`.
* **`WorldWatchView`** (`<<MonoBehaviour - Gameplay Scene>>`): 3D wrist watch visual view subscribing to `WatchManager`.
* **`ITimerModifier`** (`<<interface>>`): Contract for objects that alter the game countdown.
* **`SkeletonTrigger`** (`<<MonoBehaviour - Gameplay Scene>>`): Hallway trigger granting bonus time (`AddTime`).
* **`BabushkaDoll`** (`<<MonoBehaviour - Gameplay Scene>>`): Implements both `IInteractable` and `ITimerModifier`. Increases global countdown (`AddTime`).

---

### 2.3 Feature Module 3: Door & Lock System (`Time-Gated Passcode Access`)
* **`RoomConfig`** (`<<ScriptableObject>>`): Stores `roomId`, actual/expected unlock minutes, and passcode.
* **`DoorLockController`** (`<<MonoBehaviour - Gameplay Scene, per door>>`): Lives on each door GameObject. Has its own `[SerializeField] RoomConfig` and reference to `WatchManager`. Implements `IInteractable`. Handles:
  - **Time-gate check:** `CanUnlockAtCurrentTime()` — if current elapsed time is before the door's `actualUnlockTimeMinutes`, interaction is blocked (prompt shows locked message, keypad never opens).
  - **Code validation:** `ValidateCode(string)` — checks entered code against `RoomConfig.correctPasscode`.
* **`RoomDoor`** (`<<MonoBehaviour - Gameplay Scene>>`): Purely the physical door view — pivot animation, open/close state. Owned by `DoorLockController`.
* **`KeypadPopupUI`** (`<<MonoBehaviour - Gameplay Scene>>`): 2D modal popup for entering passcodes. Only opens when `DoorLockController.CanUnlockAtCurrentTime()` returns true.

---

### 2.4 Feature Module 4: Generic Puzzles, Phone System & Ending Evaluation

#### Generic Puzzle Validation (`BasePuzzle<T>` & `CheckSolve()`)
* **`IPuzzle` Interface**: Defines `bool CheckSolve()`, `InitializePuzzle()`, `ResetPuzzle()`, and `OnPuzzleSolved`.
* **`BasePuzzle<T>`** (`<<abstract MonoBehaviour>>`): Abstract generic base class comparing `T currentState` against `T targetState` inside `CheckSolve()` via `EqualityComparer<T>.Default.Equals(currentState, targetState)`. Concrete subclasses (e.g. `class RadarPuzzle : BasePuzzle<float>`) are non-generic and can be attached to GameObjects.
* **`PuzzleTracker`** (`<<Controller - Plain C#>>`): Tracks completion of **`TOTAL_ROOM_PUZZLES = 4`** room puzzles.

#### Phone System (`PhoneInteractable` & `PhoneChoiceUI`)
* **`PhoneInteractable`** (`<<MonoBehaviour - Gameplay Scene>>`): Physical phone object implementing `IInteractable`. Queries `GameManager.AreAllPuzzlesSolved()` to determine available choices:
  - If **all 4 puzzles solved:** Player can choose `ReportFalseAlarm` or `ReportIncomingNuke`.
  - If **any puzzle unsolved:** Only `ReportIncomingNuke` is available.
* **`PhoneChoiceUI`** (`<<MonoBehaviour - Gameplay Scene>>`): 2D modal presenting the available `PhoneCallChoice` options. On selection, triggers `GameManager.EvaluateEnding()`.

#### Ending Decision Matrix (Inside `GameManager`)
* **`GameManager`** (`<<MonoBehaviour - Scene Singleton>>`): Lives in `BunkerGameScene` (destroyed on scene reset). Owns all ending evaluation logic internally:
  - `AreAllPuzzlesSolved()` — Public facade over `PuzzleTracker`.
  - `EvaluateEnding(bool leftControlRoom, PhoneCallChoice callChoice)` — Determines `EndingType` based on departure status, phone choice, and puzzle completion.
  - `OnTimeExpiredHandler()` — Subscribed to `WatchManager.OnTimeExpired` to trigger timeout evaluation.

```
                              ┌──────────────────────────────────────┐
                              │            END-GAME EVALUATION       │
                              └──────────────────┬───────────────────┘
                                                 │
                   ┌─────────────────────────────┴─────────────────────────────┐
                   ▼                                                           ▼
        [VICTORY: WorldSaved]                                       [DEFEAT: NuclearWar]
   1. Stay in Control Room & Don't Call                      1. Leave Control Room & Don't Call
   2. Solve 4 Puzzles & Don't Call                           2. Fail Puzzles -> Only Call Option
   3. Solve 4 Puzzles & Call (Report False Alarm)               is "Report Nuke Incoming"
                                                             3. Solve 4 Puzzles -> Choose
                                                                "Report Nuke Incoming"
```

---

### 2.5 Feature Module 5: Audio Manager & UI Presentation

#### Audio System (`3 Enum-Indexed Arrays`)
* **`AudioManager`** (`<<MonoBehaviour - Persistent Singleton>>`): Persistent sound manager with **3 separate `AudioSource` channels** and **3 enum-indexed `AudioClip[]` arrays**:
  - `SFXType` enum → `AudioClip[] sfxClips` → `sfxSource` (one-shot effects: `DoorUnlock`, `KeypadPress`, `WatchBeep`, `TypewriterChar`, `PuzzleSolved`, `PhoneRing`, `SkeletonDrop`)
  - `BGMType` enum → `AudioClip[] bgmClips` → `bgmSource` (looping ambient: `BunkerAmbient`, `TensionLoop`, `SilenceLoop`)
  - `MusicType` enum → `AudioClip[] musicClips` → `musicSource` (cutscene/ending tracks: `OpeningCutscene`, `EndingVictory`, `EndingNuclearWar`)

#### Crosshair & Interaction Prompt
* **`CrosshairUI`** (`<<MonoBehaviour - Gameplay Scene>>`): Persistent center-screen reticle. Listens to `PlayerInteraction.OnInteractableTargetChanged` and swaps between `defaultCrosshair` and `interactCrosshair` sprites when targeting an interactable object.
* **`InteractionPromptUI`** (`<<MonoBehaviour - Gameplay Scene>>`): Text tooltip below the crosshair showing `target.GetPrompt()` (e.g. `"[E] Open Door"`, `"[E] Pick Up Phone"`).

#### Cutscene System (Unity Timeline)
* **`CutscenePlayer`** (`<<MonoBehaviour - Gameplay Scene>>`): Wraps Unity's `PlayableDirector` with serialized `PlayableAsset` references for:
  - `openingTimeline` — Intro cutscene on game start.
  - `victoryTimeline` — World Saved ending.
  - `defeatTimeline` — Nuclear War ending.
  - Fires `OnCutsceneFinished` when playback completes.

#### Menu System
* **`MainMenuUI`** (`<<MonoBehaviour - Main Menu Scene>>`): Play and Quit buttons. `OnPlayPressed()` loads the `BunkerGameScene`.
* **`PauseMenuUI`** (`<<MonoBehaviour - Gameplay Scene>>`): In-game pause overlay with Resume, Restart, and Main Menu buttons. Toggles `Time.timeScale` and cursor lock state.

#### General UI
* **`UIManager`** (`<<MonoBehaviour - Gameplay Scene>>`): Coordinates all in-game UI overlays — keypad modal, phone choice modal, interaction prompt, crosshair, and cutscene triggers.
* **`WorldSpaceTerminal`** (`<<MonoBehaviour - Gameplay Scene>>`): 3D `TextMeshPro` on in-world computer monitors displaying diegetic readouts.
* **`TypewriterEffect`** (`<<MonoBehaviour - Gameplay Scene>>`): Asynchronous retro text streaming utility.

---

## 3. Scene Ownership Summary

```
┌───────────────────────────────────────────────────────────────────────────────┐
│                         PERSISTENT (DontDestroyOnLoad)                        │
│  AudioManager (Persistent Singleton)                                          │
└───────────────────────────────────────┬───────────────────────────────────────┘
                                        │
          ┌─────────────────────────────┼─────────────────────────────┐
          ▼                             ▼                             ▼
┌─────────────────────┐   ┌──────────────────────────┐   ┌──────────────────┐
│   MAIN MENU SCENE   │   │   BUNKER GAME SCENE      │   │  (Destroyed on   │
│                     │   │                          │   │   scene reload)  │
│  - MainMenuUI       │   │  - GameManager (Singleton)│   │                  │
│                     │   │  - PlayerController       │   │  GameManager     │
│                     │   │  - PlayerMovement         │   │  PuzzleTracker   │
│                     │   │  - CameraController       │   │  WatchManager    │
│                     │   │  - PlayerInteraction      │   │                  │
│                     │   │  - WorldWatchView         │   └──────────────────┘
│                     │   │  - ControlRoomTrigger     │
│                     │   │  - DoorLockController (x4)│
│                     │   │  - RoomDoor (x4)          │
│                     │   │  - BasePuzzle<T> (x4)     │
│                     │   │  - PhoneInteractable      │
│                     │   │  - SkeletonTrigger        │
│                     │   │  - BabushkaDoll           │
│                     │   │  - WorldSpaceTerminal(s)  │
│                     │   │  - UIManager              │
│                     │   │  - KeypadPopupUI          │
│                     │   │  - PhoneChoiceUI          │
│                     │   │  - InteractionPromptUI    │
│                     │   │  - CrosshairUI            │
│                     │   │  - CutscenePlayer         │
│                     │   │  - PauseMenuUI            │
│                     │   │  - TypewriterEffect       │
└─────────────────────┘   └──────────────────────────┘
```

---

## 4. Standalone Class Diagram

The complete architecture class diagram is maintained in:
👉 **[architecture.mermaid](./architecture.mermaid)**

---

## 5. Development Task Breakdown by Feature Module

| Feature Module | Primary Deliverables | Key Patterns & Technologies | Assignee |
|---|---|---|---|
| **Module 1: Player System** | `PlayerController`, `PlayerMovement`, `CameraController`, `PlayerInteraction`, `ControlRoomTrigger` | Unity Inspector `PlayerInput` Bindings, Trigger Collider | **Shery / Team** |
| **Module 2: Time & Watch** | `WatchManager` (7-min const, UniTask), `WorldWatchView`, `SkeletonTrigger`, `BabushkaDoll` | Diegetic 3D View, UniTask Countdown | **Shery** |
| **Module 3: Door & Lock** | `RoomConfig`, `DoorLockController` (per door), `RoomDoor`, `KeypadPopupUI` | ScriptableObjects, Time-Gated Passcode | **Shery / Shai** |
| **Module 4: Puzzles, Phone & Endings** | `GameManager` (Scene Singleton, owns ending evaluation), `BasePuzzle<T>` (`CheckSolve()`), `PuzzleTracker`, `PhoneInteractable`, `PhoneChoiceUI` | Generic `T` validation, Phone choice flow, 2-Ending matrix | **Shery / Team** |
| **Module 5: Audio & UI** | `AudioManager` (3 enum arrays), `UIManager`, `CrosshairUI`, `InteractionPromptUI`, `CutscenePlayer` (Timeline), `MainMenuUI`, `PauseMenuUI` | 3-Channel Audio, Unity Timeline, Crosshair Swap | **Shai** |
