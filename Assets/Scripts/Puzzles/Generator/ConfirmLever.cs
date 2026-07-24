using TMPro;
using UnityEngine;

/// <summary>
/// The large CONFIRM lever. Per the design, nothing in a room locks until this is pulled:
/// the player experiments freely, then commits. Pulling it prints the room's card, shuts
/// the machinery down and makes the answer final - right or wrong.
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
    }

    public void Interact(PlayerController player)
    {
        if (puzzle == null || puzzle.IsConfirmed)
        {
            return;
        }

        PrintCard(puzzle.Confirm());
    }

    public string GetPrompt()
    {
        if (puzzle != null && puzzle.IsConfirmed)
        {
            return "Result already filed";
        }

        return "[E] Confirm";
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
