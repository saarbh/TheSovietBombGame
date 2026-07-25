using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the victory and loss conditions of the game, handles displaying ending toasts,
/// and listens for developer cheat codes to speed up time.
/// </summary>
public class VictoryLoseManager
{
    private readonly GameManager gameManager;
    
    private string toastMessage = string.Empty;
    private bool isToastVisible = false;
    private float toastTimer = 0f;
    private GUIStyle toastStyle;

    public string ToastMessage => toastMessage;
    public bool IsToastVisible => isToastVisible;

    public VictoryLoseManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
        
        if (this.gameManager != null)
        {
            this.gameManager.OnGameEnded += HandleGameEnded;
            this.gameManager.OnGameStarted += HandleGameStarted;
        }
    }

    /// <summary>
    /// Cleans up event subscriptions.
    /// </summary>
    public void Cleanup()
    {
        if (gameManager != null)
        {
            gameManager.OnGameEnded -= HandleGameEnded;
            gameManager.OnGameStarted -= HandleGameStarted;
        }
    }

    /// <summary>
    /// Handles frame updates (such as cheat key detection).
    /// </summary>
    public void Update()
    {
        // Update toast timer
        if (isToastVisible && toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f)
            {
                isToastVisible = false;
            }
        }

#if UNITY_EDITOR
        // Developer cheat code: Shift + T reduces time remaining by 1 minute
        if (Keyboard.current != null)
        {
            if (Keyboard.current.shiftKey.isPressed && Keyboard.current.tKey.wasPressedThisFrame)
            {
                TriggerTimeCheat();
            }
        }
#endif
    }

#if UNITY_EDITOR
    private void TriggerTimeCheat()
    {
        if (gameManager == null || gameManager.WatchManager == null)
        {
            return;
        }

        if (gameManager.IsGameOver)
        {
            return;
        }

        Debug.Log("[VictoryLoseManager] Cheat triggered: Reducing remaining time by 60 seconds.");
        gameManager.WatchManager.ReduceTime(60f);

        // Display a temporary cheat toast
        ShowToast("Cheat: Reduced time by 1 minute!", 2f);
    }
