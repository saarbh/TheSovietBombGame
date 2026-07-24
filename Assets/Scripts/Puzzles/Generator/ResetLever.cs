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
        if (puzzle == null || puzzle.IsConfirmed)
        {
            return;
        }

        puzzle.ResetGenerators();
    }

    public string GetPrompt()
    {
        if (puzzle != null && puzzle.IsConfirmed)
        {
            return "Panel sealed";
        }

        return "[E] Reset generators";
    }
}
