using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Terminal state enum for the Threat Volume & Alarm Override Room (Room 4).
/// </summary>
public enum ThreatTerminalState
{
    Off,
    Rebooting,
    TelemetryLive,
    AlarmLocked,
    Overridden
}

/// <summary>
/// Room 4 Threat Volume & Alarm Override Puzzle.
/// The computer starts frozen on a kernel error. The player follows the CRT sticky note hint
/// or types /help to learn the reboot sequence (init 0 -> check-mem -> start-oko).
/// Booting brings up satellite telemetry (1 missile -> spike to 5 missiles) and triggers the alarm klaxon.
/// Consulted doctrine indicates &lt; 10 ICBMs is a solar reflection anomaly requiring code OVERRIDE-05.
/// Entering OVERRIDE-05 silences the alarm and marks the room resolved.
/// </summary>
public class ThreatVolumePuzzle : BasePuzzle<string>, IConfirmablePuzzle
{
    [Header("Threat Volume Setup")]
    [SerializeField] private ThreatVolumeRoomConfig threatConfig;

    [Header("Audio & Visual Feedback")]
    [SerializeField] private AudioSource alarmAudioSource;
    [SerializeField] private GameObject alarmLight;

    private ThreatTerminalState currentStateEnum = ThreatTerminalState.Off;
    private int currentRebootStep = 0;
    private int currentMissileCount = 0;
    private string enteredOverrideCode = string.Empty;
    private CancellationTokenSource telemetryCts;

    public ThreatTerminalState State => currentStateEnum;
    public int CurrentRebootStep => currentRebootStep;
    public int CurrentMissileCount => currentMissileCount;
    public string EnteredOverrideCode => enteredOverrideCode;
    public ThreatVolumeRoomConfig ThreatConfig => threatConfig;

    #region IConfirmablePuzzle Implementation

    /// <summary>True once CONFIRM has been pulled or filed; the answer can no longer be changed.</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>
    /// True when the player has entered an override code or completed the override sequence.
    /// </summary>
    public bool CanConfirm => !IsConfirmed && (currentStateEnum == ThreatTerminalState.AlarmLocked || currentStateEnum == ThreatTerminalState.Overridden) && !string.IsNullOrEmpty(enteredOverrideCode);

    /// <summary>Why CONFIRM is refused, for the lever's prompt. Null when it is available.</summary>
    public string ConfirmBlockedReason
    {
        get
        {
            if (IsConfirmed)
            {
                return "Result already filed";
            }

            if (currentStateEnum == ThreatTerminalState.Off || currentStateEnum == ThreatTerminalState.Rebooting)
            {
                return "Terminal reboot required";
            }

            if (currentStateEnum == ThreatTerminalState.TelemetryLive)
            {
                return "Telemetry sequence in progress";
            }

            if (string.IsNullOrEmpty(enteredOverrideCode))
            {
                return "Override code required";
            }

            return null;
        }
    }

    /// <summary>The panel seals on CONFIRM; until then a retry costs nothing.</summary>
    public bool CanReset => !IsConfirmed;

    /// <summary><see cref="IConfirmablePuzzle"/> implementation for resetting the attempt.</summary>
    public void ResetAttempt()
    {
        ResetPuzzleState();
    }

    /// <summary>
    /// Locks in the player's attempt and files the card into inventory.
    /// </summary>
    public PuzzleCard Confirm()
    {
        if (IsConfirmed)
        {
            return FiledCard ?? BuildCard(IsSolved);
        }

        if (!CanConfirm)
        {
            Debug.LogWarning("[ThreatVolume] CONFIRM refused - puzzle not ready or override code missing.", this);
            return BuildCard(false);
        }

        IsConfirmed = true;

        currentState = enteredOverrideCode;
        CheckSolve();

        SetAlarmActive(false);

        MarkResolved(IsSolved);

        var card = FiledCard.Value;
        Debug.Log($"[ThreatVolume] CONFIRM filed - code \"{enteredOverrideCode}\", correct={card.WasCorrect}, card \"{card.ToPrintedLine()}\". Room sealed.", this);

        OnConfirmed?.Invoke(card);
        return card;
    }

    #endregion

    #region Events

    public event Action<ThreatTerminalState> OnStateChanged;
    public event Action<string> OnOutputLineAdded;
    public event Action<int> OnMissileCountUpdated;
    public event Action<PuzzleCard> OnConfirmed;
    public event Action OnPuzzleReset;

    #endregion

    protected override void Awake()
    {
        base.Awake();

        if (threatConfig == null && puzzleConfig is ThreatVolumeRoomConfig config)
        {
            threatConfig = config;
        }
    }

