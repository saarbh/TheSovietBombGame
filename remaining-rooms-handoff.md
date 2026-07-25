# Handoff: the remaining puzzle rooms

**Owner of this task:** whoever picks up rooms 4 and 5.
**Written:** 2026-07-26, against branch `audio-and-fixes`.
**Status of the world:** 3 of the 5 essential rooms are built and play-tested. Rooms 4 and 5 exist
as empty shells in the level with open doorways. All the shared machinery you need already exists
and is play-tested — **the two remaining essential rooms need almost no new C#.**

Read [CLAUDE.md](CLAUDE.md) and [SKILL.md](SKILL.md) before writing any code. Everything below
assumes those rules.

---

## 1. What is left

| # | Room | Stage | Card char | Where it goes | New logic needed |
| --- | --- | --- | --- | --- | --- |
| 4 | **Radio Authentication** | `Authenticate` (4) | `K` | Level room 4 | One ~50-line display script (provided below) |
| 5 | **Records Archive** | `Authorize` (5) | `R` | Level room 5 | **None** |
| 6 | Trajectory Plotting *(stretch)* | `Trace` (3) | `7` | not built | None |
| 7 | Telephone Routing *(stretch)* | `Report` (6) | `M` | not built | None |

Already done, for reference: Generator (`Confirm` → `9`), Radar (`Detect` → `4`),
Identification (`Classify` → `2`).

Production priority from the design doc (p16) puts **radio before archive**, so do room 4 first.

---

## 2. The machinery you are plugging into

Read these four files once, in this order. It will take ten minutes and save you a day.

1. [SwitchComboPuzzle.cs](Assets/Scripts/Puzzles/SwitchComboPuzzle.cs) — the shared spine of every
   dial-a-combination room. Set N switches to one correct combination, pull a lever, a card prints.
2. [SelectorSwitch.cs](Assets/Scripts/Puzzles/SelectorSwitch.cs) — a rotary selector with labelled
   positions. Wraps around on each interact. Knows nothing about any puzzle.
3. [BasePuzzle.cs](Assets/Scripts/Puzzles/BasePuzzle.cs) — generic base. Owns solved/resolved state,
   files the evidence card, registers with the tracker.
4. [PuzzleConfig.cs](Assets/Scripts/Puzzles/PuzzleConfig.cs) — the ScriptableObject holding a room's
   identity, stage, and its two output cards (correct and wrong).

### The one paragraph that matters

`SwitchComboPuzzle` already implements everything a room needs: reading the switches, comparing
against the authored answer, guarding the confirm lever, locking the panel, printing the card,
firing `OnResolved`. [RadarPuzzle.cs](Assets/Scripts/Puzzles/Radar/RadarPuzzle.cs) and
[IdentificationPuzzle.cs](Assets/Scripts/Puzzles/Identification/IdentificationPuzzle.cs) are each
**three lines long** — they exist only so the room has its own component type and its own console
log prefix. Both remaining essential rooms are the same shape. If you find yourself writing puzzle
logic, stop and check whether authored data would do it instead.

```csharp
/// <summary>
/// The Radio Authentication Room (AUTHENTICATION). ...
/// </summary>
public class RadioPuzzle : SwitchComboPuzzle
{
    protected override string LogTag => "Radio";
}
```

### Free parts you do not have to build

