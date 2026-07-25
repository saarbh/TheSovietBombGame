using TMPro;
using UnityEngine;

/// <summary>
/// The large CONFIRM lever. Per the design, nothing in a room locks until this is pulled:
/// the player experiments freely, then commits. Pulling it prints the room's card, shuts
/// the machinery down and makes the answer final - right or wrong.
///
/// Shared by every room. The room decides through <see cref="IConfirmablePuzzle"/> when the
/// lever is allowed to fire and what to say when it is not, so a curious player cannot seal
/// a room before they have attempted it.
/// </summary>
public class ConfirmLever : MonoBehaviour, IInteractable
{
    [Header("Puzzle")]
    [Tooltip("GameObject carrying the room's puzzle. Read as IConfirmablePuzzle. "
             + "Leave empty to find it on a parent.")]
    [SerializeField] private GameObject puzzleObject;

    [Header("Presentation")]
    [Tooltip("Verb shown in the prompt, e.g. Confirm for the generators or Classify for the IFF machine.")]
    [SerializeField] private string confirmVerb = "Confirm";

    [Header("Printout")]
    [Tooltip("Optional. The card text the printer produces.")]
    [SerializeField] private TMP_Text printoutDisplay;

    [Tooltip("Optional. Revealed when the card is printed.")]
    [SerializeField] private GameObject printedCard;

    private IConfirmablePuzzle puzzle;

    // An interface reference to a MonoBehaviour bypasses Unity's overridden ==, so a plain
    // null check on it is unreliable. Resolution is recorded once, here, and every guard
    // below tests this flag instead.
    private bool hasPuzzle;

    private void Awake()
    {
        ResolvePuzzle();

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
        if (!hasPuzzle)
        {
            Debug.LogError($"[Interact] {confirmVerb} lever has no IConfirmablePuzzle.", this);
            return;
        }

        if (puzzle.IsConfirmed)
        {
            Debug.Log($"[Interact] {confirmVerb} ignored - the result is already filed.", this);
            return;
        }

        if (!puzzle.CanConfirm)
        {
            Debug.Log($"[Interact] {confirmVerb} ignored - {puzzle.ConfirmBlockedReason}", this);
            return;
        }

        Debug.Log($"[Interact] {confirmVerb} lever pulled.", this);
        PrintCard(puzzle.Confirm());
    }

    public string GetPrompt()
    {
        if (!hasPuzzle)
        {
            return $"{confirmVerb} (unwired)";
        }

        if (puzzle.IsConfirmed)
        {
            return "Result already filed";
        }

        if (!puzzle.CanConfirm)
        {
            var reason = puzzle.ConfirmBlockedReason;
            return string.IsNullOrEmpty(reason) ? "Not ready" : reason;
        }

        return $"[E] {confirmVerb}";
    }

    private void ResolvePuzzle()
    {
        // The explicit reference wins; the parent search keeps rooms authored before this
        // field existed working without needing to be re-wired.
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

    private void PrintCard(PuzzleCard card)
    {
        if (printedCard != null)
        {
            printedCard.SetActive(true);
        }

        if (printoutDisplay == null)
        {
            return;
        }

        printoutDisplay.text = string.IsNullOrEmpty(card.Evidence)
            ? card.ToPrintedLine()
            : $"{card.ToPrintedLine()}\n{card.Evidence}";
    }
}
