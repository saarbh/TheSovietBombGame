using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Terminal UI View for Room 4 Threat Volume &amp; Alarm Override Puzzle.
/// Controls CRT monitor text display, command input field, sticky note prompt,
/// alarm banner status, and doctrine help modal popup window.
/// Includes OnGUI fallback rendering if serialized UI references are unassigned.
/// Implements <see cref="IInteractable"/> so the player must interact (E) to open the view.
/// </summary>
public class ThreatVolumeTerminalUI : MonoBehaviour, IInteractable
{
    [Header("Puzzle Reference")]
    [SerializeField] private ThreatVolumePuzzle puzzle;

    [Header("Display Elements")]
    [SerializeField] private GameObject terminalUIContainer;
    [SerializeField] private TMP_Text monitorDisplayText;
    [SerializeField] private TMP_Text stickyNoteText;
    [SerializeField] private TMP_InputField commandInputField;
    [SerializeField] private TMP_Text alarmBannerText;

    [Header("Help Modal Window")]
    [SerializeField] private GameObject helpModalWindow;
    [SerializeField] private TMP_Text helpModalText;
    [SerializeField] private Button helpButton;
    [SerializeField] private Button closeHelpButton;
    [SerializeField] private Button exitTerminalButton;

    [Header("Settings")]
    [SerializeField] private int maxLogLines = 16;
    [SerializeField] private string interactPrompt = "[E] Access Terminal";

    private readonly List<string> logBuffer = new List<string>();
    private bool isOpen = false;
    private bool isHelpOpen;
    private string tempInput = string.Empty;
    private PlayerController focusedPlayer;

    public bool IsOpen => isOpen;

    #region IInteractable Implementation

    public void Interact(PlayerController player)
    {
        if (isOpen)
        {
            CloseTerminal();
        }
        else
        {
            OpenTerminal(player);
        }
    }

    public string GetPrompt()
    {
        return isOpen ? string.Empty : interactPrompt;
    }

    #endregion

    public void OpenTerminal(PlayerController player = null)
    {
        isOpen = true;
        focusedPlayer = player;

        if (focusedPlayer != null)
        {
            focusedPlayer.SetInputEnabled(false);
        }

        SetTerminalUIVisible(true);
        RefreshDisplay();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (commandInputField != null)
        {
            commandInputField.Select();
            commandInputField.ActivateInputField();
        }
    }

    public void CloseTerminal()
    {
        isOpen = false;

        if (isHelpOpen)
        {
            CloseHelpModal();
        }

        SetTerminalUIVisible(false);

        if (focusedPlayer != null)
        {
            focusedPlayer.SetInputEnabled(true);
            focusedPlayer = null;
        }
        else
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null && !player.IsInputEnabled)
            {
                player.SetInputEnabled(true);
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetTerminalUIVisible(bool visible)
    {
        if (terminalUIContainer != null)
        {
            terminalUIContainer.SetActive(visible);
        }
        else if (monitorDisplayText != null && monitorDisplayText.transform.parent != null)
        {
            var parentGo = monitorDisplayText.transform.parent.gameObject;
            if (parentGo != this.gameObject)
            {
                parentGo.SetActive(visible);
            }
        }
    }

    private void Awake()
    {
        if (puzzle == null)
        {
            puzzle = GetComponentInParent<ThreatVolumePuzzle>();
        }

        if (helpModalWindow != null)
        {
            helpModalWindow.SetActive(false);
        }

        isOpen = false;
        SetTerminalUIVisible(false);
    }

    private void OnEnable()
    {
        if (puzzle != null)
        {
            puzzle.OnStateChanged += HandleStateChanged;
            puzzle.OnOutputLineAdded += HandleOutputLineAdded;
            puzzle.OnMissileCountUpdated += HandleMissileCountUpdated;
            puzzle.OnConfirmed += HandleConfirmed;
            puzzle.OnPuzzleReset += HandlePuzzleReset;
        }

        if (commandInputField != null)
        {
            commandInputField.onSubmit.AddListener(OnInputSubmitted);
        }

        if (helpButton != null)
        {
            helpButton.onClick.AddListener(OpenHelpModal);
        }

        if (closeHelpButton != null)
        {
            closeHelpButton.onClick.AddListener(CloseHelpModal);
        }

        if (exitTerminalButton != null)
        {
            exitTerminalButton.onClick.AddListener(CloseTerminal);
        }

        if (!isOpen)
        {
            SetTerminalUIVisible(false);
        }

        UpdateStickyNote();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            CloseTerminal();
        }

        if (puzzle != null)
        {
            puzzle.OnStateChanged -= HandleStateChanged;
            puzzle.OnOutputLineAdded -= HandleOutputLineAdded;
            puzzle.OnMissileCountUpdated -= HandleMissileCountUpdated;
            puzzle.OnConfirmed -= HandleConfirmed;
            puzzle.OnPuzzleReset -= HandlePuzzleReset;
        }

        if (commandInputField != null)
        {
            commandInputField.onSubmit.RemoveListener(OnInputSubmitted);
        }

        if (helpButton != null)
        {
            helpButton.onClick.RemoveListener(OpenHelpModal);
        }

        if (closeHelpButton != null)
        {
            closeHelpButton.onClick.RemoveListener(CloseHelpModal);
        }

        if (exitTerminalButton != null)
        {
            exitTerminalButton.onClick.RemoveListener(CloseTerminal);
        }
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }

        if (IsEscapePressed())
        {
            CloseTerminal();
        }
    }

    private bool IsEscapePressed()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }

        try
        {
            return Input.GetKeyDown(KeyCode.Escape);
        }
        catch
        {
            return false;
        }
    }

    public void OnInputSubmitted(string text)
    {
        if (puzzle == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        puzzle.ProcessTerminalInput(text);

        if (commandInputField != null)
        {
            commandInputField.text = string.Empty;
            commandInputField.ActivateInputField();
        }
    }

    public void OpenHelpModal()
    {
        isHelpOpen = true;

        if (helpModalWindow != null)
        {
            helpModalWindow.SetActive(true);
        }

        if (helpModalText != null && puzzle != null && puzzle.ThreatConfig != null)
        {
            helpModalText.text = puzzle.ThreatConfig.DoctrineHelpText;
        }
    }

    public void CloseHelpModal()
    {
        isHelpOpen = false;

        if (helpModalWindow != null)
        {
            helpModalWindow.SetActive(false);
        }
    }

    private void HandleStateChanged(ThreatTerminalState state)
    {
        UpdateAlarmBanner(state);
        RefreshDisplay();
    }

    private void HandleOutputLineAdded(string line)
    {
        logBuffer.Add(line);

        while (logBuffer.Count > maxLogLines)
        {
            logBuffer.RemoveAt(0);
        }

        RefreshDisplay();
    }

    private void HandleMissileCountUpdated(int count)
    {
        RefreshDisplay();
    }

    private void HandleConfirmed(PuzzleCard card)
    {
        HandleOutputLineAdded($"[SYSTEM SEALED] Card filed: {card.ToPrintedLine()}");
    }

    private void HandlePuzzleReset()
    {
        logBuffer.Clear();
        logBuffer.Add("[KERNEL ERROR 0x80004005] OKO Subsystem unresponsive.");
        logBuffer.Add("SYSTEM FROZEN. Consult sticky note for reboot instructions.");
        RefreshDisplay();
    }

    private void UpdateStickyNote()
    {
        if (stickyNoteText != null && puzzle != null && puzzle.ThreatConfig != null)
        {
            stickyNoteText.text = puzzle.ThreatConfig.StickyNoteText;
        }
    }

    private void UpdateAlarmBanner(ThreatTerminalState state)
    {
        if (alarmBannerText == null)
        {
            return;
        }

        switch (state)
        {
            case ThreatTerminalState.Off:
                alarmBannerText.text = "STATUS: TERMINAL FROZEN";
                break;
            case ThreatTerminalState.Rebooting:
                alarmBannerText.text = "STATUS: REBOOTING...";
                break;
            case ThreatTerminalState.TelemetryLive:
                alarmBannerText.text = $"STATUS: SATELLITE TELEMETRY LIVE ({puzzle.CurrentMissileCount} MISSILE DETECTED)";
                break;
            case ThreatTerminalState.AlarmLocked:
                alarmBannerText.text = $"[!!! ALARM KLAXON ACTIVE !!!] {puzzle.CurrentMissileCount} MISSILES DETECTED - ENTER OVERRIDE";
                break;
            case ThreatTerminalState.Overridden:
                alarmBannerText.text = "STATUS: OVERRIDE APPLIED - ALARM SILENCED";
                break;
        }
    }

    private void RefreshDisplay()
    {
        if (monitorDisplayText == null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("==================================================");
        sb.AppendLine("   OKO STRATEGIC EARLY WARNING SYSTEM - TERMINAL  ");
        sb.AppendLine("==================================================");

        foreach (var line in logBuffer)
        {
            sb.AppendLine(line);
        }

        monitorDisplayText.text = sb.ToString();
    }

    private void OnGUI()
    {
        if (!isOpen)
        {
            return;
        }

        if (monitorDisplayText != null && monitorDisplayText.gameObject.activeInHierarchy)
        {
            return;
        }

        bool enterPressed = false;
        if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
        {
            enterPressed = true;
            Event.current.Use();
        }

        var windowWidth = 540f;
        var windowHeight = 480f;
        var rect = new Rect((Screen.width - windowWidth) / 2f, (Screen.height - windowHeight) / 2f, windowWidth, windowHeight);

        GUI.Box(rect, "CRT TERMINAL - ROOM 4 THREAT VOLUME");
        GUILayout.BeginArea(new Rect(rect.x + 15, rect.y + 25, rect.width - 30, rect.height - 35));

        var stateStr = puzzle != null ? puzzle.State.ToString() : "OFF";
        var countStr = puzzle != null ? puzzle.CurrentMissileCount.ToString() : "0";
        GUILayout.Label($"<b>Terminal State:</b> {stateStr} | <b>Missiles:</b> {countStr}");

        var stickyHint = puzzle != null && puzzle.ThreatConfig != null ? puzzle.ThreatConfig.StickyNoteText : "Type /help for system commands";
        GUILayout.Box($"[STICKY NOTE]: {stickyHint}", GUILayout.Height(30));

        var sb = new StringBuilder();
        foreach (var line in logBuffer)
        {
            sb.AppendLine(line);
        }

        GUILayout.TextArea(sb.ToString(), GUILayout.Height(180));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Cmd:", GUILayout.Width(40));
        tempInput = GUILayout.TextField(tempInput);

        if (GUILayout.Button("SEND", GUILayout.Width(60)) || enterPressed)
        {
            if (!string.IsNullOrWhiteSpace(tempInput))
            {
                puzzle?.ProcessTerminalInput(tempInput);
                tempInput = string.Empty;
            }
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("HELP / DOCTRINE [?]", GUILayout.Height(30)))
        {
            isHelpOpen = !isHelpOpen;
        }

        if (puzzle != null && puzzle.CanConfirm && GUILayout.Button("CONFIRM OVERRIDE", GUILayout.Height(30)))
        {
            puzzle.Confirm();
        }

        if (puzzle != null && puzzle.CanReset && GUILayout.Button("RESET TERMINAL", GUILayout.Height(30)))
        {
            puzzle.ResetAttempt();
        }

        if (GUILayout.Button("EXIT TERMINAL [ESC]", GUILayout.Height(30)))
        {
            CloseTerminal();
        }

        GUILayout.EndHorizontal();

        if (isHelpOpen && puzzle != null && puzzle.ThreatConfig != null)
        {
            GUILayout.Space(5);
            GUILayout.Box($"<b>DOCTRINE HELP:</b>\n{puzzle.ThreatConfig.DoctrineHelpText}", GUILayout.MinHeight(120));
        }

        GUILayout.EndArea();
    }
}

