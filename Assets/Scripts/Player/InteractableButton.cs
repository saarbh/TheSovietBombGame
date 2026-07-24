using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A diegetic, look-at-and-press interactable. Attach to any object with a Collider;
/// when the player aims at it and presses Interact, <see cref="onInteracted"/> fires.
/// Wire the UnityEvent in the Inspector to whatever the button should do.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InteractableButton : MonoBehaviour, IInteractable
{
    [Tooltip("Crosshair prompt shown while the player is aiming at this button.")]
    [SerializeField] private string prompt = "[E] Press";

    [Tooltip("If true, the button can only be pressed once.")]
    [SerializeField] private bool singleUse;

    [Tooltip("Seconds before the button can be pressed again (ignored if Single Use).")]
    [SerializeField] private float cooldownSeconds;

    [Space]
    [Tooltip("Invoked each time the player successfully presses this button.")]
    [SerializeField] private UnityEvent onInteracted;

    /// <summary>Raised alongside the UnityEvent, carrying the presser (for code listeners).</summary>
    public event System.Action<PlayerController> Interacted;

    private bool hasBeenUsed;
    private float lastInteractTime = float.NegativeInfinity;

    /// <summary>True when the button will currently respond to a press.</summary>
    public bool CanInteract
    {
        get
        {
            if (singleUse && hasBeenUsed)
            {
                return false;
            }

            return Time.time >= lastInteractTime + cooldownSeconds;
        }
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract)
        {
            return;
        }

        hasBeenUsed = true;
        lastInteractTime = Time.time;

        onInteracted?.Invoke();
        Interacted?.Invoke(player);
    }

    public string GetPrompt()
    {
        // Hide the prompt once a single-use button is spent so it doesn't invite a dead press.
        return CanInteract ? prompt : string.Empty;
    }
}