    public override void InitializePuzzle()
    {
        base.InitializePuzzle();

        var targetCode = threatConfig != null ? threatConfig.CorrectOverrideCode : "OVERRIDE-05";
        targetState = NormalizeCode(targetCode);
        currentState = string.Empty;

        ResetPuzzleState();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        CancelTelemetryTask();
    }

    public override void ResetPuzzle()
    {
        base.ResetPuzzle();
        IsConfirmed = false;
        ResetPuzzleState();
    }

    public void ResetPuzzleState()
    {
        if (IsConfirmed)
        {
            return;
        }

        CancelTelemetryTask();
        currentStateEnum = ThreatTerminalState.Off;
        currentRebootStep = 0;
        currentMissileCount = 0;
        enteredOverrideCode = string.Empty;
        currentState = string.Empty;

        SetAlarmActive(false);

        OnStateChanged?.Invoke(currentStateEnum);
        OnPuzzleReset?.Invoke();
    }

    /// <summary>
    /// Processes CLI commands typed into the terminal or input field.
    /// Supports /help, reboot commands (init 0, check-mem, start-oko), and override commands.
    /// </summary>
    public void ProcessTerminalInput(string input)
    {
        if (IsConfirmed)
        {
            OnOutputLineAdded?.Invoke("[SYSTEM LOCKED] Room attempt already filed and sealed.");
            return;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var trimmedInput = input.Trim();
        OnOutputLineAdded?.Invoke($"> {trimmedInput}");

        var lowerInput = trimmedInput.ToLowerInvariant();

        // Help command
        if (lowerInput == "help" || lowerInput == "/help" || lowerInput == "man")
        {
            PrintHelpInformation();
            return;
        }

        // Handle reboot commands when terminal is Off or Rebooting
        if (currentStateEnum == ThreatTerminalState.Off || currentStateEnum == ThreatTerminalState.Rebooting)
        {
            ProcessRebootCommand(trimmedInput);
            return;
        }

        // Handle override commands when terminal is TelemetryLive, AlarmLocked, or Overridden
        if (lowerInput.StartsWith("override") || lowerInput == "05" || lowerInput == "99" || lowerInput == "anomaly" || lowerInput == "threat")
        {
            ProcessOverrideCommand(trimmedInput);
            return;
        }

        OnOutputLineAdded?.Invoke($"[ERROR] Unrecognized command '{trimmedInput}'. Type /help for system commands.");
    }

    private void PrintHelpInformation()
    {
        OnOutputLineAdded?.Invoke("=== OKO EARLY WARNING SYSTEM HELP ===");
        OnOutputLineAdded?.Invoke("System Commands:");
        OnOutputLineAdded?.Invoke("  /help                   - Display this command manual");
        OnOutputLineAdded?.Invoke("Reboot Sequence:");

        var seq = threatConfig != null ? threatConfig.RebootSequenceCommands : new[] { "init 0", "check-mem", "start-oko" };
        if (seq.Length >= 3)
        {
            OnOutputLineAdded?.Invoke($"  1. {seq[0]}               - Purge kernel error & clear memory");
            OnOutputLineAdded?.Invoke($"  2. {seq[1]}           - Verify memory integrity");
            OnOutputLineAdded?.Invoke($"  3. {seq[2]}           - Launch OKO satellite telemetry feed");
        }

        OnOutputLineAdded?.Invoke("Override Commands:");
        OnOutputLineAdded?.Invoke("  override [CODE]          - Apply satellite alarm override (e.g. override 05)");
        OnOutputLineAdded?.Invoke("=====================================");
    }

    private void ProcessRebootCommand(string input)
    {
        var seq = threatConfig != null ? threatConfig.RebootSequenceCommands : new[] { "init 0", "check-mem", "start-oko" };

        if (currentRebootStep < seq.Length && input.Equals(seq[currentRebootStep], StringComparison.OrdinalIgnoreCase))
        {
            currentStateEnum = ThreatTerminalState.Rebooting;
            currentRebootStep++;
            OnStateChanged?.Invoke(currentStateEnum);

            if (currentRebootStep == 1)
            {
                OnOutputLineAdded?.Invoke("> Shutting down corrupted kernel... Memory cleared.");
            }
            else if (currentRebootStep == 2)
            {
                OnOutputLineAdded?.Invoke("> Memory integrity check: OK. 64KB RAM operational.");
            }

            if (currentRebootStep >= seq.Length)
            {
                OnOutputLineAdded?.Invoke("> Initializing OKO Satellite Warning System... BOOT COMPLETE.");
                CancelTelemetryTask();
                telemetryCts = new CancellationTokenSource();
                TriggerTelemetrySequenceAsync(telemetryCts.Token).Forget();
            }
        }
        else
        {
            var expected = currentRebootStep < seq.Length ? seq[currentRebootStep] : "Sequence complete";
            OnOutputLineAdded?.Invoke($"[BOOT ERROR] Command out of order. Expected: '{expected}'. Type /help for syntax.");
        }
    }

    private void ProcessOverrideCommand(string input)
    {
        var code = ExtractOverrideCode(input);
        var normalizedCode = NormalizeCode(code);
        enteredOverrideCode = normalizedCode;
        currentState = enteredOverrideCode;

        var correctCode = threatConfig != null ? NormalizeCode(threatConfig.CorrectOverrideCode) : "OVERRIDE-05";
        var incorrectCode = threatConfig != null ? NormalizeCode(threatConfig.IncorrectOverrideCode) : "OVERRIDE-99";

        if (normalizedCode == correctCode)
        {
            currentStateEnum = ThreatTerminalState.Overridden;
            SetAlarmActive(false);
            OnOutputLineAdded?.Invoke($"[OVERRIDE SUCCESSFUL] Code {normalizedCode} accepted.");
            OnOutputLineAdded?.Invoke("ALARM SILENCED. Categorized: SENSOR ANOMALY (Solar Reflection).");
            OnStateChanged?.Invoke(currentStateEnum);

            CheckSolve();
        }
        else if (normalizedCode == incorrectCode)
        {
            currentStateEnum = ThreatTerminalState.AlarmLocked;
            OnOutputLineAdded?.Invoke($"[OVERRIDE APPLIED] Code {normalizedCode} accepted.");
            OnOutputLineAdded?.Invoke("CRITICAL WARNING: Full First Strike Threat Confirmed. Counterstrike authorized!");
            OnStateChanged?.Invoke(currentStateEnum);
        }
        else
        {
            OnOutputLineAdded?.Invoke($"[ERROR] Invalid override code '{code}'. Valid options: OVERRIDE-05 (Anomaly), OVERRIDE-99 (Threat).");
        }
    }

    private async UniTaskVoid TriggerTelemetrySequenceAsync(CancellationToken cancellationToken)
    {
        currentStateEnum = ThreatTerminalState.TelemetryLive;
        OnStateChanged?.Invoke(currentStateEnum);

        var initialCount = threatConfig != null ? threatConfig.InitialMissileCount : 1;
        var updatedCount = threatConfig != null ? threatConfig.UpdatedMissileCount : 5;
        var delaySec = threatConfig != null ? threatConfig.TelemetryUpdateDelaySeconds : 2.5f;

        currentMissileCount = initialCount;
        OnMissileCountUpdated?.Invoke(currentMissileCount);
        OnOutputLineAdded?.Invoke($"[WARNING] {currentMissileCount} MISSILE LAUNCH DETECTED AT SECTOR 4!");

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delaySec), cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        currentMissileCount = updatedCount;
        OnMissileCountUpdated?.Invoke(currentMissileCount);
        OnOutputLineAdded?.Invoke($"[ALERT] TELEMETRY SPIKE: {currentMissileCount} MISSILES DETECTED IN FORMATION!");
        OnOutputLineAdded?.Invoke("[LOCKOUT] CONSOLE LOCKED. IMMEDIATE OVERRIDE REQUIRED!");

        currentStateEnum = ThreatTerminalState.AlarmLocked;
        OnStateChanged?.Invoke(currentStateEnum);
        SetAlarmActive(true);
    }

