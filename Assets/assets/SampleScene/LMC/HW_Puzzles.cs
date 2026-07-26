// ============================================================================
//  THE HOT WAR — HW_Puzzles.cs
//  Four verification puzzles, one per room. Each is ONE component on ONE
//  console object (any mesh with a collider). Press E to sit down at the
//  console (focus mode: movement locks, panel opens), solve with the
//  keyboard, Esc to stand up. Each pairs with an HW_Note placed in the same
//  room that holds the clue, so the player has to look around.
//
//  Room mapping per the design doc:
//    0 RADAR ARRAY          -> HW_Puzzle_SwitchPattern
//    1 SATELLITE TELEMETRY  -> HW_Puzzle_Needle
//    2 COMMUNICATIONS       -> HW_Puzzle_Frequency
//    3 COMPUTER HALL        -> HW_Puzzle_Checksum
//  (The components don't enforce this mapping — put any puzzle in any room
//   via its Room Index field.)
// ============================================================================
using UnityEngine;

namespace TheHotWar
{
    // ------------------------------------------------------------------------
    public abstract class HW_PuzzleBase : HW_Interactable
    {
        [Tooltip("0..3 — which room this verifies.")]
        public int roomIndex;
        public string roomName = "ROOM";
        [TextArea] public string solvedFlavour = "System verified: false alarm signature.";

        [System.NonSerialized] public bool solved;

        protected bool Focused { get { return GM != null && GM.focusedPuzzle == this; } }

        public override string Prompt
        {
            get
            {
                if (solved) return roomName + " — VERIFIED: false alarm signature.";
                return "[E] Use console — " + roomName;
            }
        }

        public override void OnPress()
        {
            if (solved || GM == null) return;
            GM.Focus(this);
            OnFocused();
        }

        protected virtual void OnFocused() { }

        protected void Solve()
        {
            if (solved) return;
            solved = true;
            if (GM != null)
            {
                GM.SolveRoom(roomIndex, solvedFlavour);
                GM.Blur();
            }
        }

        protected void Exit() { if (GM != null) GM.Blur(); }

        void Update()
        {
            if (!Focused) return;
            if (HotInput.EscapeDown) { Exit(); return; }
            FocusedUpdate();
        }

        protected abstract void FocusedUpdate();

        // Panel drawing (called from its own OnGUI while focused)
        void OnGUI()
        {
            if (!Focused) return;
            float s = Screen.height / 1080f;
            float w = 900f * s, h = 460f * s;
            Rect box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.color = new Color(0.02f, 0.03f, 0.02f, 0.96f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            DrawPanel(box, s);

            GUIStyle foot = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter };
            foot.fontSize = Mathf.RoundToInt(20f * s);
            foot.normal.textColor = new Color(0.6f, 0.65f, 0.6f);
            GUI.Label(new Rect(box.x, box.y + box.height - 36f * s, box.width, 30f * s), "[ESC] step away", foot);
        }

        protected abstract void DrawPanel(Rect box, float s);

        protected static GUIStyle PanelStyle(float s, int size, Color c)
        {
            GUIStyle st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
            st.fontSize = Mathf.RoundToInt(size * s);
            st.normal.textColor = c;
            return st;
        }
    }

    // ------------------------------------------------------------------------
    //  RADAR ARRAY — set the four sweep switches to the duty-card pattern.
    //  The pattern lives on an HW_Note elsewhere in the room.
    // ------------------------------------------------------------------------
    [AddComponentMenu("The Hot War/Puzzle - Switch Pattern (Radar)")]
    public class HW_Puzzle_SwitchPattern : HW_PuzzleBase
    {
        [Tooltip("U = up, D = down. Must match the clue note in the room.")]
        public string requiredPattern = "UDDU";

        bool[] _up = { false, false, false, false };

        protected override void FocusedUpdate()
        {
            int d;
            if (HotInput.DigitDown(out d) && d >= 1 && d <= 4)
                _up[d - 1] = !_up[d - 1];

            if (HotInput.InteractDown)
            {
                if (Matches()) Solve();
                else if (GM != null) GM.ShowNote("Sweep pattern rejected. Check the duty card posted in this room.", 3f);
            }
        }

        bool Matches()
        {
            string req = (requiredPattern + "UUUU").Substring(0, 4).ToUpper();
            for (int i = 0; i < 4; i++)
            {
                bool wantUp = req[i] == 'U';
                if (_up[i] != wantUp) return false;
            }
            return true;
        }

        protected override void DrawPanel(Rect box, float s)
        {
            string sw = "";
            for (int i = 0; i < 4; i++) sw += (i + 1) + ":[" + (_up[i] ? "UP" : "DN") + "]   ";
            GUI.Label(new Rect(box.x, box.y + 24f * s, box.width, box.height),
                "RADAR ARRAY — SWEEP VERIFICATION\n\n" +
                "Set the four sweep selectors to the pattern on the duty card\n" +
                "posted in this room, then confirm.\n\n" +
                sw + "\n\n[1-4] flip selector      [E] CONFIRM SWEEP",
                PanelStyle(s, 30, new Color(0.7f, 0.95f, 0.75f)));
        }
    }

    // ------------------------------------------------------------------------
    //  SATELLITE TELEMETRY — catch the drifting return inside the calibration
    //  band three times. Pure timing; no clue needed (the note adds flavour).
    // ------------------------------------------------------------------------
    [AddComponentMenu("The Hot War/Puzzle - Needle (Telemetry)")]
    public class HW_Puzzle_Needle : HW_PuzzleBase
    {
        [Tooltip("How many clean captures are needed.")]
        public int capturesNeeded = 3;
        [Tooltip("Needle sweep speed. Higher = harder.")]
        public float speed = 1.6f;
        [Tooltip("Half-width of the green band, 0..1. Smaller = harder.")]
        public float band = 0.16f;

