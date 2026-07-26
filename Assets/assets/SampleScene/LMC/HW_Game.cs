// ============================================================================
//  THE HOT WAR — HW_Game.cs
//  The game loop: countdown, win/lose endings, phone call, progress tracking.
//  ONE instance per game scene (empty GameObject -> add HW_Game).
//  Everything else (doors, puzzles, phone, cards) talks to HW_Game.I.
// ============================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheHotWar
{
    [AddComponentMenu("The Hot War/Game (manager)")]
    public class HW_Game : MonoBehaviour
    {
        public static HW_Game I;

        public enum GameState { Playing, Won, Lost }

        [Header("Timing")]
        [Tooltip("Total countdown in seconds. Design doc: 420 (7:00).")]
        public float totalTime = 420f;
        [Tooltip("Seconds shaved off every still-sealed door by the override terminal.")]
        public float overrideReduction = 45f;

        [Header("Scene flow")]
        [Tooltip("Scene name of the main menu. Leave empty to hide the menu option on end screens.")]
        public string mainMenuScene = "MainMenu";

        [Header("Runtime state (read-only)")]
        public GameState state = GameState.Playing;
        public float elapsed;
        public bool leftControlRoom;
        public bool overrideUsed;
        public bool phoneOpen;
        public bool[] cardOwned = new bool[4];
        public bool[] roomSolved = new bool[4];
        [System.NonSerialized] public HW_PuzzleBase focusedPuzzle;

        // ---- UI plumbing (HW_UI reads these) --------------------------------
        [System.NonSerialized] public string endTitle = "", endBody = "";
        [System.NonSerialized] public string note = "";
        [System.NonSerialized] public float noteTimer;
        [System.NonSerialized] public string prompt = "";

        public float Remaining { get { return Mathf.Max(0f, totalTime - elapsed); } }
        public bool InputLocked { get { return state != GameState.Playing || phoneOpen || focusedPuzzle != null; } }
        public bool AllSolved { get { return SolvedCount == 4; } }

        public int SolvedCount { get { int n = 0; for (int i = 0; i < 4; i++) if (roomSolved[i]) n++; return n; } }
        public int CardCount { get { int n = 0; for (int i = 0; i < 4; i++) if (cardOwned[i]) n++; return n; } }

        void Awake() { I = this; }

        void Start()
        {
            ShowNote("ALERT: possible launch detected.\nVerify all four systems and call it in — or trust the silence and touch nothing.", 9f);
        }

        void Update()
        {
            if (noteTimer > 0f)
            {
                noteTimer -= Time.deltaTime;
                if (noteTimer <= 0f) note = "";
            }

            if (state == GameState.Playing)
            {
                elapsed += Time.deltaTime;

                if (phoneOpen) HandlePhone();

                if (elapsed >= totalTime)
                {
                    phoneOpen = false;
                    if (focusedPuzzle != null) { focusedPuzzle = null; }
                    if (!leftControlRoom)
                        Win("MORNING CAME",
                            "Seven minutes. You never left the chair, never lifted the phone.\n" +
                            "The alert dies on its own — a sensor ghost, sunlight on high clouds.\n" +
                            "Nobody will ever know how close it was. That was the job.");
                    else
                        Lose("THE PROTOCOL DECIDES",
                            "You left your post chasing certainty, and the clock beat you to it.\n" +
                            "An abandoned station defaults to the worst assumption.\n" +
                            "Silence reads as attack. The counter-strike launches itself.");
                }
            }
            else
            {
                if (HotInput.RestartDown)
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                if (HotInput.MenuDown && !string.IsNullOrEmpty(mainMenuScene))
                    SceneManager.LoadScene(mainMenuScene);
            }
        }

        void HandlePhone()
        {
            if (HotInput.EscapeDown) { phoneOpen = false; return; }

            if (AllSolved)
            {
                if (HotInput.Num1Down)
                {
                    phoneOpen = false;
                    Win("FALSE ALARM REPORTED",
                        "Four systems, four confirmations: sunlight on clouds, not missiles.\n" +
                        "You call it in with " + HW_UI.FmtT(Remaining) + " to spare.\n" +
                        "History never learns your name. That's what winning looks like tonight.");
                }
                else if (HotInput.Num2Down)
                {
                    phoneOpen = false;
                    Lose("YOU REPORTED A STRIKE",
                        "Every system told you it was a false alarm.\n" +
                        "You reported missiles anyway. The response is automatic —\n" +
                        "and then, so is theirs.");
                }
                else if (HotInput.Num3Down) phoneOpen = false;
            }
            else
            {
                if (HotInput.Num1Down)
                {
                    phoneOpen = false;
                    Lose("STRIKE REPORTED ON PARTIAL DATA",
                        "With systems unverified, the duty script permits only one sentence:\n" +
                        "\"Missile attack in progress.\"\n" +
                        "It was a flock of sunlit clouds.");
                }
                else if (HotInput.Num2Down) phoneOpen = false; // hang up
            }
        }

        // ---- public API used by everything else -----------------------------
        public void OpenPhone() { if (state == GameState.Playing && focusedPuzzle == null) phoneOpen = true; }

        public void Focus(HW_PuzzleBase p) { if (state == GameState.Playing && !phoneOpen) focusedPuzzle = p; }
        public void Blur() { focusedPuzzle = null; }

        public void MarkLeftControlRoom()
        {
            if (leftControlRoom || state != GameState.Playing) return;
            leftControlRoom = true;
        }

        public void TakeCard(int i, string roomName, string code)
        {
            if (i < 0 || i > 3) return;
            cardOwned[i] = true;
            ShowNote("CODE CARD TAKEN — " + roomName + ": " + code, 4f);
        }

        public void SolveRoom(int i, string flavour)
        {
            if (i < 0 || i > 3 || roomSolved[i]) return;
            roomSolved[i] = true;
            string extra = AllSolved ? "\nALL SYSTEMS VERIFIED: FALSE ALARM. Get back to the phone." : "";
            ShowNote(flavour + extra, 6.5f);
        }

        public void UseOverride()
        {
            if (overrideUsed) return;
            overrideUsed = true;
            HW_Door.ApplyOverrideToAll(overrideReduction);
            ShowNote("MECHANICAL OVERRIDE ENGAGED — every sealed door timer advanced by " +
                     Mathf.RoundToInt(overrideReduction) + "s.", 5f);
        }

        public void ShowNote(string t, float secs) { note = t; noteTimer = secs; }
        public void SetPrompt(string p) { prompt = p; }

        void Win(string t, string b) { state = GameState.Won; EndCommon(t, b); }
        void Lose(string t, string b) { state = GameState.Lost; EndCommon(t, b); }

        void EndCommon(string t, string b)
        {
            endTitle = t;
            endBody = b;
            note = "";
            prompt = "";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
