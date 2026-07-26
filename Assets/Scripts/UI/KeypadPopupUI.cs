using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 2D UI modal popup for entering passcodes on a targeted <see cref="DoorLockController"/>.
/// Freezes player input while open and evaluates passcodes on submit.
/// Features OnGUI fallback rendering if serialized UI references are missing in the scene.
/// </summary>
public class KeypadPopupUI : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject keypadPanel;

    [Header("Display Elements")]
    [SerializeField] private TextMeshProUGUI codeDisplay;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Settings")]
    [SerializeField] private int maxCodeLength = 6;
    [SerializeField] private string defaultStatusPrompt = "ENTER PASSCODE";
    [SerializeField] private string successStatusMessage = "ACCESS GRANTED";
    [SerializeField] private string errorStatusMessage = "INVALID CODE";

    private DoorLockController currentLock;
    private PlayerController focusedPlayer;
    private readonly StringBuilder enteredBuffer = new StringBuilder();
    private bool isOpen;
    private string statusMessage = "";

    public bool IsOpen => isOpen;

    private void OnEnable()
    {
        DoorLockController.OnAnyLockInteracted += HandleLockInteracted;
    }

    private void OnDisable()
    {
        DoorLockController.OnAnyLockInteracted -= HandleLockInteracted;
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }

        HandleKeyboardInput();
    }

    private void HandleLockInteracted(DoorLockController lockController, PlayerController player)
    {
        OpenKeypad(lockController, player);
    }

    public void OpenKeypad(DoorLockController targetLock, PlayerController player)
    {
        if (targetLock == null || targetLock.IsUnlocked)
        {
            return;
        }

        currentLock = targetLock;
        focusedPlayer = player;
        enteredBuffer.Clear();
        isOpen = true;

        if (keypadPanel != null)
        {
            keypadPanel.SetActive(true);
        }

        UpdateDisplay();

        SetStatus(defaultStatusPrompt);

        if (focusedPlayer != null)
        {
            focusedPlayer.SetInputEnabled(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseKeypad()
    {
        isOpen = false;
        enteredBuffer.Clear();

        if (keypadPanel != null)
        {
            keypadPanel.SetActive(false);
        }

        if (focusedPlayer != null)
        {
            focusedPlayer.SetInputEnabled(true);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentLock = null;
        focusedPlayer = null;
    }

    public void OnNumPadPressed(int digit)
    {
        if (!isOpen || enteredBuffer.Length >= maxCodeLength)
        {
            return;
        }

        enteredBuffer.Append(digit.ToString());
        UpdateDisplay();
    }

    public void OnBackspacePressed()
    {
        if (!isOpen || enteredBuffer.Length == 0)
        {
            return;
        }

        enteredBuffer.Remove(enteredBuffer.Length - 1, 1);
        UpdateDisplay();
    }

    public void OnClearPressed()
    {
        if (!isOpen)
        {
            return;
        }

        enteredBuffer.Clear();
        UpdateDisplay();
    }

    public void SubmitCode()
    {
        if (!isOpen || currentLock == null)
        {
            return;
        }

        var code = enteredBuffer.ToString();
        var isValid = currentLock.ValidateCode(code);

        if (isValid)
        {
            SetStatus(successStatusMessage);
            CloseKeypad();
        }
        else
        {
            SetStatus(errorStatusMessage);
            enteredBuffer.Clear();
            UpdateDisplay();
        }
    }

    private void HandleKeyboardInput()
    {
        var kb = Keyboard.current;
        if (kb == null)
        {
            return;
        }

        if (kb.escapeKey.wasPressedThisFrame)
        {
            CloseKeypad();
            return;
        }

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            SubmitCode();
            return;
        }

        if (kb.backspaceKey.wasPressedThisFrame)
        {
            OnBackspacePressed();
            return;
        }

        for (var d = 0; d <= 9; d++)
        {
            // Key.Digit0 is 50 and sits AFTER Digit1..Digit9 (41..49), so `Digit0 + d`
            // walks straight into the modifier keys - 1 became LeftShift, 2 RightShift,
            // and so on. That made every passcode containing a non-zero digit
            // impossible to type on the number row. Numpad0..Numpad9 (84..93) really
            // are contiguous and in order, which is why the numpad always worked.
            var alphaKey = d == 0 ? Key.Digit0 : Key.Digit1 + (d - 1);
            var numpadKey = Key.Numpad0 + d;

            if (kb[alphaKey].wasPressedThisFrame || kb[numpadKey].wasPressedThisFrame)
            {
                OnNumPadPressed(d);
                break;
            }
        }
    }

    private void UpdateDisplay()
    {
        if (codeDisplay != null)
        {
            codeDisplay.text = enteredBuffer.Length > 0 ? enteredBuffer.ToString() : "------";
        }
    }

    private void SetStatus(string message)
    {
        statusMessage = message;

        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void OnGUI()
    {
        if (!isOpen || keypadPanel != null)
        {
            return;
        }

        var windowWidth = 340f;
        var windowHeight = 420f;
        var rect = new Rect((Screen.width - windowWidth) / 2f, (Screen.height - windowHeight) / 2f, windowWidth, windowHeight);

        GUI.Box(rect, $"KEYPAD - {currentLock?.Config?.RoomId ?? "DOOR LOCK"}");
        GUILayout.BeginArea(new Rect(rect.x + 20, rect.y + 30, rect.width - 40, rect.height - 45));

        GUILayout.Label($"<b>Status:</b> {statusMessage}");
        GUILayout.Space(5);

        var displayCode = enteredBuffer.Length > 0 ? enteredBuffer.ToString() : "------";
        GUILayout.Box($"<size=24><b>{displayCode}</b></size>", GUILayout.Height(40));
        GUILayout.Space(10);

        // Keypad Grid 1-9
        for (var row = 0; row < 3; row++)
        {
            GUILayout.BeginHorizontal();
            for (var col = 1; col <= 3; col++)
            {
                var num = (row * 3) + col;
                if (GUILayout.Button(num.ToString(), GUILayout.Height(36)))
                {
                    OnNumPadPressed(num);
                }
            }
            GUILayout.EndHorizontal();
        }

        // Bottom row: Clear, 0, Backspace
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("CLR", GUILayout.Height(36)))
        {
            OnClearPressed();
        }
        if (GUILayout.Button("0", GUILayout.Height(36)))
        {
            OnNumPadPressed(0);
        }
        if (GUILayout.Button("DEL", GUILayout.Height(36)))
        {
            OnBackspacePressed();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SUBMIT", GUILayout.Height(36)))
        {
            SubmitCode();
        }
        if (GUILayout.Button("CANCEL (ESC)", GUILayout.Height(36)))
        {
            CloseKeypad();
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