| Component | What it gives you |
| --- | --- |
| [ConfirmLever.cs](Assets/Scripts/Puzzles/ConfirmLever.cs) | The commit lever. Serialize the puzzle's **GameObject** in `Puzzle Object`. Set `Confirm Verb` to the room's verb (e.g. `Authenticate`, `Stamp`). Optionally wire `Printout Display` (a TMP) and `Printed Card` (a GameObject revealed on print). |
| [ResetLever.cs](Assets/Scripts/Puzzles/ResetLever.cs) | Free retries. Same `Puzzle Object` field, plus `Reset Noun` for the prompt. |
| [PuzzleResolutionAudio.cs](Assets/Scripts/Audio/PuzzleResolutionAudio.cs) | The verdict sting on solve *and* on failure. Sits on the room root, `Puzzle Object` = the puzzle. |
| [PuzzleDoorOpener.cs](Assets/Scripts/Puzzles/PuzzleDoorOpener.cs) / [SlidingDoor.cs](Assets/Scripts/Puzzles/SlidingDoor.cs) | Exit-door behaviour, if the room owns a door. **See §7 — coordinate before touching doors.** |
| [InteractionSfx.cs](Assets/Scripts/Audio/InteractionSfx.cs) | Per-object interact sound. Optional; everything is audible without it. |

**Evidence cards need zero wiring.** `BasePuzzle.MarkResolved` builds the card from the config and
files it into `GameManager.CardInventory` *before* `OnResolved` fires. You do not write card code.

---

## 3. Room 4 — Radio Authentication

### Design intent (design doc p11, authoritative)

> The player rotates a radio dial between several channels. Most contain distractions: music,
> static, a weather report, two operators arguing, a recorded emergency message.
>
> The genuine military signal uses a specific authentication pattern listed on a wall card:
> *"Every authenticated broadcast begins with three descending tones and ends with today's
> challenge number."*
>
> The player must find the correct transmission and enter its final digit into a keypad.
>
> For accessibility, show the tones on a small waveform or flashing-light display rather than
> relying only on audio.
>
> **Result:** `AUTHENTICATION — K` / *"Signal authentication failed."*
> The supposed launch warning lacks the correct daily code.
>
> **Comedy:** one channel says *"This is not the emergency channel. Please stop calling this
> frequency."* Another contains a bunker-approved canned-beet advertisement.

The card's evidence line is the joke: the room "succeeds" by proving the warning **failed**
authentication.

### Recommended build: two switches, no keypad

Do **not** try to reuse [KeypadPopupUI.cs](Assets/Scripts/UI/KeypadPopupUI.cs) — it is welded to
`DoorLockController` (it subscribes to `DoorLockController.OnAnyLockInteracted` and evaluates door
passcodes). Wiring a second modal keypad is a day of work for no gain, and a modal breaks the
diegetic feel of the other rooms.

Instead, the "keypad" is a second `SelectorSwitch`. The room becomes a two-switch combo:

| Switch | `Switch Label` | Options | Meaning |
| --- | --- | --- | --- |
| 1 | `CHANNEL` | `1`, `2`, `3`, `4`, `5` | Which frequency the dial is on |
| 2 | `CHALLENGE` | `0` … `9` | The digit the player read off the authenticated broadcast |

`Correct Option Indices` = `[3, 7]` → channel 4 is the genuine signal, and its challenge number
is 7. Change to taste, but keep the two arrays the same length — `OnValidate` warns if you don't.

Because `requireAnyChange` defaults to **true**, a player who walks in and yanks the lever without
touching anything is refused with *"Set the switches first"*. Leave that on.

### The one new script you need

Channels have to be *readable* — a transcript panel plus the accessibility waveform. This is the only
genuinely new code in the room. Create `Assets/Scripts/Puzzles/Radio/RadioChannelDisplay.cs`:

