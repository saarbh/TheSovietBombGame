using UnityEngine;

/// <summary>
/// Returns all three generators to idle immediately. Retrying has to be free: a mistimed
/// attempt already costs the player eight seconds of watching countdowns, and the room is
/// budgeted at well under a minute.
/// </summary>
public class ResetLever : MonoBehaviour, IInteractable
{
    [SerializeField] private GeneratorPuzzle puzzle;

    private void Awake()
    {
        if (puzzle == null)
        {
            puzzle = GetComponentInParent<GeneratorPuzzle>();
        }
    }

    public void Interact(PlayerController player)
    {
        Debug.Log("[Interact] RESET lever used.", this);

        if (puzzle == null)
        {
            Debug.LogError("[Interact] RESET lever has no GeneratorPuzzle reference.", this);
            return;
        }

        if (puzzle.IsConfirmed)
        {
            Debug.Log("[Interact] RESET ignored - the result is already filed and the panel is sealed.", this);
            return;
        }

        puzzle.ResetGenerators();
    }

    public string GetPrompt()
    {
        if (puzzle == null)
        {
            return "Reset (unwired)";
        }

        if (puzzle.IsConfirmed)
        {
            return "Panel sealed";
        }

        return "[E] Reset generators";
    }
}
