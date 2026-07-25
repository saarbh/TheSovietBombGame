using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Component attached to the phone in the control room. Allows the player to pick it up,
/// presenting choices based on whether all puzzles are solved.
/// </summary>
public class PhoneInteractable : MonoBehaviour, IInteractable
{
    [Header("References")]
    [Tooltip("The collider used for player interaction.")]
    [SerializeField] private Collider interactionCollider;

    private bool isMenuOpen = false;
    private PlayerController activePlayer;

    public string GetPrompt()
    {
        return "[E] Use Phone";
    }

    public void Interact(PlayerController player)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver)
        {
            return;
        }

        activePlayer = player;
        isMenuOpen = true;
        
        // Disable player input and free the cursor
        activePlayer.SetInputEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseMenu()
    {
        isMenuOpen = false;
        
        if (activePlayer != null)
        {
            activePlayer.SetInputEnabled(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnGUI()
    {
        if (!isMenuOpen)
        {
            return;
        }

        if (GameManager.Instance == null || GameManager.Instance.IsGameOver)
        {
            CloseMenu();
            return;
        }

        var allPuzzlesSolved = GameManager.Instance.AreAllPuzzlesSolved();

        var style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperCenter;
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(10, 10, 10, 10);

        var buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 12;

        var width = 400f;
        var height = allPuzzlesSolved ? 180f : 140f;
        var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

        GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        
        // Draw menu container
        GUI.Box(rect, "<b>SECURE PHONE INTERCOM</b>\nSelect option to report to command:", style);

        var buttonWidth = 360f;
        var buttonHeight = 30f;
        var currentY = rect.y + 45f;

        if (allPuzzlesSolved)
        {
            if (GUI.Button(new Rect(rect.x + 20f, currentY, buttonWidth, buttonHeight), "Report False Alarm (Crisis Averted)", buttonStyle))
            {
                SubmitChoice(PhoneCallChoice.ReportFalseAlarm);
            }
            currentY += buttonHeight + 10f;
        }

        if (GUI.Button(new Rect(rect.x + 20f, currentY, buttonWidth, buttonHeight), "Report Incoming Nuclear Strike (Escalate/Retaliate)", buttonStyle))
        {
            SubmitChoice(PhoneCallChoice.ReportIncomingNuke);
        }
        currentY += buttonHeight + 10f;

        if (GUI.Button(new Rect(rect.x + 20f, currentY, buttonWidth, buttonHeight), "Hang Up", buttonStyle))
        {
            CloseMenu();
        }
    }

    private void SubmitChoice(PhoneCallChoice choice)
    {
        isMenuOpen = false;
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SubmitPhoneCall(choice);
        }
    }
}