```csharp
using TMPro;
using UnityEngine;

/// <summary>
/// Drives the radio's transcript panel and its tone display from whatever channel the dial is on.
/// Purely presentation: the answer lives in the RadioPuzzle's authored combination, and this
/// component never decides anything - it only shows the player what they are listening to.
///
/// Per the design doc the three descending tones must be VISIBLE, not just audible, so each
/// channel carries its own indicator object rather than relying on the clip alone.
/// </summary>
public class RadioChannelDisplay : MonoBehaviour
{
    [System.Serializable]
    private class Channel
    {
        [Tooltip("One or two sentences. Players do not have time to read paragraphs.")]
        [TextArea]
        public string transcript;

        [Tooltip("Optional. Shown only while this channel is tuned - the three-descending-tones "
                 + "waveform for the genuine signal, nothing for the distractions.")]
        public GameObject toneIndicator;

        [Tooltip("Optional. Looping audio for this channel.")]
        public AudioSource loop;
    }

    [Header("Source")]
    [Tooltip("The CHANNEL dial. This component listens; it never moves it.")]
    [SerializeField] private SelectorSwitch channelDial;

    [Header("Display")]
    [SerializeField] private TMP_Text transcriptDisplay;

    [Tooltip("One entry per dial option, in the same order.")]
    [SerializeField] private Channel[] channels = System.Array.Empty<Channel>();

    private void OnEnable()
    {
        if (channelDial == null)
        {
            Debug.LogError("[Radio] RadioChannelDisplay has no channel dial; the panel will stay blank.", this);
            return;
        }

        channelDial.OnSelectionChanged += HandleChannelChanged;
    }

    private void OnDisable()
    {
        if (channelDial == null)
        {
            return;
        }

        channelDial.OnSelectionChanged -= HandleChannelChanged;
    }

    // Start, not OnEnable: SelectorSwitch sets its own starting position in Awake, and
    // OnSelectionChanged deliberately does NOT fire for that, so the opening state has to be
    // pushed once by hand or the panel reads blank until the player touches the dial.
    private void Start()
    {
        Refresh();
    }

    private void HandleChannelChanged(SelectorSwitch changed)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (channelDial == null)
        {
            return;
        }

        var tuned = channelDial.CurrentIndex;

        for (var i = 0; i < channels.Length; i++)
        {
            var channel = channels[i];

            if (channel == null)
            {
                continue;
            }

            var isTuned = i == tuned;

            if (channel.toneIndicator != null)
            {
                channel.toneIndicator.SetActive(isTuned);
            }

            if (channel.loop != null)
            {
                if (isTuned && !channel.loop.isPlaying)
                {
                    channel.loop.Play();
                }
                else if (!isTuned && channel.loop.isPlaying)
                {
                    channel.loop.Stop();
                }
            }
        }

        if (transcriptDisplay == null)
        {
            return;
        }

        transcriptDisplay.text = tuned >= 0 && tuned < channels.Length && channels[tuned] != null
            ? channels[tuned].transcript
            : string.Empty;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // A short channel list silently makes the last dial positions read blank, which looks
        // like a dead radio rather than an authoring mistake.
        if (channelDial != null && channels.Length != channelDial.OptionCount)
        {
            Debug.LogWarning(
                $"[Radio] '{name}' has {channels.Length} channels but the dial has "
                + $"{channelDial.OptionCount} positions. They must line up one-to-one.", this);
        }
    }
#endif
}
```

### Suggested channel content

| Ch | Transcript | Tone indicator |
| --- | --- | --- |
| 1 | Military band music, mid-march. | — |
| 2 | Static. | — |
| 3 | *"…scattered cloud over the eastern oblast, no precipitation expected."* | — |
| 4 | **Three descending tones.** *"AUTHENTICATED TRANSMISSION. STAND BY. CHALLENGE NUMBER SEVEN."* | ✅ waveform |
| 5 | *"This is not the emergency channel. Please stop calling this frequency."* | — |

Keep the beet advertisement as a sixth channel if you have time; it is the funniest line in the
section and costs one array entry.

### PuzzleConfig asset — `Assets/Configs/RadioRoomConfig.asset`

Create it via **Assets → Create → SovietBomb → Puzzle Config**. Fill in:

| Field | Value |
| --- | --- |
| `Puzzle Id` | `radio_room` |
| `Puzzle Name` | `Radio Authentication Room` |
| `Preserve Progress On Exit` | ✅ |
| **`Stage`** | **`Authenticate`** ← the field that orders the final code. Get this wrong and nothing else matters. |
| `Stage Label` | `AUTHENTICATION` |
| Correct → `Code Character` | `K` |
| Correct → `Evidence` | *Signal authentication failed. The supposed launch warning lacks the correct daily challenge code.* |
| Incorrect → `Code Character` | `X` (anything, as long as it is **not** `K`) |
| Incorrect → `Evidence` | *Transmission authenticated. Daily challenge code matches the launch order.* |

