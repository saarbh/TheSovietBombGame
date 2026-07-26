// ============================================================================
//  THE HOT WAR — HW_TestLevel.cs   (OPTIONAL — for testing, not for the ship scene)
//  Purpose: prove the WHOLE gameplay loop works, in a brand-new empty scene,
//  in under a minute — independent of the main scene's state.
//
//    1. New empty scene.
//    2. Empty GameObject -> Add Component -> The Hot War -> TEST LEVEL.
//    3. Press Play. Full game: timer, doors, cards, 4 puzzles, phone, endings.
//
//  When integrating into the real scene you do NOT use this file — you place
//  the HW_* components on your own prefabs (see HOTWAR_SETUP.md). This file
//  is the reference for how everything wires together.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace TheHotWar
{
    [AddComponentMenu("The Hot War/TEST LEVEL (builds everything on Play)")]
    public class HW_TestLevel : MonoBehaviour
    {
        [Header("Countdown")]
        public float totalTime = 420f;
        public float[] realUnlockElapsed = { 60f, 360f, 300f, 180f };
        public float[] shownUnlockElapsed = { 60f, 180f, 300f, 360f };

        [Header("Layout")]
        public float hallwayLength = 40f;
        public float hallwayWidth = 4f;
        public float roomSize = 8f;
        public float wallHeight = 3.2f;

        [Header("Debug")]
        public bool showDebugHud = true;

        const float WallT = 0.3f, DoorW = 1.8f, DoorH = 2.4f;

        static readonly string[] RoomNames =
            { "RADAR ARRAY", "SATELLITE TELEMETRY", "COMMUNICATIONS", "COMPUTER HALL" };
        static readonly string[] RoomCodes = { "1983", "0925", "0026", "0015" };
        static readonly string[] Flavour =
        {
            "RADAR ARRAY verified: ground radar sweeps show NO inbound tracks.",
            "SATELLITE TELEMETRY verified: the OKO return is sunlight glare on high clouds, not launches.",
            "COMMUNICATIONS verified: no strike chatter on any monitored frequency.",
            "COMPUTER HALL verified: the M-4 array flags the alert pattern as a sensor fault."
        };

        static readonly Color[] Accent =
        {
            new Color(0.20f, 0.50f, 0.30f), new Color(0.60f, 0.20f, 0.20f),
            new Color(0.22f, 0.32f, 0.58f), new Color(0.58f, 0.46f, 0.15f)
        };

        readonly Dictionary<Color, Material> _mats = new Dictionary<Color, Material>();
        Transform _root;

        void Start()
        {
            if (GameObject.Find("HOTWAR_TESTLEVEL") != null) return;
            Build();
        }

        void Build()
        {
            _root = new GameObject("HOTWAR_TESTLEVEL").transform;
            float halfHall = hallwayWidth * 0.5f, halfR = roomSize * 0.5f, L = hallwayLength;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.28f, 0.30f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.028f;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.06f);

            // ---- control room 12x10 (x -6..6, z -10..0) ---------------------
            Box("Floor.Ctrl", new Vector3(0, -0.1f, -5), new Vector3(12, 0.2f, 10), new Color(0.25f, 0.24f, 0.19f));
            WallX(-10, -6, 6, null);
            WallZ(-6, -10, 0, null);
            WallZ(6, -10, 0, null);
            WallX(0, -6, 6, new float[] { 0f });

            // control-room exit zone (trigger)
            GameObject zone = new GameObject("ControlRoomZone");
            zone.transform.SetParent(_root, false);
            zone.transform.localPosition = new Vector3(0, 1.6f, -4.65f);
            BoxCollider bc = zone.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(11.4f, 3.2f, 10.1f);
            zone.AddComponent<HW_ControlRoomZone>();

            Sign("DUTY STATION 07\nDO NOT ABANDON POST", new Vector3(-2.9f, 2.55f, -0.12f), 0, 0.9f, new Color(0.9f, 0.55f, 0.4f));

            // ---- corridor ---------------------------------------------------
            Box("Floor.Hall", new Vector3(0, -0.1f, L * 0.5f), new Vector3(hallwayWidth, 0.2f, L), new Color(0.17f, 0.17f, 0.18f));
            int[] side = { -1, 0, -1, 1 };
            float[] doorZ = { L * 0.25f, L, L * 0.70f, L * 0.45f };
            WallZ(-halfHall, 0, L, new float[] { doorZ[0], doorZ[2] });
            WallZ(halfHall, 0, L, new float[] { doorZ[3] });
            PLight(new Vector3(0, 2.8f, L * 0.2f), Color.white, 1f, 9f);
            PLight(new Vector3(0, 2.8f, L * 0.55f), Color.white, 1f, 9f);
            PLight(new Vector3(0, 2.8f, L * 0.9f), new Color(1f, 0.85f, 0.8f), 1f, 9f);

            // ---- rooms + doors + puzzles ------------------------------------
            for (int i = 0; i < 4; i++)
            {
                float rx0, rx1, rz0, rz1;
                Vector3 doorPos, doorScale, lampA, lampB, signPos; float signRot;

                if (side[i] == 0)
                {
                    rx0 = -halfR; rx1 = halfR; rz0 = L; rz1 = L + roomSize;
                    WallX(rz0, rx0, rx1, new float[] { 0f });
                    WallX(rz1, rx0, rx1, null);
                    WallZ(rx0, rz0, rz1, null);
                    WallZ(rx1, rz0, rz1, null);
                    doorPos = new Vector3(0, DoorH * 0.5f, L);
                    doorScale = new Vector3(DoorW - 0.06f, DoorH, WallT - 0.04f);
                    lampA = new Vector3(0, 2.62f, L - 0.24f); lampB = new Vector3(0, 2.62f, L + 0.24f);
                    signPos = new Vector3(0, 2.95f, L - 0.18f); signRot = 0;
                }
                else if (side[i] < 0)
                {
                    rx1 = -halfHall; rx0 = rx1 - roomSize;
                    rz0 = doorZ[i] - halfR; rz1 = doorZ[i] + halfR;
                    WallX(rz0, rx0, rx1, null); WallX(rz1, rx0, rx1, null); WallZ(rx0, rz0, rz1, null);
                    doorPos = new Vector3(rx1, DoorH * 0.5f, doorZ[i]);
                    doorScale = new Vector3(WallT - 0.04f, DoorH, DoorW - 0.06f);
                    lampA = new Vector3(rx1 + 0.24f, 2.62f, doorZ[i]); lampB = new Vector3(rx1 - 0.24f, 2.62f, doorZ[i]);
                    signPos = new Vector3(rx1 + 0.18f, 2.95f, doorZ[i]); signRot = -90;
                }
                else
                {
                    rx0 = halfHall; rx1 = rx0 + roomSize;
                    rz0 = doorZ[i] - halfR; rz1 = doorZ[i] + halfR;
                    WallX(rz0, rx0, rx1, null); WallX(rz1, rx0, rx1, null); WallZ(rx1, rz0, rz1, null);
                    doorPos = new Vector3(rx0, DoorH * 0.5f, doorZ[i]);
                    doorScale = new Vector3(WallT - 0.04f, DoorH, DoorW - 0.06f);
                    lampA = new Vector3(rx0 - 0.24f, 2.62f, doorZ[i]); lampB = new Vector3(rx0 + 0.24f, 2.62f, doorZ[i]);
                    signPos = new Vector3(rx0 - 0.18f, 2.95f, doorZ[i]); signRot = 90;
                }

                Vector3 rc = new Vector3((rx0 + rx1) * 0.5f, 0, (rz0 + rz1) * 0.5f);
                Box("Floor." + RoomNames[i], new Vector3(rc.x, -0.1f, rc.z),
                    new Vector3(rx1 - rx0, 0.2f, rz1 - rz0), Color.Lerp(new Color(0.17f, 0.17f, 0.18f), Accent[i], 0.22f));
                PLight(new Vector3(rc.x, 2.6f, rc.z), Color.Lerp(Color.white, Accent[i], 0.5f), 1.2f, 9f);

                // door
                Transform slab = Box("Door_" + (i + 1), doorPos, doorScale, new Color(0.48f, 0.13f, 0.10f));
                Transform la = Box("LampA", lampA, new Vector3(0.18f, 0.18f, 0.18f), Color.red);
                Transform lb = Box("LampB", lampB, new Vector3(0.18f, 0.18f, 0.18f), Color.red);
                HW_Door d = slab.gameObject.AddComponent<HW_Door>();
                d.roomIndex = i; d.roomName = RoomNames[i]; d.code = RoomCodes[i];
                d.realUnlockElapsed = Get(realUnlockElapsed, i, 60f * (i + 1));
                d.shownUnlockElapsed = Get(shownUnlockElapsed, i, 60f * (i + 1));
                d.lampRenderers = new Renderer[] { la.GetComponent<Renderer>(), lb.GetComponent<Renderer>() };
                Sign(RoomNames[i] + "\nPLATE: OPENS AT T-" + HW_UI.FmtT(totalTime - d.shownUnlockElapsed), signPos, signRot, 0.85f, null);

                // console + puzzle + clue note
                Vector3 conPos = side[i] == 0 ? new Vector3(0, 0.55f, rz1 - 0.8f)
                              : side[i] < 0 ? new Vector3(rx0 + 0.8f, 0.55f, doorZ[i])
                                            : new Vector3(rx1 - 0.8f, 0.55f, doorZ[i]);
                Vector3 conScale = side[i] == 0 ? new Vector3(1.8f, 1.1f, 0.9f) : new Vector3(0.9f, 1.1f, 1.8f);
                Transform console = Box("Console_" + RoomNames[i], conPos, conScale, new Color(0.20f, 0.23f, 0.28f));

                HW_PuzzleBase pz;
                Vector3 cluePos = side[i] == 0 ? new Vector3(-2.5f, 1.5f, rz1 - 0.35f)
                               : new Vector3(conPos.x, 1.5f, doorZ[i] + 2.5f);
                switch (i)
                {
                    case 0:
                        HW_Puzzle_SwitchPattern p0 = console.gameObject.AddComponent<HW_Puzzle_SwitchPattern>();
                        p0.requiredPattern = "UDDU"; pz = p0;
                        Clue(cluePos, "[E] Read the duty card",
                             "SWEEP CHECK — REGULATION 7:\n1 UP   2 DOWN   3 DOWN   4 UP");
                        break;
                    case 1:
                        HW_Puzzle_Needle p1 = console.gameObject.AddComponent<HW_Puzzle_Needle>();
                        pz = p1;
                        Clue(cluePos, "[E] Read the calibration slip",
                             "GLARE TEST: three clean captures inside the band\nproves it's sunlight, not exhaust plumes.");
                        break;
                    case 2:
                        HW_Puzzle_Frequency p2 = console.gameObject.AddComponent<HW_Puzzle_Frequency>();
                        p2.targetKHz = 4625; pz = p2;
                        Clue(cluePos, "[E] Read the taped note",
                             "DUTY NET (do not write down, comrade):\n4625 kHz");
                        break;
                    default:
                        HW_Puzzle_Checksum p3 = console.gameObject.AddComponent<HW_Puzzle_Checksum>();
                        p3.checksum = "0926"; pz = p3;
                        Clue(cluePos, "[E] Read the M-4 printout",
                             "M-4 RUN 0347 — TO RE-RUN ANALYSIS\nENTER CHECKSUM: 0926");
                        break;
                }
                pz.roomIndex = i; pz.roomName = RoomNames[i]; pz.solvedFlavour = Flavour[i];
            }

            // ---- control room props ----------------------------------------
            Box("Desk", new Vector3(0, 0.45f, -9.1f), new Vector3(7.5f, 0.9f, 1.1f), new Color(0.36f, 0.26f, 0.18f));
            Transform phone = Box("Phone", new Vector3(-3f, 1.02f, -9.1f), new Vector3(0.5f, 0.25f, 0.35f), new Color(0.85f, 0.1f, 0.08f));
            phone.gameObject.AddComponent<HW_Phone>();
            Sign("DUTY LINE", new Vector3(-3f, 1.9f, -9.82f), 180, 0.8f, null);

            for (int i = 0; i < 4; i++)
            {
                Transform card = Box("Card_" + (i + 1), new Vector3(-1.05f + i * 0.7f, 1.12f, -9.1f),
                                     new Vector3(0.34f, 0.42f, 0.08f), Accent[i]);
                HW_CodeCard cc = card.gameObject.AddComponent<HW_CodeCard>();
                cc.roomIndex = i; cc.roomName = RoomNames[i]; cc.code = RoomCodes[i];
            }
            Sign("CODE CARDS", new Vector3(0.35f, 1.9f, -9.82f), 180, 0.8f, null);

            Clue(new Vector3(5.8f, 1.5f, -4.5f), "[E] Read the maintenance log",
                 "MAINT LOG, 1979: during repainting, the door plates for\nSATELLITE TELEMETRY and COMPUTER HALL were swapped.\nNever corrected. The paint lies; the wiring does not.");
            Sign("MAINTENANCE LOG", new Vector3(5.82f, 2.25f, -4.5f), 90, 0.75f, null);

            Transform ovr = Box("Override", new Vector3(-5.55f, 0.65f, -4.5f), new Vector3(0.8f, 1.3f, 0.8f), new Color(0.20f, 0.23f, 0.28f));
            Transform ovrLamp = Box("OvrLamp", new Vector3(-5.55f, 1.46f, -4.5f), new Vector3(0.16f, 0.16f, 0.16f), Color.yellow);
            HW_OverrideTerminal ot = ovr.gameObject.AddComponent<HW_OverrideTerminal>();
            ot.lampRenderer = ovrLamp.GetComponent<Renderer>();
            Sign("MECHANICAL OVERRIDE", new Vector3(-5.82f, 2.25f, -4.5f), -90, 0.75f, null);

            // ---- player + camera -------------------------------------------
            foreach (Camera c in Camera.allCameras) if (c != null) c.gameObject.SetActive(false);
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0, 0.05f, -5);
            CharacterController cc2 = player.AddComponent<CharacterController>();
            cc2.height = 1.8f; cc2.radius = 0.38f; cc2.center = new Vector3(0, 0.95f, 0);
            GameObject camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.62f, 0);
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            camGo.AddComponent<AudioListener>();
            HW_Player pc = player.AddComponent<HW_Player>();
            pc.cam = camGo.transform;
            HW_Interactor inter = camGo.AddComponent<HW_Interactor>();
            inter.rayOrigin = camGo.transform;

            // ---- manager + UI ----------------------------------------------
            GameObject gmGo = new GameObject("HW_Game");
            HW_Game gm = gmGo.AddComponent<HW_Game>();
            gm.totalTime = totalTime;
            gm.mainMenuScene = ""; // test scene: no menu hop
            HW_UI ui = gmGo.AddComponent<HW_UI>();
            ui.showDebug = showDebugHud;
        }

        // ---- tiny builders --------------------------------------------------
        static float Get(float[] a, int i, float f) { return (a != null && i < a.Length) ? a[i] : f; }

        void Clue(Vector3 pos, string prompt, string text)
        {
            Transform t = Box("Clue", pos, new Vector3(0.5f, 0.65f, 0.06f), new Color(0.85f, 0.8f, 0.62f));
            HW_Note n = t.gameObject.AddComponent<HW_Note>();
            n.promptText = prompt; n.noteText = text; n.noteSeconds = 8f;
        }

        Transform Box(string n, Vector3 pos, Vector3 size, Color c)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(c);
            return go.transform;
        }

        void WallZ(float x, float z0, float z1, float[] doorZs)
        {
            List<float> ds = doorZs == null ? new List<float>() : new List<float>(doorZs);
            ds.Sort();
            float cur = z0;
            foreach (float dz in ds)
            {
                float g0 = dz - DoorW * 0.5f, g1 = dz + DoorW * 0.5f;
                if (g0 > cur + 0.01f)
                    Box("Wall", new Vector3(x, wallHeight * 0.5f, (cur + g0) * 0.5f), new Vector3(WallT, wallHeight, g0 - cur), new Color(0.36f, 0.37f, 0.35f));
                Box("Lintel", new Vector3(x, (DoorH + wallHeight) * 0.5f, dz), new Vector3(WallT, wallHeight - DoorH, DoorW), new Color(0.36f, 0.37f, 0.35f));
                cur = g1;
            }
            if (z1 > cur + 0.01f)
                Box("Wall", new Vector3(x, wallHeight * 0.5f, (cur + z1) * 0.5f), new Vector3(WallT, wallHeight, z1 - cur), new Color(0.36f, 0.37f, 0.35f));
        }

        void WallX(float z, float x0, float x1, float[] doorXs)
        {
            List<float> ds = doorXs == null ? new List<float>() : new List<float>(doorXs);
            ds.Sort();
            float cur = x0;
            foreach (float dx in ds)
            {
                float g0 = dx - DoorW * 0.5f, g1 = dx + DoorW * 0.5f;
                if (g0 > cur + 0.01f)
                    Box("Wall", new Vector3((cur + g0) * 0.5f, wallHeight * 0.5f, z), new Vector3(g0 - cur, wallHeight, WallT), new Color(0.42f, 0.40f, 0.33f));
                Box("Lintel", new Vector3(dx, (DoorH + wallHeight) * 0.5f, z), new Vector3(DoorW, wallHeight - DoorH, WallT), new Color(0.42f, 0.40f, 0.33f));
                cur = g1;
            }
            if (x1 > cur + 0.01f)
                Box("Wall", new Vector3((cur + x1) * 0.5f, wallHeight * 0.5f, z), new Vector3(x1 - cur, wallHeight, WallT), new Color(0.42f, 0.40f, 0.33f));
        }

        Material Mat(Color c)
        {
            Material m;
            if (_mats.TryGetValue(c, out m)) return m;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("HDRP/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Diffuse");
            m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            _mats[c] = m;
            return m;
        }

        void Sign(string text, Vector3 pos, float yRot, float size, Color? col)
        {
            GameObject go = new GameObject("Sign");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = 0.035f * size;
            tm.color = col ?? new Color(0.95f, 0.90f, 0.72f);
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            if (f != null)
            {
                tm.font = f;
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null && f.material != null) mr.sharedMaterial = f.material;
            }
        }

        void PLight(Vector3 pos, Color c, float i, float r)
        {
            GameObject go = new GameObject("Light");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = i; l.range = r;
        }
    }
}
