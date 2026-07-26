// ============================================================================
//  THE HOT WAR — HW_Interactables.cs
//  Components you attach to YOUR scene objects (make each a prefab):
//    HW_Door            — time-locked door (needs its code card too)
//    HW_CodeCard        — pickup on the control-room desk
//    HW_Note            — anything readable (maintenance log, printouts, window)
//    HW_Phone           — opens the report call
//    HW_OverrideTerminal— single-use, advances all sealed door timers
//    HW_ControlRoomZone — trigger volume; leaving it forfeits the quiet ending
//  Plus HotInput: works with BOTH legacy Input Manager and the new Input System.
// ============================================================================

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TheHotWar
{
    // ------------------------------------------------------------------------
    public abstract class HW_Interactable : MonoBehaviour
    {
        public abstract string Prompt { get; }
        public virtual void OnPress() { }
        public virtual void OnHold(float dt) { }

        protected static HW_Game GM { get { return HW_Game.I; } }

        protected static void Tint(Renderer r, Color c)
        {
            if (r == null) return;
            Material m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
    }

    // ------------------------------------------------------------------------
    [AddComponentMenu("The Hot War/Door (timed + card)")]
    public class HW_Door : HW_Interactable
    {
        static readonly List<HW_Door> All = new List<HW_Door>();

        [Tooltip("0..3 — which room this is (also which code card it needs).")]
        public int roomIndex;
        public string roomName = "ROOM";

        [Tooltip("REAL unlock time, seconds since game start.")]
        public float realUnlockElapsed = 60f;
        [Tooltip("Time PAINTED on the plate (what the prompt quotes). Rooms 2 & 4 lie.")]
        public float shownUnlockElapsed = 60f;
        [Tooltip("Shown in the prompt once the card is owned.")]
        public string code = "0000";

        [Header("Opening")]
        [Tooltip("Local movement when opening (default: slide down into the floor).")]
        public Vector3 openOffset = new Vector3(0f, -2.55f, 0f);
        public float openSeconds = 0.7f;
        [Tooltip("Hook your own animation/sound here; fires once when the door opens.")]
        public UnityEvent onOpened;

        [Header("Status lamps (optional)")]
        public Renderer[] lampRenderers;

        static readonly Color LampRed = new Color(0.9f, 0.15f, 0.12f);
        static readonly Color LampYellow = new Color(0.95f, 0.8f, 0.2f);
        static readonly Color LampGreen = new Color(0.25f, 0.9f, 0.35f);

        bool _open;
        float _slide;
        Vector3 _closedPos;
        int _lampState = -1;

        public bool IsOpen { get { return _open; } }
        bool TimeReached { get { return GM != null && GM.elapsed >= realUnlockElapsed; } }
        bool HasCard { get { return GM != null && GM.cardOwned[Mathf.Clamp(roomIndex, 0, 3)]; } }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            _closedPos = transform.localPosition;
            SetLampState(0);
        }

        void Update()
        {
            if (!_open) SetLampState(TimeReached ? 1 : 0);
            float target = _open ? 1f : 0f;
            _slide = Mathf.MoveTowards(_slide, target, Time.deltaTime / Mathf.Max(0.05f, openSeconds));
            transform.localPosition = _closedPos + openOffset * _slide;
        }

        public override string Prompt
        {
            get
            {
                if (_open) return "";
                float total = GM != null ? GM.totalTime : 420f;
                if (!TimeReached)
                    return roomName + " — SEALED. Plate says it opens at T-" +
                           HW_UI.FmtT(Mathf.Max(0f, total - shownUnlockElapsed));
                if (!HasCard)
                    return roomName + " — keypad active. You need this room's CODE CARD (control room).";
                return "[E] " + roomName + " — enter code " + code;
            }
        }

        public override void OnPress()
        {
            if (_open) return;
            if (!TimeReached)
            {
                if (GM != null) GM.ShowNote("The lock is mechanical. It will not open one second early.", 3f);
                return;
            }
            if (!HasCard)
            {
                if (GM != null) GM.ShowNote("The keypad wants a four-digit code. The cards are back in the control room.", 3f);
                return;
            }
            _open = true;
            SetLampState(2);
            if (onOpened != null) onOpened.Invoke();
            if (GM != null) GM.ShowNote(roomName + " unlocked.", 2.5f);
        }

        void SetLampState(int st)
        {
            if (st == _lampState) return;
            _lampState = st;
            Color c = st == 0 ? LampRed : st == 1 ? LampYellow : LampGreen;
            if (lampRenderers != null)
                foreach (Renderer r in lampRenderers) Tint(r, c);
        }

        public static void ApplyOverrideToAll(float seconds)
        {
            foreach (HW_Door d in All)
                if (d != null && !d._open)
                    d.realUnlockElapsed = Mathf.Max(0f, d.realUnlockElapsed - seconds);
        }
    }

    // ------------------------------------------------------------------------
    [AddComponentMenu("The Hot War/Code Card")]
    public class HW_CodeCard : HW_Interactable
    {
        public int roomIndex;
        public string roomName = "ROOM";
        public string code = "0000";

        public override string Prompt { get { return "[E] Take code card — " + roomName; } }

        public override void OnPress()
        {
            if (GM != null) GM.TakeCard(roomIndex, roomName, code);
            gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------------------
    [AddComponentMenu("The Hot War/Note (readable)")]
    public class HW_Note : HW_Interactable
    {
        public string promptText = "[E] Read";
        [TextArea] public string noteText = "";
        public float noteSeconds = 7f;

        public override string Prompt { get { return promptText; } }
        public override void OnPress() { if (GM != null) GM.ShowNote(noteText, noteSeconds); }
    }

    // ------------------------------------------------------------------------
    [AddComponentMenu("The Hot War/Phone (duty line)")]
    public class HW_Phone : HW_Interactable
    {
        public override string Prompt { get { return "[E] Pick up the duty line"; } }
        public override void OnPress() { if (GM != null) GM.OpenPhone(); }
    }

    // ------------------------------------------------------------------------
    [AddComponentMenu("The Hot War/Override Terminal")]
    public class HW_OverrideTerminal : HW_Interactable
    {
        public float holdSeconds = 3f;
        public Renderer lampRenderer;

        float _progress;
        bool _used;

        void Start() { Tint(lampRenderer, new Color(0.95f, 0.8f, 0.2f)); }

        public override string Prompt
        {
            get
            {
                if (_used || (GM != null && GM.overrideUsed)) return "MECHANICAL OVERRIDE — SPENT.";
                if (_progress > 0f)
                    return "CRANKING OVERRIDE...  " + Mathf.RoundToInt(_progress / holdSeconds * 100f) + "%   (keep holding E)";
                return "[HOLD E] Mechanical override — advance every sealed door timer (single use)";
            }
        }

        public override void OnHold(float dt)
        {
            if (_used || (GM != null && GM.overrideUsed)) return;
            _progress += dt;
            if (_progress >= holdSeconds)
            {
                _used = true;
                Tint(lampRenderer, new Color(0.25f, 0.9f, 0.35f));
                if (GM != null) GM.UseOverride();
            }
        }
    }

    // ------------------------------------------------------------------------
    //  Put this on an empty GameObject with a BoxCollider (Is Trigger = ON)
    //  covering the whole control room. Stepping out = quiet ending forfeited.
    // ------------------------------------------------------------------------
    [AddComponentMenu("The Hot War/Control Room Zone (trigger)")]
    public class HW_ControlRoomZone : MonoBehaviour
    {
        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (other == null) return;
            bool isPlayer = other.GetComponentInParent<HW_Interactor>() != null
                         || other.GetComponentInParent<HW_Player>() != null;
            if (isPlayer && HW_Game.I != null) HW_Game.I.MarkLeftControlRoom();
        }
    }

    // ========================================================================
    //  INPUT — works with legacy Input Manager AND the new Input System
    // ========================================================================
    public static class HotInput
    {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        public static float LookX { get { return Input.GetAxis("Mouse X"); } }
        public static float LookY { get { return Input.GetAxis("Mouse Y"); } }
        public static float MoveX { get { return Input.GetAxisRaw("Horizontal"); } }
        public static float MoveY { get { return Input.GetAxisRaw("Vertical"); } }
        public static bool SprintHeld { get { return Input.GetKey(KeyCode.LeftShift); } }
        public static bool InteractDown { get { return Input.GetKeyDown(KeyCode.E); } }
        public static bool InteractHeld { get { return Input.GetKey(KeyCode.E); } }
        public static bool EscapeDown { get { return Input.GetKeyDown(KeyCode.Escape); } }
        public static bool RestartDown { get { return Input.GetKeyDown(KeyCode.R); } }
        public static bool MenuDown { get { return Input.GetKeyDown(KeyCode.M); } }
        public static bool ClickDown { get { return Input.GetMouseButtonDown(0); } }
        public static bool Num1Down { get { return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1); } }
        public static bool Num2Down { get { return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2); } }
        public static bool Num3Down { get { return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3); } }

        public static bool DigitDown(out int d)
        {
            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i)) ||
                    Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + i)))
                { d = i; return true; }
            }
            d = -1; return false;
        }
