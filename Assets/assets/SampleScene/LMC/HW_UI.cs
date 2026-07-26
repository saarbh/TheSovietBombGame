// ============================================================================
//  THE HOT WAR — HW_UI.cs
//  All HUD in one component (add next to HW_Game). Deliberately OnGUI so it
//  needs ZERO canvas setup and works the second it's in the scene. When Shai
//  builds the real uGUI, replace this component — everything it reads is
//  public on HW_Game, so the swap is one-way and painless.
// ============================================================================
using UnityEngine;

namespace TheHotWar
{
    [AddComponentMenu("The Hot War/UI (HUD + phone + end screens)")]
    public class HW_UI : MonoBehaviour
    {
        [Tooltip("Show the debug overlay (door times, flags).")]
        public bool showDebug = false;

        GUIStyle _big, _mid, _small, _promptStyle, _noteStyle, _debugStyle;

        void EnsureStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _mid = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _noteStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true, fontStyle = FontStyle.Bold };
            _debugStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft };
        }

        void OnGUI()
        {
            HW_Game gm = HW_Game.I;
            if (gm == null) return;
            EnsureStyles();

            float s = Screen.height / 1080f;
            float w = Screen.width;

            if (gm.state == HW_Game.GameState.Playing)
            {
                // the watch
                float rem = gm.Remaining;
                _big.fontSize = Mathf.RoundToInt(78f * s);
                Color tcol = rem <= 60f ? new Color(1f, 0.25f, 0.2f) : new Color(0.92f, 0.9f, 0.8f);
                if (rem <= 10f && Mathf.FloorToInt(rem * 4f) % 2 == 0) tcol.a = 0.25f;
                _big.normal.textColor = tcol;
                GUI.Label(new Rect(0f, 18f * s, w, 90f * s), "T-" + FmtT(rem), _big);

                _mid.fontSize = Mathf.RoundToInt(26f * s);
                _mid.normal.textColor = new Color(0.8f, 0.8f, 0.72f);
                GUI.Label(new Rect(0f, 104f * s, w, 34f * s),
                          "SYSTEMS VERIFIED " + gm.SolvedCount + "/4      CODE CARDS " + gm.CardCount + "/4", _mid);

                // crosshair (hidden while a panel is open)
                if (!gm.phoneOpen && gm.focusedPuzzle == null)
                {
                    _small.fontSize = Mathf.RoundToInt(26f * s);
                    _small.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
                    GUI.Label(new Rect(0f, Screen.height * 0.5f - 15f * s, w, 30f * s), "·", _small);
                }

                // interact prompt
                if (!gm.phoneOpen && gm.focusedPuzzle == null && !string.IsNullOrEmpty(gm.prompt))
                {
                    _promptStyle.fontSize = Mathf.RoundToInt(28f * s);
                    _promptStyle.normal.textColor = new Color(0.95f, 0.95f, 0.9f);
                    GUI.Label(new Rect(w * 0.1f, Screen.height - 120f * s, w * 0.8f, 60f * s), gm.prompt, _promptStyle);
                }

                // note popup
                if (!string.IsNullOrEmpty(gm.note))
                {
                    _noteStyle.fontSize = Mathf.RoundToInt(26f * s);
                    _noteStyle.normal.textColor = new Color(1f, 0.85f, 0.45f);
                    GUI.Label(new Rect(w * 0.12f, Screen.height - 280f * s, w * 0.76f, 150f * s), gm.note, _noteStyle);
                }

                if (gm.phoneOpen) DrawPhone(gm, s, w);
                if (showDebug) DrawDebug(gm, s);
            }
            else
            {
                GUI.color = new Color(0f, 0f, 0f, 0.92f);
                GUI.DrawTexture(new Rect(0f, 0f, w, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                _big.fontSize = Mathf.RoundToInt(88f * s);
                _big.normal.textColor = gm.state == HW_Game.GameState.Won
                    ? new Color(0.75f, 0.95f, 0.75f)
                    : new Color(1f, 0.3f, 0.25f);
                GUI.Label(new Rect(0f, Screen.height * 0.24f, w, 110f * s), gm.endTitle, _big);

                _mid.fontSize = Mathf.RoundToInt(30f * s);
                _mid.normal.textColor = new Color(0.9f, 0.88f, 0.8f);
                GUI.Label(new Rect(w * 0.1f, Screen.height * 0.24f + 130f * s, w * 0.8f, 320f * s), gm.endBody, _mid);

                string opts = "[ R ]  RESTART";
                if (!string.IsNullOrEmpty(gm.mainMenuScene)) opts += "        [ M ]  MAIN MENU";
                _small.fontSize = Mathf.RoundToInt(26f * s);
                _small.normal.textColor = new Color(0.7f, 0.7f, 0.65f);
                GUI.Label(new Rect(0f, Screen.height * 0.84f, w, 40f * s), opts, _small);
            }
        }

        void DrawPhone(HW_Game gm, float s, float w)
        {
            float bw = 780f * s, bh = 380f * s;
            Rect box = new Rect((w - bw) * 0.5f, (Screen.height - bh) * 0.5f, bw, bh);
            GUI.color = new Color(0.02f, 0.02f, 0.03f, 0.95f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            _mid.fontSize = Mathf.RoundToInt(30f * s);
            _mid.normal.textColor = new Color(0.95f, 0.9f, 0.75f);
            string txt;
            if (gm.AllSolved)
                txt = "DUTY LINE — GENERAL STAFF\n\nAll four systems verified.\n\n" +
                      "[1]  REPORT FALSE ALARM\n" +
                      "[2]  REPORT MISSILE ATTACK IN PROGRESS\n" +
                      "[3 / ESC]  HANG UP";
            else
                txt = "DUTY LINE — GENERAL STAFF\n\nSystems verified: " + gm.SolvedCount + "/4.\n" +
                      "The duty script permits only one report on partial data.\n\n" +
                      "[1]  \"MISSILE ATTACK IN PROGRESS\"\n" +
                      "[2 / ESC]  HANG UP";
            GUI.Label(new Rect(box.x + 30f * s, box.y + 24f * s, box.width - 60f * s, box.height - 48f * s), txt, _mid);
        }

        void DrawDebug(HW_Game gm, float s)
        {
            _debugStyle.fontSize = Mathf.RoundToInt(18f * s);
            _debugStyle.normal.textColor = new Color(0.6f, 0.85f, 0.6f, 0.85f);
            string t = "DEBUG   elapsed " + Mathf.FloorToInt(gm.elapsed) + "s   leftControlRoom: " + gm.leftControlRoom +
                       "   solved " + gm.SolvedCount + "/4   cards " + gm.CardCount + "/4\n";
            GUI.Label(new Rect(14f * s, 14f * s, 900f * s, 200f * s), t, _debugStyle);
        }

        public static string FmtT(float seconds)
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return string.Format("{0:00}:{1:00}", t / 60, t % 60);
        }
    }
}