The wrong-answer evidence must read as **confidently wrong** and support the launch warning — that
is the tone of the whole game. `OnValidate` on the config will shout at you if the two characters
match, or if the label and the stage disagree.

---

## 4. Room 5 — Records Archive

### Design intent (design doc p12, authoritative)

> The player finds three short documents: a **radar maintenance record**, a **launch warning
> report**, and an **officer duty log**. They must place them into three chronological slots.
>
> The contradiction:
> - Radar maintenance started at **14:00**.
> - The launch warning supposedly came from that radar at **14:03**.
> - The report was signed at **13:57**.
>
> The player selects or stamps the impossible report.
>
> **Result:** `AUTHORIZATION — R` / *"Report prepared before detection."*
> This strongly suggests the warning was part of a scheduled exercise.
>
> **Comedy:** one document requires *"Form 12-B: Authorization to acknowledge receipt of Form 12-A."*
>
> **Keep each document to one or two sentences. Players do not have time to read paragraphs.**

### Recommended build: three slot switches, zero new code

This is an ordering puzzle, which is exactly a combination in disguise. One `SelectorSwitch` per
slot; its options are the three document names. The player dials each slot to the document that
belongs in it, then **stamps** (the confirm lever with `Confirm Verb` = `Stamp`).

Options list, identical on all three switches and **in this order**:

```
0 — RADAR MAINTENANCE RECORD   (14:00)
1 — LAUNCH WARNING REPORT      (14:03)
2 — OFFICER DUTY LOG           (13:57, when the report was signed)
```

| Switch | `Switch Label` | Correct option |
| --- | --- | --- |
| 1 | `SLOT 1 — EARLIEST` | `2` (duty log, 13:57) |
| 2 | `SLOT 2` | `0` (maintenance, 14:00) |
| 3 | `SLOT 3 — LATEST` | `1` (warning, 14:03) |

So `Correct Option Indices` = **`[2, 0, 1]`**.

Note the deliberate simplification: nothing stops the player putting the same document in two
slots. That is fine — only one combination is correct, and a jam player who does that gets a wrong
verdict and a wrong card, which is a supported outcome. Do not build permutation validation.

If you want the doc's explicit *"stamp the impossible report"* beat, add a fourth switch
`FLAG AS IMPOSSIBLE` with the same three options, correct index `1` (the launch warning report —
it cites a radar that was already off, and was signed before the detection). Four switches instead
of three, still zero new code.

### The documents

Three world-space props with a TMP face each, one or two sentences. Suggested wording:

- **Radar maintenance record** — *"Unit 4 radar array taken offline for scheduled maintenance,
  14:00. Signed: Technician Volkov."*
- **Launch warning report** — *"Launch detected by Unit 4 radar array, 14:03. Escalation
  authorized."*
- **Officer duty log** — *"13:57 — Received completed launch warning report for filing. Requires
  Form 12-B: Authorization to acknowledge receipt of Form 12-A."*

**World-space TMP faces its local −Z.** Any text the player reads while standing on the room's +Z
side needs `localEulerAngles = (0, 180, 0)` or it renders mirrored. World font sizes are around
0.3–1.0, not the 20–40 you use for UI text. This has bitten every room so far.

### PuzzleConfig asset — `Assets/Configs/ArchiveRoomConfig.asset`

| Field | Value |
| --- | --- |
| `Puzzle Id` | `archive_room` |
| `Puzzle Name` | `Records Archive` |
| **`Stage`** | **`Authorize`** |
| `Stage Label` | `AUTHORIZATION` |
| Correct → `Code Character` | `R` |
| Correct → `Evidence` | *Report prepared before detection. The warning was part of a scheduled exercise.* |
| Incorrect → `Code Character` | `B` (anything but `R`) |
| Incorrect → `Evidence` | *Filing order consistent. Report follows detection as procedure requires.* |

