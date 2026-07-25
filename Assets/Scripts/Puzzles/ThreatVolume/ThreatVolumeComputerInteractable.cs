using UnityEngine;

/// <summary>
/// Interactable computer terminal component for Room 4.
/// Implements <see cref="IInteractable"/> attached to the computer collider.
/// Player looks at the computer collider and presses 'E' to open the CRT terminal view.
/// </summary>
public class ThreatVolumeComputerInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private ThreatVolumeTerminalUI terminalUI;
    [SerializeField] private string interactPrompt = "[E] Access Terminal";

    private void Awake()
    {
        if (terminalUI == null)
        {
            terminalUI = GetComponentInParent<ThreatVolumeTerminalUI>();
        }

        if (terminalUI == null)
        {
            terminalUI = FindFirstObjectByType<ThreatVolumeTerminalUI>();
        }
    }

    public void Interact(PlayerController player)
    {
        if (terminalUI != null)
        {
            terminalUI.Interact(player);
        }
    }

    public string GetPrompt()
    {
        if (terminalUI != null && terminalUI.IsOpen)
        {
            return string.Empty;
        }

        return interactPrompt;
    }
}