        int _captures;
        float _t;

        protected override void OnFocused() { _captures = 0; }

        protected override void FocusedUpdate()
        {
            _t += Time.deltaTime * speed;
            if (HotInput.InteractDown)
            {
                float v = Mathf.Sin(_t);
                if (Mathf.Abs(v) <= band)
                {
                    _captures++;
                    if (_captures >= capturesNeeded) Solve();
                }
                else
                {
                    _captures = Mathf.Max(0, _captures - 1);
                    if (GM != null) GM.ShowNote("Capture outside calibration band — signal lost.", 2f);
                }
            }
        }

        protected override void DrawPanel(Rect box, float s)
        {
            float v = Mathf.Sin(_t); // -1..1
            int width = 41;
            int pos = Mathf.Clamp(Mathf.RoundToInt((v * 0.5f + 0.5f) * (width - 1)), 0, width - 1);
            int b0 = Mathf.RoundToInt((0.5f - band * 0.5f) * (width - 1));
            int b1 = Mathf.RoundToInt((0.5f + band * 0.5f) * (width - 1));
            string bar = "";
            for (int i = 0; i < width; i++)
                bar += (i == pos) ? "█" : (i >= b0 && i <= b1 ? "▓" : "·");

            GUI.Label(new Rect(box.x, box.y + 24f * s, box.width, box.height),
                "SATELLITE TELEMETRY — GLARE DISCRIMINATION\n\n" +
                "Capture the return while it sits inside the calibration band.\n" +
                "Clean captures: " + _captures + " / " + capturesNeeded + "\n\n" +
                bar + "\n\n[E] CAPTURE when the mark is inside the band",
                PanelStyle(s, 30, new Color(0.95f, 0.8f, 0.6f)));
        }
    }

    // ------------------------------------------------------------------------
    //  COMMUNICATIONS — tune the receiver to the net frequency taped up in
    //  the room, then confirm silence on the net.
    // ------------------------------------------------------------------------
    [AddComponentMenu("The Hot War/Puzzle - Frequency (Comms)")]
    public class HW_Puzzle_Frequency : HW_PuzzleBase
    {
        [Tooltip("The correct frequency in kHz. Must match the clue note in the room.")]
        public int targetKHz = 4625;
        public int minKHz = 4000;
        public int maxKHz = 5000;
        public int stepKHz = 25;

        int _freq;

        protected override void OnFocused() { _freq = minKHz; }

        protected override void FocusedUpdate()
        {
            if (HotInput.Num1Down) _freq -= stepKHz;
            if (HotInput.Num2Down) _freq += stepKHz;
            if (_freq < minKHz) _freq = maxKHz;
            if (_freq > maxKHz) _freq = minKHz;

            if (HotInput.InteractDown)
            {
                if (_freq == targetKHz) Solve();
                else if (GM != null) GM.ShowNote("Static. Wrong net — the frequency is written somewhere in this room.", 3f);
            }
        }

        protected override void DrawPanel(Rect box, float s)
        {
            int dist = Mathf.Abs(_freq - targetKHz);
            string signal = dist == 0 ? "CARRIER LOCKED — NET SILENT"
                          : dist <= 50 ? "signal... almost..."
                          : dist <= 150 ? "faint signal in the static"
                          : "static";
            GUI.Label(new Rect(box.x, box.y + 24f * s, box.width, box.height),
                "COMMUNICATIONS — NET CHECK\n\n" +
                "Tune the receiver to the duty net and confirm no strike traffic.\n\n" +
                "FREQ:  " + _freq + " kHz\n(" + signal + ")\n\n" +
                "[1] tune down    [2] tune up    [E] CONFIRM NET SILENT",
                PanelStyle(s, 30, new Color(0.65f, 0.8f, 0.98f)));
        }
    }

    // ------------------------------------------------------------------------
    //  COMPUTER HALL — type the checksum from the M-4 printout note.
    // ------------------------------------------------------------------------
    [AddComponentMenu("The Hot War/Puzzle - Checksum (Computer Hall)")]
    public class HW_Puzzle_Checksum : HW_PuzzleBase
    {
        [Tooltip("4 digits. Must match the printout note in the room.")]
        public string checksum = "0926";

        string _typed = "";

        protected override void OnFocused() { _typed = ""; }

        protected override void FocusedUpdate()
        {
            int d;
            if (HotInput.DigitDown(out d))
            {
                _typed += d.ToString();
                if (_typed.Length >= 4)
                {
                    if (_typed == checksum) Solve();
                    else
                    {
                        _typed = "";
                        if (GM != null) GM.ShowNote("CHECKSUM MISMATCH. The correct sum is on the M-4 printout in this room.", 3f);
                    }
                }
            }
        }

        protected override void DrawPanel(Rect box, float s)
        {
            string shown = _typed;
            while (shown.Length < 4) shown += "_";
            GUI.Label(new Rect(box.x, box.y + 24f * s, box.width, box.height),
                "COMPUTER HALL — M-4 RE-RUN\n\n" +
                "Re-enter the checksum from tonight's printout to re-run the\n" +
                "alert analysis on the M-4 array.\n\n" +
                "CHECKSUM:  " + shown + "\n\n[0-9] enter digits",
                PanelStyle(s, 30, new Color(0.95f, 0.9f, 0.6f)));
        }
    }
}