And the subclass, `Assets/Scripts/Puzzles/Archive/ArchivePuzzle.cs`:

```csharp
public class ArchivePuzzle : SwitchComboPuzzle
{
    protected override string LogTag => "Archive";
}
```

---

## 5. Build recipe — copy the room that already works

Do not author a room from scratch. Duplicate
[IdentificationPuzzleRoom.prefab](Assets/prefabs/IdentificationPuzzleRoom.prefab) and rename the
pieces. Its hierarchy is the template:

```
IdentificationPuzzleRoom          ← RoomAudioZone + PuzzleResolutionAudio + IdentificationPuzzle
├── Console
│   ├── BackPanel
│   ├── Switch_SPEED              ← SelectorSwitch (+ Dial > Pointer > Tip, Readout_SPEED)
│   ├── Switch_ALTITUDE
│   ├── Switch_HEAT
│   └── VerdictDisplay            ← TMP, the machine's announcement
├── ClassifyLever                 ← ConfirmLever  (Confirm Verb = "Classify")
├── ResetLever                    ← ResetLever
├── Printer
│   ├── PrintoutDisplay           ← TMP, the printed card text
│   └── PrintedCard               ← revealed on print
├── TelemetryBoard > TelemetrySheet   ← the readable evidence
└── ChartBoard > IdentificationChart  ← the rules the evidence is read against
```

Steps, in order:

1. **Duplicate the prefab**, rename to `RadioPuzzleRoom` / `ArchivePuzzleRoom`. Rename the switch
   objects and their readouts.
2. **Write the subclass** (three lines). Swap the `IdentificationPuzzle` component on the root for
   yours, or add yours and remove theirs.
3. **Create the PuzzleConfig asset** and assign it to the puzzle's `Puzzle Config` field.
4. **Re-point the arrays**: `Criteria Switches` (in answer order) and `Correct Option Indices`
   (same length, same order).
5. **Author the switch options** on each `SelectorSwitch`, plus `Switch Label` and `Start Index`.
6. **Re-point the levers**: `Puzzle Object` on both `ConfirmLever` and `ResetLever` → the room root.
   Set `Confirm Verb` (`Authenticate` / `Stamp`) and `Reset Noun` (`dial` / `filing`).
7. **Author the room text**: the wall card / documents / verdict strings
   (`Correct Verdict`, `Incorrect Verdicts[]`).
8. **Audio** — see §6.
9. **Drop one prefab instance** into the scene at the room's coordinates (§7).

Wire all of this with **MCP for Unity** against the live editor (`manage_prefabs`,
`manage_components`, `manage_scriptable_object`). **Never hand-edit `.prefab` or `.unity` YAML** —
it is the single most reliable way to lose a day on this project.

### Wrong-answer verdict strings

`SwitchComboPuzzle` picks from `Incorrect Verdicts[]` **deterministically** — keyed off the panel
itself, so a player who retries the same wrong combination always gets the same answer. Write 3–4:

- Radio: `NO AUTHENTICATION PATTERN DETECTED` / `CHALLENGE NUMBER REJECTED` /
  `CHANNEL NOT MILITARY` / `TRANSMISSION IS A BEET ADVERTISEMENT`
- Archive: `FILING ORDER ACCEPTED` / `CHRONOLOGY IMPOSSIBLE — RESUBMIT ON FORM 12-B` /
  `DOCUMENT MISSING SIGNATURE`

---

## 6. Audio — 10 minutes, mostly free

The audio module is built and documented in [CLAUDE.md](CLAUDE.md). Resolution runs from most
specific to least: **object's clip override → object's `SfxId` in the room bank → same id in the
default bank → generic press**. A new room is *audible with zero authoring* — you only declare what
should differ.