    private void SetAlarmActive(bool active)
    {
        if (alarmAudioSource != null)
        {
            if (active)
            {
                if (!alarmAudioSource.isPlaying)
                {
                    alarmAudioSource.Play();
                }
            }
            else
            {
                alarmAudioSource.Stop();
            }
        }

        if (alarmLight != null)
        {
            alarmLight.SetActive(active);
        }
    }

    private void CancelTelemetryTask()
    {
        if (telemetryCts != null)
        {
            telemetryCts.Cancel();
            telemetryCts.Dispose();
            telemetryCts = null;
        }
    }

    private static string ExtractOverrideCode(string input)
    {
        var trimmed = input.Trim();
        if (trimmed.StartsWith("override", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                return parts[1];
            }
        }
        return trimmed;
    }

    private static string NormalizeCode(string rawCode)
    {
        var clean = rawCode.Trim().ToUpperInvariant();
        if (clean == "05" || clean == "OVERRIDE-05" || clean == "OVERRIDE 05" || clean == "ANOMALY")
        {
            return "OVERRIDE-05";
        }
        if (clean == "99" || clean == "OVERRIDE-99" || clean == "OVERRIDE 99" || clean == "THREAT")
        {
            return "OVERRIDE-99";
        }
        if (!clean.StartsWith("OVERRIDE-") && clean.Length > 0)
        {
            return "OVERRIDE-" + clean;
        }
        return clean;
    }
}