#else
        static Keyboard K { get { return Keyboard.current; } }
        static Mouse M { get { return Mouse.current; } }

        public static float LookX { get { return M != null ? M.delta.ReadValue().x * 0.1f : 0f; } }
        public static float LookY { get { return M != null ? M.delta.ReadValue().y * 0.1f : 0f; } }
        public static float MoveX
        {
            get
            {
                if (K == null) return 0f;
                return (K.dKey.isPressed ? 1f : 0f) - (K.aKey.isPressed ? 1f : 0f);
            }
        }
        public static float MoveY
        {
            get
            {
                if (K == null) return 0f;
                return (K.wKey.isPressed ? 1f : 0f) - (K.sKey.isPressed ? 1f : 0f);
            }
        }
        public static bool SprintHeld { get { return K != null && K.leftShiftKey.isPressed; } }
        public static bool InteractDown { get { return K != null && K.eKey.wasPressedThisFrame; } }
        public static bool InteractHeld { get { return K != null && K.eKey.isPressed; } }
        public static bool EscapeDown { get { return K != null && K.escapeKey.wasPressedThisFrame; } }
        public static bool RestartDown { get { return K != null && K.rKey.wasPressedThisFrame; } }
        public static bool MenuDown { get { return K != null && K.mKey.wasPressedThisFrame; } }
        public static bool ClickDown { get { return M != null && M.leftButton.wasPressedThisFrame; } }
        public static bool Num1Down { get { return K != null && (K.digit1Key.wasPressedThisFrame || K.numpad1Key.wasPressedThisFrame); } }
        public static bool Num2Down { get { return K != null && (K.digit2Key.wasPressedThisFrame || K.numpad2Key.wasPressedThisFrame); } }
        public static bool Num3Down { get { return K != null && (K.digit3Key.wasPressedThisFrame || K.numpad3Key.wasPressedThisFrame); } }

        public static bool DigitDown(out int d)
        {
            d = -1;
            if (K == null) return false;
            UnityEngine.InputSystem.Controls.KeyControl[] digits =
            {
                K.digit0Key, K.digit1Key, K.digit2Key, K.digit3Key, K.digit4Key,
                K.digit5Key, K.digit6Key, K.digit7Key, K.digit8Key, K.digit9Key
            };
            UnityEngine.InputSystem.Controls.KeyControl[] pads =
            {
                K.numpad0Key, K.numpad1Key, K.numpad2Key, K.numpad3Key, K.numpad4Key,
                K.numpad5Key, K.numpad6Key, K.numpad7Key, K.numpad8Key, K.numpad9Key
            };
            for (int i = 0; i <= 9; i++)
                if (digits[i].wasPressedThisFrame || pads[i].wasPressedThisFrame) { d = i; return true; }
            return false;
        }
#endif
    }
}