1. On the room root, add **`RoomAudioZone`** with a **trigger** collider covering the interior.
   ⚠️ The volume must be on the audio-zone layer (**layer 6**) — it must stay out of
   `PlayerInteraction.interactableMask`, or the room-sized collider swallows the interaction
   raycast and **nothing in the room will be usable.**
2. Duplicate `Assets/Configs/Audio/RadarRoomSfxBank.asset` → `RadioRoomSfxBank` / `ArchiveRoomSfxBank`,
   assign to the zone's `Room Bank`. Declare only the ids that should sound different — likely
   `SwitchStep`, `LeverConfirm`, `PuzzleSolved`, `PuzzleFailed`.
3. Add **`PuzzleResolutionAudio`** to the room root, `Puzzle Object` = the room root. Set
   `Emit From` to the printer so the verdict comes from the right place.
4. Radio only: the per-channel loops go on `AudioSource`s referenced by `RadioChannelDisplay`, not
   through `SfxBank` — they are looping content, not one-shots. Use `RoomLoopSource` if you want
   them gated on room occupancy.

Available `SfxId` values are in [SfxId.cs](Assets/Scripts/Audio/SfxId.cs). **Never renumber an
existing member** — bank assets serialize the value. Adding new ids at the end is fine.

---

## 7. Level placement and what NOT to touch

Rooms 4 and 5 already exist as empty shells with open doorways, from the 3 → 5 room level rebuild:

| Room | Bounds | Side | Doorway gap |
| --- | --- | --- | --- |
| 4 | X `[2.0, 12.2]`, Z `[36.9, 47.2]` | right | east wall Z `[41.1, 42.9]`, centre z = 42 |
| 5 | X `[-14.2, -2.0]`, Z `[47.9, 58.2]` | left | west wall Z `[52.1, 53.9]`, centre z = 53 |

Verify these in the open scene before you build against them — they are recorded from the last
rebuild, not re-measured today.

### Hard rules

- **Do not add doors.** Door locks and door opening are owned by another developer. Both rooms are
  deliberately doorless right now. Pull her latest work before assuming a room has no door, and
  coordinate before wiring any exit behaviour.
- **`HOTWAR_ROOMS` is disposable output.** [HotWarLevelKit](Assets/assets/HotWarLevelKit.cs)
  destroys and regenerates that whole root on any rebuild. Put your room prefab instance under
  **`HOTWAR_PROPS`** or its own root — never inside `HOTWAR_ROOMS`, or a rebuild deletes your work.
- **Keep `RoomSpec.doorSlab = false`.** A rebuild with it on re-seals every doorway with a collider
  slab and the level becomes unplayable.
- **One scene per person.** Four people work in parallel specifically to avoid `.unity` merge
  conflicts — Unity scene YAML merges badly and a conflicted scene usually means lost work. Build
  the room as a **prefab**; the scene change is then a single instance line.
- **No `.asmdef` files.** Everything compiles into `Assembly-CSharp` on purpose.

---

## 8. Integration once both rooms are in

Three small changes that are easy to forget and each breaks the endgame quietly:

