using UnityEngine;

/// <summary>
/// Scraps the current attempt so the player can start over. Retrying has to be free: a
/// mistimed generator run already costs eight seconds of watching countdowns, and every
/// room is budgeted at well under a minute.
///
/// Shared by every room; the room itself decides what "start over" means through
/// <see cref="IConfirmablePuzzle.ResetAttempt"/>.
/// </summary>
public class ResetLever : MonoBehaviour, IInteractable
{
    [Header("Puzzle")]
    [Tooltip("GameObject carrying the room's puzzle. Read as IConfirmablePuzzle. "
             + "Leave empty to find it on a parent.")]
    [SerializeField] private GameObject puzzleObject;

    [Header("Presentation")]
    [Tooltip("What the prompt offers to reset, e.g. generators or switches.")]
    [SerializeField] private string resetNoun = "generators";

    private IConfirmablePuzzle puzzle;

    // See ConfirmLever: an interface reference to a MonoBehaviour cannot be null-checked
    // reliably, so resolution is recorded once as a flag.
    private bool hasPuzzle;

    private void Awake()
    {
        if (puzzleObject != null)
        {
            puzzle = puzzleObject.GetComponent<IConfirmablePuzzle>();
        }

        if (puzzle == null)
        {
            puzzle = GetComponentInParent<IConfirmablePuzzle>();
        }

        hasPuzzle = puzzle != null;
    }

    public void Interact(PlayerController player)
    {
        Debug.Log("[Interact] RESET lever used.", this);

        if (!hasPuzzle)
        {
            Debug.LogError("[Interact] RESET lever has no IConfirmablePuzzle.", this);
            return;
        }

        if (!puzzle.CanReset)
        {
            Debug.Log("[Interact] RESET ignored - the result is already filed and the panel is sealed.", this);
            return;
        }

        puzzle.ResetAttempt();
    }

    public string GetPrompt()
    {
        if (!hasPuzzle)
        {
            return "Reset (unwired)";
        }

        if (!puzzle.CanReset)
        {
            return "Panel sealed";
        }

        return $"[E] Reset {resetNoun}";
    }
}
