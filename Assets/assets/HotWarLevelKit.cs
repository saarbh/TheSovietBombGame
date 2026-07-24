// ============================================================================
//  THE HOT WAR — LEVEL KIT v3  (declarative: you decide WHAT, it decides WHERE)
// ----------------------------------------------------------------------------
//  PER ROOM you choose only:
//      name | size (width x depth) | shape (Rectangle / LShape)
//      feature (None / Platform with stairs / Basement) | optional side/gap
//
//  THE SYSTEM decides everything else, live:
//      - where each room sits along the corridor (staggered left/right,
//        auto-spaced, corridor auto-lengthens to fit)
//      - door placement, which walls are shared/omitted, slabs + plates
//      - where the L-notch goes (far corner, left of the entrance)
//      - platform size + position (far wall) and the stairs to reach it
//      - basement stairwell (right-hand lane), the floor hole above it,
//        and the basement room underneath — auto-shrunk to fit small rooms
//
//  AUTO-REBUILD: change any field in the Inspector and the level regenerates
//  by itself (toggle 'Auto Rebuild' off if you ever want manual control;
//  right-click -> Rebuild Level is always available).
//
//  LAYOUT CONVENTION (so results are predictable):
//      width  = across the doorway wall          depth = away from the door
//      "Left/Right" side = as seen walking up the corridor from the start room
//      the LAST room in the list caps the end of the corridor (toggleable)
//
//  Output is plain geometry (cubes, lights, TextMesh signs), no scripts on
//  any of it. Data lives on this component; geometry under HOTWAR_ROOMS is
//  disposable output. Hand-polish goes in HOTWAR_PROPS (never touched).
//
//  SETUP: replace the old HotWarLevelKit.cs with this file. Empty GameObject
//  -> Add Component -> The Hot War -> Hot War Level Kit. Then either press
//  'Load Doc Layout' in the context menu, or just add rooms to the list.
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheHotWar
{
    [AddComponentMenu("The Hot War/Hot War Level Kit")]
    public class HotWarLevelKit : MonoBehaviour
    {
        public enum RoomShape { Rectangle, LShape }
        public enum Feature { None, PlatformWithStairs, Basement }
        public enum Side { Auto, Left, Right }

        // ====================================================================
        //  WHAT YOU EDIT
        // ====================================================================
        [Serializable]
        public class RoomSpec
        {
            public string name = "ROOM";

            [Tooltip("width = across the doorway wall, depth = how far the room extends away from the door.")]
            public Vector2 size = new Vector2(8f, 8f);

            public RoomShape shape = RoomShape.Rectangle;

            [Tooltip("How big the cut-away corner is, as a fraction of the room (LShape only).")]
            [Range(0.2f, 0.55f)] public float notchScale = 0.4f;

            [Tooltip("None | stairs up to a platform on the far wall | stairwell down to an auto-generated basement.")]
            public Feature feature = Feature.None;

            [Tooltip("Which side of the corridor. Auto alternates left/right.")]
            public Side side = Side.Auto;

            [Tooltip("Extra corridor distance before this room (optional).")]
            public float extraGapBefore = 0f;

            [Tooltip("Painted plate text on the door. Empty = the room name.")]
            [TextArea] public string plateText = "";

            [Tooltip("Place a door slab + status lamps in the opening.")]
            public bool doorSlab = true;

            public Color accent = new Color(0.45f, 0.45f, 0.42f);
        }

        [Header("ROOMS — add/remove/reorder; everything places itself")]
        public List<RoomSpec> rooms = new List<RoomSpec>();

        [Tooltip("The last room in the list caps the far end of the corridor instead of sitting on a side.")]
        public bool lastRoomCapsCorridor = true;

        [Header("Start room (the control room)")]
        public string startRoomName = "CONTROL ROOM";
        public Vector2 startRoomSize = new Vector2(12f, 10f);
        [TextArea] public string startRoomLabel = "DUTY STATION 07\nDO NOT ABANDON POST";

        [Header("Layout tuning (sane defaults — mostly leave alone)")]
        public float corridorWidth = 4f;
        [Tooltip("Corridor distance before the first room.")]
        public float startGap = 4f;
        [Tooltip("Corridor distance between consecutive rooms.")]
        public float roomSpacing = 3f;
        [Tooltip("Corridor distance after the last side room.")]
        public float endGap = 3f;
        public float wallHeight = 3.2f;
        [Tooltip("Top of an auto platform above the floor. Snaps to whole steps.")]
        public float platformHeight = 1.75f;
        [Tooltip("How deep an auto basement goes. Snaps to whole steps; auto-shrinks in small rooms.")]
        public float basementDepth = 3.25f;
        public bool buildCeilings = false;

        [Header("Rebuild")]
        [Tooltip("Regenerate the level automatically whenever a field changes.")]
        public bool autoRebuild = true;

        [Tooltip("Quiet time (seconds) after your last edit before the level regenerates. Keeps typing and slider-dragging smooth.")]
        [Range(0.1f, 1.5f)] public float rebuildDelay = 0.35f;

        [Header("Doc timing (used by 'Load Doc Layout' plates)")]
        public float totalTime = 420f;
        public float[] realUnlockElapsed = { 60f, 360f, 300f, 180f };
        public float[] shownUnlockElapsed = { 60f, 180f, 300f, 360f };

        // ---- constants ------------------------------------------------------
        const float WallT = 0.3f;
        const float DoorW = 1.8f;
        const float DoorH = 2.4f;
        const float StepH = 0.25f;
        const float StepD = 0.30f;
        const string RoomsRootName = "HOTWAR_ROOMS";
        const string PropsRootName = "HOTWAR_PROPS";

        static readonly Color DoorCol = new Color(0.48f, 0.13f, 0.10f);
        static readonly Color LampOff = new Color(0.9f, 0.15f, 0.12f);
        static readonly Color CeilCol = new Color(0.15f, 0.15f, 0.16f);
        static readonly Color HallWall = new Color(0.34f, 0.36f, 0.34f);
        static readonly Color HallFloor = new Color(0.17f, 0.17f, 0.18f);

        readonly Dictionary<Color, Material> _mats = new Dictionary<Color, Material>();
#if UNITY_EDITOR
        double _lastEditTime;
        bool _updateHooked;
#endif

        // ====================================================================
        //  AUTO-REBUILD
        // ====================================================================
        void OnValidate()
        {
#if UNITY_EDITOR
            if (!autoRebuild) return;
            _lastEditTime = EditorApplication.timeSinceStartup;
            if (!_updateHooked)
            {
                _updateHooked = true;
                EditorApplication.update += EditorDebounce;
            }
#endif
        }

#if UNITY_EDITOR
        // Rebuild only after the edits go quiet, and NEVER touch selection/focus:
        // the Inspector you're typing into must stay exactly where it is.
        void EditorDebounce()
        {
            if (this == null) { EditorApplication.update -= EditorDebounce; return; }
            if (EditorApplication.timeSinceStartup - _lastEditTime < rebuildDelay) return;

            EditorApplication.update -= EditorDebounce;
            _updateHooked = false;

            if (!autoRebuild || Application.isPlaying) return;
            RebuildLevel();
        }
#endif

        // ====================================================================
        //  MENU
        // ====================================================================
        [ContextMenu("Rebuild Level")]
        public void RebuildLevel()
        {
            GameObject old = GameObject.Find(RoomsRootName);
            if (old != null) DestroyImmediate(old);
            _mats.Clear();

            Transform root = new GameObject(RoomsRootName).transform;

            // ---------------- LAYOUT PASS -----------------------------------
            // Corridor runs along +Z, centered on x = 0, starting at z = 0.
            float halfHall = corridorWidth * 0.5f;

            RoomSpec endSpec = (lastRoomCapsCorridor && rooms.Count > 0) ? rooms[rooms.Count - 1] : null;
            int sideCount = rooms.Count - (endSpec != null ? 1 : 0);

            List<InternalRoom> placed = new List<InternalRoom>();
            List<GapReq> westGaps = new List<GapReq>();
            List<GapReq> eastGaps = new List<GapReq>();

            float cursor = startGap;
            bool nextLeft = true;

            for (int i = 0; i < sideCount; i++)
            {
                RoomSpec s = rooms[i];
                if (s == null || s.size.x < 2f || s.size.y < 2f) continue;

                bool left = s.side == Side.Auto ? nextLeft : (s.side == Side.Left);
                if (s.side == Side.Auto) nextLeft = !nextLeft;

                float z0 = cursor + Mathf.Max(0f, s.extraGapBefore);
                float zMid = z0 + s.size.x * 0.5f;   // width runs along the corridor
                cursor = z0 + s.size.x + roomSpacing;

                InternalRoom r = MakeSideRoom(s, left, halfHall, z0);
                placed.Add(r);

                (left ? westGaps : eastGaps).Add(new GapReq
                {
                    z = zMid,
                    slab = s.doorSlab,
                    name = "Door_" + SafeName(s.name),
                    plate = string.IsNullOrEmpty(s.plateText) ? s.name : s.plateText
                });
            }

            float corridorLen = Mathf.Max(cursor - roomSpacing + endGap, 8f);

            // ---------------- BUILD PASS ------------------------------------
            BuildStartRoom(root, halfHall);
            BuildCorridor(root, halfHall, corridorLen, westGaps, eastGaps, endSpec != null);

            foreach (InternalRoom r in placed)
                BuildRoom(root, r);

            if (endSpec != null && endSpec.size.x >= 2f && endSpec.size.y >= 2f)
                BuildRoom(root, MakeEndRoom(endSpec, corridorLen));
        }

        [ContextMenu("Load Doc Layout (overwrites the rooms list)")]
        public void LoadDocLayout()
        {
            startRoomName = "CONTROL ROOM";
            startRoomSize = new Vector2(12f, 10f);
            startRoomLabel = "DUTY STATION 07\nDO NOT ABANDON POST";
            corridorWidth = 4f; startGap = 5f; roomSpacing = 3f; endGap = 4f;
            lastRoomCapsCorridor = true;

            rooms = new List<RoomSpec>
            {
                DocRoom("RADAR ARRAY", 0, Side.Left, new Color(0.20f, 0.50f, 0.30f)),
                DocRoom("COMPUTER HALL", 3, Side.Right, new Color(0.58f, 0.46f, 0.15f)),
                DocRoom("COMMUNICATIONS", 2, Side.Left, new Color(0.22f, 0.32f, 0.58f)),
                DocRoom("SATELLITE TELEMETRY", 1, Side.Auto, new Color(0.60f, 0.20f, 0.20f)) // caps the corridor
            };

            Debug.Log("[Hot War Kit] Doc layout loaded: 4 rooms in the real unlock order (1 -> 4 -> 3 -> 2 at the end). " +
                      "Plates carry the doc's painted (lying) times.");
            RebuildIfAuto();
        }

        RoomSpec DocRoom(string nm, int idx, Side side, Color accent)
        {
            float shown = (shownUnlockElapsed != null && idx < shownUnlockElapsed.Length) ? shownUnlockElapsed[idx] : 60f;
            return new RoomSpec
            {
                name = nm,
                size = new Vector2(8f, 8f),
                side = side,
                accent = accent,
                plateText = nm + "\nPLATE: OPENS AT T-" + FmtT(totalTime - shown)
            };
        }

        [ContextMenu("Append Example Rooms (L-room + platform, basement room)")]
        public void AppendExamples()
        {
            rooms.Add(new RoomSpec
            {
                name = "DEMO L-MEZZANINE",
                size = new Vector2(10f, 10f),
                shape = RoomShape.LShape,
                feature = Feature.PlatformWithStairs,
                accent = new Color(0.5f, 0.4f, 0.6f)
            });
            rooms.Add(new RoomSpec
            {
                name = "DEMO CELLAR",
                size = new Vector2(7f, 8f),
                feature = Feature.Basement,
                accent = new Color(0.35f, 0.5f, 0.55f)
            });
            Debug.Log("[Hot War Kit] Appended 2 demo rooms — they slot themselves into the corridor. Delete the list entries to remove them.");
            RebuildIfAuto();
        }

        [ContextMenu("Spawn Doc Control-Room Props")]
        public void SpawnDocProps()
        {
            Color[] accent =
            {
                new Color(0.20f, 0.50f, 0.30f), new Color(0.60f, 0.20f, 0.20f),
                new Color(0.22f, 0.32f, 0.58f), new Color(0.58f, 0.46f, 0.15f)
            };
            string[] names = { "RADAR ARRAY", "SATELLITE TELEMETRY", "COMMUNICATIONS", "COMPUTER HALL" };

            GameObject rootGo = GameObject.Find(PropsRootName);
            if (rootGo == null) rootGo = new GameObject(PropsRootName);
            Transform root = rootGo.transform;
            RegisterUndo(rootGo, "Spawn Hot War Props");

            Box(root, "Desk", new Vector3(0f, 0.45f, -9.1f), new Vector3(7.5f, 0.9f, 1.1f), new Color(0.36f, 0.26f, 0.18f), false);
            Box(root, "Phone_DutyLine", new Vector3(-3.0f, 1.02f, -9.1f), new Vector3(0.5f, 0.25f, 0.35f), new Color(0.85f, 0.1f, 0.08f), false);
            Sign(root, "DUTY LINE", new Vector3(-3.0f, 1.9f, -9.82f), 180f, 0.8f, null);

            Sign(root, "CODE CARDS", new Vector3(0.35f, 1.9f, -9.82f), 180f, 0.8f, null);
            for (int i = 0; i < 4; i++)
                Box(root, "CodeCard_" + (i + 1) + "_" + names[i],
                    new Vector3(-1.05f + i * 0.7f, 1.12f, -9.1f), new Vector3(0.34f, 0.42f, 0.08f), accent[i], false);

            Box(root, "MaintenanceLog_(hints plates 2 & 4 swapped)",
                new Vector3(5.8f, 1.5f, -4.5f), new Vector3(0.12f, 0.9f, 0.7f), new Color(0.7f, 0.62f, 0.45f), false);
            Sign(root, "MAINTENANCE LOG", new Vector3(5.82f, 2.25f, -4.5f), 90f, 0.75f, null);

            Box(root, "OverrideTerminal", new Vector3(-5.55f, 0.65f, -4.5f), new Vector3(0.8f, 1.3f, 0.8f), new Color(0.20f, 0.23f, 0.28f), false);
            Box(root, "OverrideLamp", new Vector3(-5.55f, 1.46f, -4.5f), new Vector3(0.16f, 0.16f, 0.16f), Color.yellow, false);
            Sign(root, "MECHANICAL OVERRIDE", new Vector3(-5.82f, 2.25f, -4.5f), -90f, 0.75f, null);

            Box(root, "Window", new Vector3(3.3f, 2.05f, -9.87f), new Vector3(2.4f, 1.3f, 0.08f), new Color(0.62f, 0.72f, 0.85f), false);
            Sign(root, "WINDOW", new Vector3(3.3f, 2.85f, -9.8f), 180f, 0.7f, null);

            GameObject spawn = new GameObject("PlayerSpawn (face +Z)");
            spawn.transform.SetParent(root, false);
            spawn.transform.localPosition = new Vector3(0f, 0.05f, -5f);
        }

        [ContextMenu("Apply Mood Lighting (fog + dim ambient)")]
        public void ApplyMoodLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.28f, 0.30f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.028f;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.06f);
        }

        [ContextMenu("Clear Rooms")]
        public void ClearRooms() { GameObject g = GameObject.Find(RoomsRootName); if (g != null) DestroyImmediate(g); }

        [ContextMenu("Clear Props")]
        public void ClearProps() { GameObject g = GameObject.Find(PropsRootName); if (g != null) DestroyImmediate(g); }

        void RebuildIfAuto() { if (autoRebuild && !Application.isPlaying) RebuildLevel(); }

        // ====================================================================
        //  LAYOUT COMPILER — turns specs into fully-resolved internal rooms
        // ====================================================================
        enum WallSide { South, North, West, East }

        class Doorway
        {
            public WallSide side;
            public float offset;
            public bool slab;
            public string doorName = "";
            public string plate = "";
            public bool plateOutward = true;
        }

        class Rect2 { public float x, z, w, d; public Rect2(float x, float z, float w, float d) { this.x = x; this.z = z; this.w = w; this.d = d; } }

        class Stair2 { public float x, z; public WallSide dir; public float width, rise; }

        class InternalRoom
        {
            public string name;
            public Vector3 origin;          // world SW corner; y = floor elevation
            public float xExt, zExt, height;
            public RoomShape shape;
            public Rect2 notch;             // local; null if rectangle
            public bool omitS, omitN, omitW, omitE;
            public List<Doorway> doors = new List<Doorway>();
            public List<Rect2> holes = new List<Rect2>();
            public List<Rect2> platforms = new List<Rect2>();
            public float platformElev;
            public List<Stair2> stairs = new List<Stair2>();
            public Color wallColor = new Color(0.38f, 0.37f, 0.35f);
            public Color floorColor = new Color(0.17f, 0.17f, 0.18f);
            public Color accent = Color.white;
            public string label = "";
            public WallSide labelSide = WallSide.North;
            public InternalRoom basement;   // auto-generated, built after the parent
        }

        class GapReq { public float z; public bool slab; public string name; public string plate; }

        // door-relative frame: u = left->right across the door wall, v = door wall -> far wall
        struct Frame
        {
            public float xExt, zExt;
            public WallSide doorSide;

            public Frame(WallSide door, float width, float depth)
            {
                doorSide = door;
                bool ns = door == WallSide.South || door == WallSide.North;
                xExt = ns ? width : depth;
                zExt = ns ? depth : width;
            }

            public void Map(float u, float v, out float x, out float z)
            {
                switch (doorSide)
                {
                    case WallSide.South: x = u; z = v; break;
                    case WallSide.North: x = xExt - u; z = zExt - v; break;
                    case WallSide.East: x = xExt - v; z = u; break;
                    default: x = v; z = zExt - u; break; // West
                }
            }

            public Rect2 MapRect(float u0, float v0, float u1, float v1)
            {
                float xa, za, xb, zb;
                Map(u0, v0, out xa, out za);
                Map(u1, v1, out xb, out zb);
                return new Rect2(Mathf.Min(xa, xb), Mathf.Min(za, zb), Mathf.Abs(xb - xa), Mathf.Abs(zb - za));
            }

            // world direction of "forward" (away from the door)
            public WallSide Fwd()
            {
                switch (doorSide)
                {
                    case WallSide.South: return WallSide.North;
                    case WallSide.North: return WallSide.South;
                    case WallSide.East: return WallSide.West;
                    default: return WallSide.East;
                }
            }
        }

        InternalRoom MakeSideRoom(RoomSpec s, bool left, float halfHall, float z0)
        {
            WallSide doorSide = left ? WallSide.East : WallSide.West;
            Frame f = new Frame(doorSide, s.size.x, s.size.y);

            InternalRoom r = NewRoom(s, f);
            r.origin = new Vector3(left ? -halfHall - s.size.y : halfHall, 0f, z0);
            if (left) r.omitE = true; else r.omitW = true;   // corridor provides that wall
            r.labelSide = f.Fwd();                            // label on the far wall

            CompileFeatures(s, f, r);
            return r;
        }

        InternalRoom MakeEndRoom(RoomSpec s, float corridorLen)
        {
            Frame f = new Frame(WallSide.South, s.size.x, s.size.y);
            InternalRoom r = NewRoom(s, f);
            r.origin = new Vector3(-s.size.x * 0.5f, 0f, corridorLen);
            r.labelSide = WallSide.North;

            r.doors.Add(new Doorway
            {
                side = WallSide.South,
                offset = s.size.x * 0.5f,
                slab = s.doorSlab,
                doorName = "Door_" + SafeName(s.name),
                plate = string.IsNullOrEmpty(s.plateText) ? s.name : s.plateText,
                plateOutward = true
            });

            CompileFeatures(s, f, r);
            return r;
        }

        InternalRoom NewRoom(RoomSpec s, Frame f)
        {
            InternalRoom r = new InternalRoom
            {
                name = SafeName(s.name),
                xExt = f.xExt,
                zExt = f.zExt,
                height = wallHeight,
                shape = s.shape,
                accent = s.accent,
                floorColor = Color.Lerp(HallFloor, s.accent, 0.25f),
                label = s.name
            };

            if (s.shape == RoomShape.LShape)
            {
                // notch at the FAR-LEFT corner (deterministic; keeps the right-hand
                // lane free for basement stairs and the far-right for platforms)
                float width = f.doorSide == WallSide.South || f.doorSide == WallSide.North ? f.xExt : f.zExt;
                float depth = f.doorSide == WallSide.South || f.doorSide == WallSide.North ? f.zExt : f.xExt;
                float nw = Mathf.Clamp(s.notchScale * width, 1f, width - 1.6f);
                float nd = Mathf.Clamp(s.notchScale * depth, 1f, depth - 1.6f);
                r.notch = f.MapRect(0f, depth - nd, nw, depth);
            }
            return r;
        }

        void CompileFeatures(RoomSpec s, Frame f, InternalRoom r)
        {
            bool ns = f.doorSide == WallSide.South || f.doorSide == WallSide.North;
            float width = ns ? f.xExt : f.zExt;
            float depth = ns ? f.zExt : f.xExt;

            float nw = 0f;
            if (s.shape == RoomShape.LShape)
                nw = Mathf.Clamp(s.notchScale * width, 1f, width - 1.6f);

            if (s.feature == Feature.PlatformWithStairs)
            {
                // platform hugs the far wall, right of the notch
                float u0 = (s.shape == RoomShape.LShape ? nw + 0.15f : 0f);
                float u1 = width;
                if (u1 - u0 < 2.2f)
                {
                    Debug.LogWarning("[Hot War Kit] '" + s.name + "': too narrow beside the notch for a platform. Feature skipped.");
                    return;
                }

                int steps = Mathf.Max(4, Mathf.RoundToInt(platformHeight / StepH));
                float run = steps * StepD;
                float pd = Mathf.Clamp(depth * 0.35f, 1.6f, 3.0f);

                while (steps > 4 && depth - pd - steps * StepD < 1.2f) steps--;
                run = steps * StepD;
                float elev = steps * StepH;

                if (depth - pd - run < 1.0f)
                {
                    Debug.LogWarning("[Hot War Kit] '" + s.name + "': too shallow for platform + stairs. Feature skipped.");
                    return;
                }

                r.platforms.Add(f.MapRect(u0, depth - pd, u1, depth));
                r.platformElev = elev;

                float su = (u0 + u1) * 0.5f;
                float sx, sz;
                f.Map(su, depth - pd - run, out sx, out sz);
                r.stairs.Add(new Stair2 { x = sx, z = sz, dir = f.Fwd(), width = 1.6f, rise = elev });
            }
            else if (s.feature == Feature.Basement)
            {
                // stairwell lane along the RIGHT wall, descending away from the door
                int steps = Mathf.Min(Mathf.Max(4, Mathf.RoundToInt(basementDepth / StepH)),
                                      Mathf.FloorToInt((depth - 2.0f) / StepD));
                if (steps < 6)
                {
                    Debug.LogWarning("[Hot War Kit] '" + s.name + "': too small for a basement stairwell. Feature skipped.");
                    return;
                }
                float run = steps * StepD;
                float bDepth = steps * StepH;

                r.holes.Add(f.MapRect(width - 2.0f, 0.9f, width - 0.2f, 0.9f + run + 0.2f));

                float sx, sz;
                f.Map(width - 1.1f, 1.0f, out sx, out sz);
                r.stairs.Add(new Stair2 { x = sx, z = sz, dir = f.Fwd(), width = 1.6f, rise = -bDepth });

                InternalRoom b = new InternalRoom
                {
                    name = r.name + "_BASEMENT",
                    origin = new Vector3(r.origin.x, -bDepth, r.origin.z),
                    xExt = r.xExt,
                    zExt = r.zExt,
                    height = bDepth,
                    shape = RoomShape.Rectangle,
                    wallColor = new Color(0.30f, 0.30f, 0.32f),
                    floorColor = new Color(0.12f, 0.12f, 0.13f),
                    accent = r.accent,
                    label = s.name + "\nBASEMENT",
                    labelSide = f.doorSide
                };
                r.basement = b;
            }
        }

        // ====================================================================
        //  GEOMETRY BACKEND
        // ====================================================================
        void BuildStartRoom(Transform root, float halfHall)
        {
            float w = Mathf.Max(6f, startRoomSize.x), d = Mathf.Max(6f, startRoomSize.y);
            InternalRoom r = new InternalRoom
            {
                name = SafeName(startRoomName),
                origin = new Vector3(-w * 0.5f, 0f, -d),
                xExt = w, zExt = d, height = wallHeight,
                wallColor = new Color(0.45f, 0.42f, 0.33f),
                floorColor = new Color(0.25f, 0.24f, 0.19f),
                accent = new Color(0.9f, 0.55f, 0.4f),
                label = startRoomLabel,
                labelSide = WallSide.North
            };
            r.doors.Add(new Doorway { side = WallSide.North, offset = w * 0.5f, slab = false });
            BuildRoom(root, r);
        }

        void BuildCorridor(Transform root, float halfHall, float len,
                           List<GapReq> west, List<GapReq> east, bool openEnd)
        {
            InternalRoom r = new InternalRoom
            {
                name = "Corridor",
                origin = new Vector3(-halfHall, 0f, 0f),
                xExt = corridorWidth, zExt = len, height = wallHeight,
                wallColor = HallWall,
                floorColor = HallFloor,
                omitS = true,                    // start room's north wall serves
                omitN = openEnd                  // end room's south wall serves (if any)
            };

            foreach (GapReq g in west)
                r.doors.Add(new Doorway { side = WallSide.West, offset = g.z, slab = g.slab, doorName = g.name, plate = g.plate, plateOutward = false });
            foreach (GapReq g in east)
                r.doors.Add(new Doorway { side = WallSide.East, offset = g.z, slab = g.slab, doorName = g.name, plate = g.plate, plateOutward = false });

            BuildRoom(root, r);

            // corridor lights, evenly spaced
            Transform g2 = GameObject.Find(RoomsRootName).transform.Find(r.name);
            int n = Mathf.Max(2, Mathf.CeilToInt(len / 9f));
            for (int i = 0; i < n; i++)
                PointLight(g2, new Vector3(halfHall, Mathf.Min(wallHeight - 0.4f, 2.8f), len * (i + 0.5f) / n),
                           new Color(0.85f, 1f, 0.85f), 1.0f, 9f);
        }

        struct Edge
        {
            public bool alongX;
            public float fixedC, min, max;
            public WallSide facing;
            public List<Doorway> gaps;
        }

        void BuildRoom(Transform parent, InternalRoom r)
        {
            Transform g = Group(r.name, parent);
            g.position = r.origin;

            float w = r.xExt, d = r.zExt, h = r.height;

            // ---- edges ------------------------------------------------------
            List<Edge> edges = ComputeEdges(r, w, d);

            foreach (Doorway dw in r.doors)
            {
                int bi = -1;
                float bestLen = -1f;
                for (int i = 0; i < edges.Count; i++)
                {
                    if (edges[i].facing != dw.side) continue;
                    if (dw.offset - DoorW * 0.5f >= edges[i].min + 0.05f &&
                        dw.offset + DoorW * 0.5f <= edges[i].max - 0.05f)
                    {
                        float len = edges[i].max - edges[i].min;
                        if (len > bestLen) { bestLen = len; bi = i; }
                    }
                }
                if (bi < 0)
                {
                    Debug.LogWarning("[Hot War Kit] '" + r.name + "': computed doorway on " + dw.side + " @ " + dw.offset + " found no wall segment. Skipped.");
                    continue;
                }
                edges[bi].gaps.Add(dw);
            }

            foreach (Edge e in edges) BuildEdge(g, e, r, h);

            // ---- floor / ceiling ---------------------------------------------
            List<Rect2> holes = new List<Rect2>(r.holes);
            if (r.notch != null) holes.Add(r.notch);
            BuildSlabWithHoles(g, "Floor", w, d, -0.1f, r.floorColor, holes, true);
            if (buildCeilings && r.origin.y >= -0.01f)
                BuildSlabWithHoles(g, "Ceiling", w, d, h + 0.1f, CeilCol, holes, true);

            // ---- platforms / stairs ------------------------------------------
            foreach (Rect2 p in r.platforms)
                Box(g, "Platform_h" + r.platformElev,
                    new Vector3(p.x + p.w * 0.5f, r.platformElev - 0.1f, p.z + p.d * 0.5f),
                    new Vector3(p.w, 0.2f, p.d), Color.Lerp(r.floorColor, Color.white, 0.18f), true);

            foreach (Stair2 s in r.stairs) BuildStairs(g, s);

            // ---- label / light -----------------------------------------------
            if (!string.IsNullOrEmpty(r.label)) PlaceLabel(g, r, w, d, h);

            Vector3 c = Centroid(r, w, d);
            PointLight(g, new Vector3(c.x, Mathf.Min(h - 0.4f, 2.6f), c.z),
                       Color.Lerp(Color.white, r.accent, 0.4f), 1.15f, Mathf.Max(w, d) * 1.2f);

            // ---- auto basement ------------------------------------------------
            if (r.basement != null) BuildRoom(parent, r.basement);
        }

        List<Edge> ComputeEdges(InternalRoom r, float w, float d)
        {
            List<Edge> e = new List<Edge>();

            if (r.notch == null)
            {
                AddEdge(e, true, 0f, 0f, w, WallSide.South);
                AddEdge(e, true, d, 0f, w, WallSide.North);
                AddEdge(e, false, 0f, 0f, d, WallSide.West);
                AddEdge(e, false, w, 0f, d, WallSide.East);
            }
            else
            {
                Rect2 n = r.notch;
                bool west = n.x < 0.01f;
                bool south = n.z < 0.01f;
                float nx1 = n.x + n.w, nz1 = n.z + n.d;

                if (south)
                {
                    AddEdge(e, true, d, 0f, w, WallSide.North);
                    AddEdge(e, true, nz1, west ? 0f : n.x, west ? nx1 : w, WallSide.South);      // shoulder
                    AddEdge(e, true, 0f, west ? nx1 : 0f, west ? w : n.x, WallSide.South);
                    AddEdge(e, false, west ? nx1 : n.x, 0f, nz1, west ? WallSide.West : WallSide.East); // flank
                    AddEdge(e, false, 0f, west ? nz1 : 0f, d, WallSide.West);
                    AddEdge(e, false, w, west ? 0f : nz1, d, WallSide.East);
                }
                else
                {
                    AddEdge(e, true, 0f, 0f, w, WallSide.South);
                    AddEdge(e, true, n.z, west ? 0f : n.x, west ? nx1 : w, WallSide.North);      // shoulder
                    AddEdge(e, true, d, west ? nx1 : 0f, west ? w : n.x, WallSide.North);
                    AddEdge(e, false, west ? nx1 : n.x, n.z, d, west ? WallSide.West : WallSide.East); // flank
                    AddEdge(e, false, 0f, 0f, west ? n.z : d, WallSide.West);
                    AddEdge(e, false, w, 0f, west ? d : n.z, WallSide.East);
                }
            }

            List<Edge> kept = new List<Edge>();
            foreach (Edge ed in e)
            {
                if (ed.facing == WallSide.South && r.omitS) continue;
                if (ed.facing == WallSide.North && r.omitN) continue;
                if (ed.facing == WallSide.West && r.omitW) continue;
                if (ed.facing == WallSide.East && r.omitE) continue;
                kept.Add(ed);
            }
            return kept;
        }

        static void AddEdge(List<Edge> list, bool alongX, float fixedC, float min, float max, WallSide facing)
        {
            if (max - min < 0.05f) return;
            list.Add(new Edge { alongX = alongX, fixedC = fixedC, min = min, max = max, facing = facing, gaps = new List<Doorway>() });
        }

        void BuildEdge(Transform parent, Edge e, InternalRoom r, float h)
        {
            e.gaps.Sort((a, b) => a.offset.CompareTo(b.offset));
            float cur = e.min;

            foreach (Doorway gp in e.gaps)
            {
                float g0 = gp.offset - DoorW * 0.5f;
                float g1 = gp.offset + DoorW * 0.5f;

                if (g0 > cur + 0.02f) WallSeg(parent, e, cur, g0, r.wallColor, h);

                if (h > DoorH + 0.02f)
                {
                    Vector3 c = e.alongX ? new Vector3(gp.offset, (DoorH + h) * 0.5f, e.fixedC)
                                         : new Vector3(e.fixedC, (DoorH + h) * 0.5f, gp.offset);
                    Vector3 s = e.alongX ? new Vector3(DoorW, h - DoorH, WallT)
                                         : new Vector3(WallT, h - DoorH, DoorW);
                    Box(parent, "Lintel", c, s, r.wallColor, true);
                }

                if (gp.slab) BuildDoorSlab(parent, e, gp);
                if (!string.IsNullOrEmpty(gp.plate)) BuildPlate(parent, e, gp);

                cur = g1;
            }

            if (e.max > cur + 0.02f) WallSeg(parent, e, cur, e.max, r.wallColor, h);
        }

        void WallSeg(Transform parent, Edge e, float a, float b, Color c, float h)
        {
            Vector3 center = e.alongX ? new Vector3((a + b) * 0.5f, h * 0.5f, e.fixedC)
                                      : new Vector3(e.fixedC, h * 0.5f, (a + b) * 0.5f);
            Vector3 size = e.alongX ? new Vector3(b - a, h, WallT)
                                    : new Vector3(WallT, h, b - a);
            Box(parent, "Wall_" + e.facing, center, size, c, true);
        }

        void BuildDoorSlab(Transform parent, Edge e, Doorway gp)
        {
            string n = string.IsNullOrEmpty(gp.doorName) ? "Door" : gp.doorName;
            Vector3 c = e.alongX ? new Vector3(gp.offset, DoorH * 0.5f, e.fixedC)
                                 : new Vector3(e.fixedC, DoorH * 0.5f, gp.offset);
            Vector3 s = e.alongX ? new Vector3(DoorW - 0.06f, DoorH, WallT - 0.04f)
                                 : new Vector3(WallT - 0.04f, DoorH, DoorW - 0.06f);
            Box(parent, n, c, s, DoorCol, false);

            Vector3 nrm = Normal(e.facing);
            Vector3 lampBase = e.alongX ? new Vector3(gp.offset, 2.62f, e.fixedC)
                                        : new Vector3(e.fixedC, 2.62f, gp.offset);
            Box(parent, "DoorLamp_A", lampBase + nrm * (WallT * 0.5f + 0.09f), new Vector3(0.18f, 0.18f, 0.18f), LampOff, false);
            Box(parent, "DoorLamp_B", lampBase - nrm * (WallT * 0.5f + 0.09f), new Vector3(0.18f, 0.18f, 0.18f), LampOff, false);
        }

        void BuildPlate(Transform parent, Edge e, Doorway gp)
        {
            Vector3 dir = Normal(e.facing) * (gp.plateOutward ? 1f : -1f);
            Vector3 basePos = e.alongX ? new Vector3(gp.offset, 2.95f, e.fixedC)
                                       : new Vector3(e.fixedC, 2.95f, gp.offset);
            Sign(parent, gp.plate, basePos + dir * (WallT * 0.5f + 0.03f), ReadableYRot(dir), 0.85f, null);
        }

        void PlaceLabel(Transform g, InternalRoom r, float w, float d, float h)
        {
            float y = Mathf.Min(h - 0.5f, 2.35f);
            Vector3 pos; float yRot;
            switch (r.labelSide)
            {
                case WallSide.South: pos = new Vector3(w * 0.5f, y, WallT * 0.5f + 0.03f); yRot = 180f; break;
                case WallSide.West: pos = new Vector3(WallT * 0.5f + 0.03f, y, d * 0.5f); yRot = -90f; break;
                case WallSide.East: pos = new Vector3(w - WallT * 0.5f - 0.03f, y, d * 0.5f); yRot = 90f; break;
                default: pos = new Vector3(w * 0.5f, y, d - WallT * 0.5f - 0.03f); yRot = 0f; break;
            }
            Sign(g, r.label, pos, yRot, 1.3f, r.accent);
        }

        static Vector3 Normal(WallSide s)
        {
            switch (s)
            {
                case WallSide.South: return new Vector3(0f, 0f, -1f);
                case WallSide.North: return new Vector3(0f, 0f, 1f);
                case WallSide.West: return new Vector3(-1f, 0f, 0f);
                default: return new Vector3(1f, 0f, 0f);
            }
        }

        static float ReadableYRot(Vector3 from)
        {
            if (from.z < -0.5f) return 0f;
            if (from.z > 0.5f) return 180f;
            if (from.x > 0.5f) return -90f;
            return 90f;
        }

        void BuildSlabWithHoles(Transform parent, string label, float w, float d, float yCenter,
                                Color c, List<Rect2> holes, bool markStatic)
        {
            List<float> xs = new List<float> { 0f, w };
            List<Rect2> hs = new List<Rect2>();

            if (holes != null)
                foreach (Rect2 a in holes)
                {
                    if (a == null) continue;
                    float x0 = Mathf.Max(0f, a.x), x1 = Mathf.Min(w, a.x + a.w);
                    float z0 = Mathf.Max(0f, a.z), z1 = Mathf.Min(d, a.z + a.d);
                    if (x1 - x0 < 0.02f || z1 - z0 < 0.02f) continue;
                    hs.Add(new Rect2(x0, z0, x1 - x0, z1 - z0));
                    xs.Add(x0); xs.Add(x1);
                }

            xs.Sort();

            for (int i = 0; i < xs.Count - 1; i++)
            {
                float x0 = xs[i], x1 = xs[i + 1];
                if (x1 - x0 < 0.02f) continue;

                List<float[]> cuts = new List<float[]>();
                foreach (Rect2 hrect in hs)
                    if (hrect.x <= x0 + 0.01f && hrect.x + hrect.w >= x1 - 0.01f)
                        cuts.Add(new float[] { hrect.z, hrect.z + hrect.d });
                cuts.Sort((a, b) => a[0].CompareTo(b[0]));

                float cur = 0f;
                foreach (float[] cut in cuts)
                {
                    if (cut[0] > cur + 0.02f)
                        Box(parent, label, new Vector3((x0 + x1) * 0.5f, yCenter, (cur + cut[0]) * 0.5f),
                            new Vector3(x1 - x0, 0.2f, cut[0] - cur), c, markStatic);
                    cur = Mathf.Max(cur, cut[1]);
                }
                if (d > cur + 0.02f)
                    Box(parent, label, new Vector3((x0 + x1) * 0.5f, yCenter, (cur + d) * 0.5f),
                        new Vector3(x1 - x0, 0.2f, d - cur), c, markStatic);
            }
        }

        void BuildStairs(Transform parent, Stair2 s)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(s.rise) / StepH));
            float sign = s.rise < 0f ? -1f : 1f;
            Vector3 dir = Normal(s.dir);

            Transform g = Group("Stairs_" + (sign > 0 ? "up" : "down"), parent);
            for (int i = 0; i < n; i++)
            {
                Vector3 c = new Vector3(s.x, 0f, s.z) + dir * (StepD * (i + 0.5f));
                c.y = sign * StepH * (i + 0.5f);
                Vector3 size = (dir.x != 0f)
                    ? new Vector3(StepD, StepH, s.width)
                    : new Vector3(s.width, StepH, StepD);
                Box(g, "Step_" + (i + 1), c, size, new Color(0.30f, 0.30f, 0.32f), true);
            }
        }

        Vector3 Centroid(InternalRoom r, float w, float d)
        {
            if (r.notch == null) return new Vector3(w * 0.5f, 0f, d * 0.5f);
            float A = w * d, An = r.notch.w * r.notch.d;
            float cx = (A * (w * 0.5f) - An * (r.notch.x + r.notch.w * 0.5f)) / (A - An);
            float cz = (A * (d * 0.5f) - An * (r.notch.z + r.notch.d * 0.5f)) / (A - An);
            return new Vector3(cx, 0f, cz);
        }

        // ====================================================================
        //  PRIMITIVES
        // ====================================================================
        Transform Group(string n, Transform parent)
        {
            GameObject go = new GameObject(n);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static string SafeName(string s) { return string.IsNullOrEmpty(s) ? "Room" : s.Replace("\n", " "); }

        Transform Box(Transform parent, string n, Vector3 localPos, Vector3 size, Color c, bool markStatic)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(c);
            go.isStatic = markStatic;
            return go.transform;
        }

        Material Mat(Color c)
        {
            Material m;
            if (_mats.TryGetValue(c, out m) && m != null) return m;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("HDRP/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Diffuse");
            m = new Material(sh);
            m.name = "HotWar_" + ((int)(c.r * 255)).ToString("X2") + ((int)(c.g * 255)).ToString("X2") + ((int)(c.b * 255)).ToString("X2");
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            _mats[c] = m;
            return m;
        }

        void Sign(Transform parent, string text, Vector3 localPos, float yRot, float sizeMul, Color? col)
        {
            GameObject go = new GameObject("Sign_" + (text.Length > 24 ? text.Substring(0, 24) : text).Replace("\n", " "));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = 0.035f * sizeMul;
            tm.color = col ?? new Color(0.95f, 0.90f, 0.72f);
            Font f = LoadBuiltinFont();
            if (f != null)
            {
                tm.font = f;
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null && f.material != null) mr.sharedMaterial = f.material;
            }
        }

        static Font LoadBuiltinFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null)
            {
                try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
            return f;
        }

        void PointLight(Transform parent, Vector3 localPos, Color c, float intensity, float range)
        {
            GameObject go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
        }

        public static string FmtT(float seconds)
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return string.Format("{0:00}:{1:00}", t / 60, t % 60);
        }

        static void RegisterUndo(GameObject go, string label)
        {
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, label);
#endif
        }

    }
}