1. **[PuzzleTracker.cs:10](Assets/Scripts/Puzzles/PuzzleTracker.cs#L10)** — `TOTAL_ROOM_PUZZLES` is
   still `4`. With five rooms in the build, the all-solved gate fires **one room early**. Change it
   to `5` when both rooms land.
2. **`GameManager.requiredStages`** on [GameSystems.prefab](Assets/prefabs/GameSystems.prefab) —
   the code default already lists the five essential stages, but check the **serialized** value on
   the prefab includes `Authenticate` and `Authorize`. This is what `IsVerificationComplete` gates
   on, and a serialized array beats the code default.
3. **`RoomProgressionController.stages`** — still 3 entries. Extending it is door work; hand it to
   the door owner rather than editing it yourself.

---

## 9. Gotchas that have already cost this project time

- **Never use `?.`, `??`, or `is null` / `is not null` on `UnityEngine.Object` types.** Pattern
  matching bypasses Unity's overridden `==` and fails to detect destroyed objects. Use
  `if (obj == null)` / `if (obj)`. Plain C# types get the modern operators. An interface reference
  to a MonoBehaviour has the same problem — resolve once and cache a `bool hasPuzzle`, the way
  `ConfirmLever` does.
- **World-space TMP renders mirrored** unless rotated 180° on Y. See §4.
- **`SelectorSwitch.OnSelectionChanged` fires only on player input** — never on init or reset. That
  is deliberate (it is how a room tells an untouched panel from a chosen answer), and it is why
  `RadioChannelDisplay` pushes its opening state from `Start`.
- **`Criteria Switches` and `Correct Option Indices` are read in lockstep.** A length mismatch
  silently checks the answer against the wrong switch and is invisible until someone fails a
  correct panel. `OnValidate` warns; read the console.
- **The wrong-answer card character must differ from the correct one**, or a player who fails every
  room still assembles the correct final code.
- **`Stage` orders the final code; `Stage Label` is only the printed noun.** A config authored with
  the default `Detect` puts its card in the radar room's slot and silently corrupts the finale.
- **Subscribe in `OnEnable`, unsubscribe in `OnDisable`.** No `GetComponent`, allocation, LINQ or
  string concatenation in `Update`.
- **New Input System only** — never `Input.GetKeyDown`. Extend the existing
  `InputSystem_Actions.inputactions`; do not add a second asset. Guard callbacks with
  `if (Time.timeScale == 0) return;`.
- **UniTask over coroutines**, always with `this.GetCancellationTokenOnDestroy()`. **DOTween over
  manual lerps**, `.SetUpdate(true)` for anything that must play while paused.
- `Packages/manifest.json` and `ProjectSettings/` are shared across all four branches and are the
  usual conflict point. Validate the manifest still parses after any merge — it has already taken
  one bad resolution that duplicated a key.

---

## 10. Definition of done

Test each room twice, in this order:

**Standalone (no GameManager in the scene — this is a supported setup):**
- [ ] Every switch steps and wraps on `E`, and its readout updates.
- [ ] Confirm lever is **refused** before any switch is touched, with a readable reason in the prompt.
- [ ] Reset lever returns every switch to its start position and clears the verdict.
- [ ] Correct combination → confirm → verdict shows `Correct Verdict`, printout shows the card line
      plus its evidence, console logs `[Radio] Confirm filed … correct=True`.
- [ ] Wrong combination → confirm → a *different* verdict, a card with the wrong character, and
      `correct=False`.
- [ ] After confirm: switches are locked, the reset lever says `Panel sealed`, the confirm lever
      says `Result already filed`.

**In the real scene (with [GameSystems.prefab](Assets/prefabs/GameSystems.prefab)):**
- [ ] The card appears in the Evidence HUD (**Tab**) under the right stage label.
- [ ] Nothing in the room is un-interactable — if the crosshair goes dead, the `RoomAudioZone`
      collider is on the wrong layer (§6).
- [ ] `read_console` via MCP is clean: no `[PuzzleConfig]` or `[SwitchCombo]` warnings.

---

## 11. Prompt to hand an AI assistant

> I'm implementing the Radio Authentication room (room 4) in this Unity project. Read
> `remaining-rooms-handoff.md`, `CLAUDE.md` and `SKILL.md` first, then read
> `Assets/Scripts/Puzzles/SwitchComboPuzzle.cs`, `SelectorSwitch.cs`, `BasePuzzle.cs` and
> `PuzzleConfig.cs`. Follow §3 and §5 of the handoff. Use MCP for Unity against the live editor for
> all prefab and asset wiring — never hand-edit prefab or scene YAML. Do not add or modify any door
> behaviour. When you're done, run the §10 checklist and show me the console output.

Swap §3 for §4 and "Radio Authentication room (room 4)" for "Records Archive (room 5)" for the
other one.
