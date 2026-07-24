# The Hot War — Level Kit v3 (declarative)

You decide **what** each room is; the system decides **where and how**. Per room you set just: name, size, shape, feature, and (optionally) side. Placement along the corridor, spacing, doors, shared-wall handling, notch orientation, stair/hole/platform/basement geometry — all computed. And with **Auto Rebuild** on (default), the level regenerates by itself shortly after you stop editing — a quiet-period debounce (`rebuildDelay`, default 0.35s) so typing multi-digit numbers and dragging sliders stays smooth, and the rebuild never touches your selection or Inspector focus. No rebuild button, no coordinates.

## Setup

Replace the old `HotWarLevelKit.cs` with this file (same name). Empty GameObject → Add Component → The Hot War → Hot War Level Kit. Then either right-click the component → **Load Doc Layout**, or just start adding rooms to the list with the + button. Everything appears/updates as you type.

## Per-room fields (the whole interface)

- **name** — used for the label, the plate, and the door object's name.
- **size** — *width* = across the doorway wall, *depth* = how far the room extends away from the door. Orientation-independent: you never think about world axes.
- **shape** — Rectangle or LShape. The notch is placed automatically at the far corner, on your left as you walk in; `notchScale` sizes it.
- **feature** — None / **Platform with stairs** (platform hugs the far wall, stairs sized and placed to reach it exactly) / **Basement** (stairwell lane along the right wall, floor hole above it, and a full basement room generated underneath). Features auto-shrink to fit small rooms, and log a warning if a room is genuinely too small.
- **side** — Auto alternates left/right up the corridor; force Left/Right if you care.
- **extraGapBefore** — optional extra corridor distance before this room.
- **plateText / doorSlab / accent** — cosmetics; plate defaults to the room name.

The **last room in the list caps the far end of the corridor** (toggle `lastRoomCapsCorridor` off to make it a side room instead). Reordering the list reorders the level.

## What the system guarantees

Rooms never overlap: they're staggered along the corridor with `roomSpacing` between them, and the corridor auto-lengthens to fit however many rooms you add. Each side room's corridor-facing wall is omitted and the corridor's wall carries the opening, slab, lamps, and plate (facing the corridor, where the player reads it). Stairs always land exactly on their platform because platform height snaps to whole steps. Basements are exactly as deep as their stairwell descends.

## Global knobs (mostly leave alone)

`corridorWidth`, `startGap`, `roomSpacing`, `endGap`, `wallHeight`, `platformHeight` (default 1.75 = 7 steps), `basementDepth` (default 3.25 = 13 steps), `buildCeilings` (off so you can see everything from above), `autoRebuild`, `rebuildDelay`.

## Menu reference

**Rebuild Level** (manual trigger; also runs automatically) · **Load Doc Layout** — the design-doc level as 4 list entries in the real unlock order (Radar → Computer Hall → Communications → Satellite Telemetry capping the end), plates carrying the doc's lying times · **Append Example Rooms** — an L-room-with-mezzanine and a basement room that slot themselves into the corridor; delete the entries to remove them · **Spawn Doc Control-Room Props** (desk/phone/cards/log/override/window/spawn marker → `HOTWAR_PROPS`, which rebuilds never touch) · **Apply Mood Lighting** · **Clear Rooms / Clear Props**.

## The one rule to remember

The rooms **list** is the level; geometry under `HOTWAR_ROOMS` is disposable output that regenerates constantly. Never hand-edit there — anything you place by hand goes in `HOTWAR_PROPS` or its own hierarchy. Undo works through the data: Ctrl+Z reverts your field change and the level follows.
