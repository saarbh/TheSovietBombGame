using TMPro;
using UnityEngine;

/// <summary>
/// The large CONFIRM lever. Per the design, nothing in a room locks until this is pulled:
/// the player experiments freely, then commits. Pulling it prints the room's card, shuts
/// the machinery down and makes the answer final - right or wrong.
///
/// It refuses while the generators are idle or still spinning up, so a curious player
/// cannot seal the room before they have attempted it.
/// </summary>
public class ConfirmLever : MonoBehaviour, IInteractable
{
    [SerializeField] private GeneratorPuzzle puzzle;

    [Header("Printout")]
    [Tooltip("Optional. The card text the printer produces.")]
    [SerializeField] private TMP_Text printoutDisplay;

    [Tooltip("Optional. Revealed when the card is printed.")]
    [SerializeField] private GameObject printedCard;

    private void Awake()
    {
        if (puzzle == null)
        {
            puzzle = GetComponentInParent<GeneratorPuzzle>();
        }

        if (printedCard != null)
        {
            printedCard.SetActive(false);
        }

        if (printoutDisplay != null)
        {
            printoutDisplay.text = string.Empty;
        }
    }

    public void Interact(PlayerController player)
    {
        Debug.Log($"[Interact] CONFIRM lever used. {DescribeState()}", this);

        if (puzzle == null)
        {
            Debug.LogError("[Interact] CONFIRM lever has no GeneratorPuzzle reference.", this);
            return;
        }

        if (puzzle.IsConfirmed)
        {
            Debug.Log("[Interact] CONFIRM ignored - the result is already filed.", this);
            return;
        }

        if (!puzzle.AreAllGeneratorsAtFullPower())
        {
            Debug.Log("[Interact] CONFIRM ignored - start all three generators first.", this);
            return;
        }

        PrintCard(puzzle.Confirm());
    }

    public string GetPrompt()
    {
        if (puzzle == null)
        {
            return "Confirm (unwired)";
        }

        if (puzzle.IsConfirmed)
        {
            return "Result already filed";
        }

        if (!puzzle.AreAllGeneratorsAtFullPower())
        {
            return "Generators not ready";
        }

        return "[E] Confirm";
    }

    private string DescribeState()
    {
        if (puzzle == null)
        {
            return "puzzle=NULL";
        }

        return $"allAtFullPower={puzzle.AreAllGeneratorsAtFullPower()} confirmed={puzzle.IsConfirmed}";
    }

    private void PrintCard(GeneratorResult result)
    {
        if (printedCard != null)
        {
            printedCard.SetActive(true);
        }

        if (printoutDisplay == null)
        {
            return;
        }

        printoutDisplay.text = string.IsNullOrEmpty(result.Evidence)
            ? result.ToPrintedLine()
            : $"{result.ToPrintedLine()}\n{result.Evidence}";
    }
}