#endif

    private void HandleGameStarted()
    {
        isToastVisible = false;
        toastMessage = string.Empty;
    }

    private void HandleGameEnded(EndingType ending)
    {
        if (gameManager == null)
        {
            return;
        }

        var leftControlRoom = gameManager.HasLeftControlRoom;
        var callChoice = gameManager.CallChoice;
        var allPuzzlesSolved = gameManager.AreAllPuzzlesSolved();

        toastMessage = EvaluateEndingMessage(ending, leftControlRoom, callChoice, allPuzzlesSolved);

        // Show ending toast permanently
        ShowToast(toastMessage, 9999f);
    }

    /// <summary>
    /// Evaluates the game ending type based on player status and choices.
    /// </summary>
    public EndingType EvaluateEnding(bool leftControlRoom, PhoneCallChoice callChoice, bool allPuzzlesSolved)
    {
        switch (callChoice)
        {
            case PhoneCallChoice.ReportIncomingNuke:
                return EndingType.NuclearWar;

            case PhoneCallChoice.ReportFalseAlarm:
                return allPuzzlesSolved ? EndingType.WorldSaved : EndingType.NuclearWar;

            case PhoneCallChoice.NoCallMade:
            default:
                return (!leftControlRoom || allPuzzlesSolved)
                    ? EndingType.WorldSaved
                    : EndingType.NuclearWar;
        }
    }

    /// <summary>
    /// Pure logic method to evaluate victory/lose messages for testing.
    /// </summary>
    public string EvaluateEndingMessage(EndingType ending, bool leftControlRoom, PhoneCallChoice callChoice, bool allPuzzlesSolved)
    {
        if (ending == EndingType.WorldSaved)
        {
            if (!leftControlRoom && callChoice == PhoneCallChoice.NoCallMade)
            {
                return "<b>VICTORY (Easter Egg):</b>\nYou did not leave the control room or make any panic calls. A false alarm was successfully handled internally!";
            }
            else if (allPuzzlesSolved && callChoice == PhoneCallChoice.ReportFalseAlarm)
            {
                return "<b>VICTORY:</b>\nYou solved all 4 room puzzles and reported a false alarm. The world is saved!";
            }
            else if (allPuzzlesSolved && callChoice == PhoneCallChoice.NoCallMade)
            {
                return "<b>VICTORY:</b>\nYou solved all 4 room puzzles and did not call down a strike. The crisis has passed!";
            }
            else
            {
                return "<b>VICTORY:</b>\nThe crisis has been averted and the world is saved!";
            }
        }
        else // EndingType.NuclearWar
        {
            if (leftControlRoom && callChoice == PhoneCallChoice.NoCallMade)
            {
                return "<b>DEFEAT:</b>\nYou left the control room, and the timer expired. A nuclear strike has been launched automatically!";
            }
            else if (!allPuzzlesSolved && callChoice == PhoneCallChoice.ReportIncomingNuke)
            {
                return "<b>DEFEAT:</b>\nYou failed to solve the puzzles and reported an incoming nuke. Escalation has caused nuclear war!";
            }
            else if (allPuzzlesSolved && callChoice == PhoneCallChoice.ReportIncomingNuke)
            {
                return "<b>DEFEAT:</b>\nYou solved all puzzles, but still mistakenly chose to report an incoming nuke. Escalation has caused nuclear war!";
            }
            else
            {
                return "<b>DEFEAT:</b>\nNuclear war has broken out!";
            }
        }
    }

    public void ShowToast(string message, float duration)
    {
        toastMessage = message;
        toastTimer = duration;
        isToastVisible = true;
    }

    /// <summary>
    /// Draws the GUI elements for the timer and endings/toasts.
    /// </summary>
    public void DrawGUI()
    {
        // Draw temporary debug timer overlay
        if (gameManager != null && gameManager.WatchManager != null && !gameManager.IsGameOver)
        {
            var debugTimerStyle = new GUIStyle(GUI.skin.box);
            debugTimerStyle.alignment = TextAnchor.MiddleCenter;
            debugTimerStyle.fontSize = 14;
            debugTimerStyle.richText = true;
            debugTimerStyle.normal.textColor = Color.green;

            var timeStr = gameManager.WatchManager.GetFormattedTime();
            var remainingSeconds = gameManager.WatchManager.TimeRemaining;

            var debugTimerRect = new Rect(10f, 10f, 180f, 35f);
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Box(debugTimerRect, $"<b>Timer: {timeStr}</b> ({remainingSeconds:F1}s)", debugTimerStyle);
        }

        if (!isToastVisible)
        {
            return;
        }

        if (toastStyle == null)
        {
            toastStyle = new GUIStyle(GUI.skin.box);
            toastStyle.alignment = TextAnchor.MiddleCenter;
            toastStyle.fontSize = 16;
            toastStyle.richText = true;
            toastStyle.normal.textColor = Color.white;
            toastStyle.padding = new RectOffset(15, 15, 10, 10);
        }

        var width = 500f;
        var height = 80f;
        var rect = new Rect((Screen.width - width) / 2f, 40f, width, height);

        // Draw shadow/bg overlay
        GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        GUI.Box(rect, toastMessage, toastStyle);

        // If game is over, also draw a restart instruction
        if (gameManager != null && gameManager.IsGameOver)
        {
            var restartRect = new Rect((Screen.width - width) / 2f, 40f + height + 10f, width, 30f);
            var restartStyle = new GUIStyle(GUI.skin.box);
            restartStyle.alignment = TextAnchor.MiddleCenter;
            restartStyle.fontSize = 12;
            restartStyle.normal.textColor = Color.yellow;
            GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            GUI.Box(restartRect, "Press <b>R</b> to restart the game", restartStyle);

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                gameManager.RestartScene();
            }
        }
    }
}
