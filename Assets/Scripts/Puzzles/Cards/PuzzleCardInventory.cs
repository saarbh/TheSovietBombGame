using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Everything the player is carrying. Rooms file cards here the moment an answer is
/// confirmed - per the design doc the card "enters their inventory automatically", so
/// there is no pickup step and no way to drop or lose one.
///
/// Plain C# and owned by <see cref="GameManager"/>, exactly like <see cref="PuzzleTracker"/>:
/// scene-independent, unit-testable, and completely unaware of how the cards are shown.
/// The on-screen panel is one view among several possible ones; a diegetic clipboard prop
/// would subscribe to the same events and need no changes here.
/// </summary>
public class PuzzleCardInventory
{
    private readonly Dictionary<VerificationStage, PuzzleCard> cardsByStage =
        new Dictionary<VerificationStage, PuzzleCard>();

    // Kept sorted on insert so views never have to sort during a redraw.
    private readonly List<PuzzleCard> orderedCards = new List<PuzzleCard>();

    private readonly List<VerificationStage> requiredStages = new List<VerificationStage>();

    private readonly StringBuilder codeBuilder = new StringBuilder(8);

    private string cachedAssembledCode = string.Empty;
    private bool isAssembledCodeDirty = true;

    /// <summary>Cards the player holds, in verification-procedure order.</summary>
    public IReadOnlyList<PuzzleCard> CardsInProcedureOrder => orderedCards;

    /// <summary>
    /// Stages the central decoder expects before it will produce a verdict. Set from
    /// <see cref="GameManager"/> so the count follows the rooms actually in the build,
    /// rather than a hard-coded puzzle total.
    /// </summary>
    public IReadOnlyList<VerificationStage> RequiredStages => requiredStages;

    public int CardCount => orderedCards.Count;

    /// <summary>True once every required stage has a card, right or wrong.</summary>
    public bool IsComplete
    {
        get
        {
            for (var i = 0; i < requiredStages.Count; i++)
            {
                if (!cardsByStage.ContainsKey(requiredStages[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// True when every card held was earned correctly. Note this is not the same as the
    /// assembled code being right - see <see cref="IsComplete"/> for the missing-card case.
    /// </summary>
    public bool AreAllCardsCorrect
    {
        get
        {
            for (var i = 0; i < orderedCards.Count; i++)
            {
                if (!orderedCards[i].WasCorrect)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// The characters of every held card, concatenated in procedural order. Missing stages
    /// simply leave the code short - the console decides how to present the gap.
    /// </summary>
    public string AssembledCode
    {
        get
        {
            if (!isAssembledCodeDirty)
            {
                return cachedAssembledCode;
            }

            codeBuilder.Clear();

            for (var i = 0; i < orderedCards.Count; i++)
            {
                codeBuilder.Append(orderedCards[i].CodeCharacter);
            }

            cachedAssembledCode = codeBuilder.ToString();
            isAssembledCodeDirty = false;

            return cachedAssembledCode;
        }
    }

    /// <summary>Raised for each newly filed card.</summary>
    public event Action<PuzzleCard> OnCardFiled;

    /// <summary>Raised after any change at all, including withdrawals and clears. Views listen here.</summary>
    public event Action OnInventoryChanged;

    /// <summary>
    /// Records a card against its stage. Returns false when that stage is already filed:
    /// a room resolves exactly once, so a second card for the same stage means either a
    /// double-fire or two PuzzleConfig assets claiming the same stage. Refusing rather
    /// than overwriting keeps the first, genuinely earned answer.
    /// </summary>
    public bool FileCard(PuzzleCard card)
    {
        if (cardsByStage.TryGetValue(card.Stage, out var existing))
        {
            UnityEngine.Debug.LogWarning(
                $"[CardInventory] '{card.SourcePuzzleId}' tried to file {card.Stage} but "
                + $"'{existing.SourcePuzzleId}' already holds that stage. Card discarded.");

            return false;
        }

        cardsByStage.Add(card.Stage, card);
        InsertInProcedureOrder(card);

        isAssembledCodeDirty = true;

        OnCardFiled?.Invoke(card);
        OnInventoryChanged?.Invoke();

        return true;
    }

    public bool HasCardFor(VerificationStage stage)
    {
        return cardsByStage.ContainsKey(stage);
    }

    public bool TryGetCard(VerificationStage stage, out PuzzleCard card)
    {
        return cardsByStage.TryGetValue(stage, out card);
    }

    /// <summary>
    /// Removes a stage's card. Only a puzzle reset should ever call this - the player has
    /// no way to discard evidence.
    /// </summary>
    public bool WithdrawCard(VerificationStage stage)
    {
        if (!cardsByStage.Remove(stage))
        {
            return false;
        }

        for (var i = 0; i < orderedCards.Count; i++)
        {
            if (orderedCards[i].Stage != stage)
            {
                continue;
            }

            orderedCards.RemoveAt(i);
            break;
        }

        isAssembledCodeDirty = true;
        OnInventoryChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// Declares which stages the finale needs. Replaces any previous list; a null or empty
    /// list means "whatever the player happens to collect", which is the right behaviour
    /// for isolated single-room test scenes.
    /// </summary>
    public void SetRequiredStages(IReadOnlyList<VerificationStage> stages)
    {
        requiredStages.Clear();

        if (stages is null)
        {
            return;
        }

        for (var i = 0; i < stages.Count; i++)
        {
            if (requiredStages.Contains(stages[i]))
            {
                UnityEngine.Debug.LogWarning(
                    $"[CardInventory] Required stage {stages[i]} listed more than once; ignoring the duplicate.");

                continue;
            }

            requiredStages.Add(stages[i]);
        }

        requiredStages.Sort(CompareStages);
        OnInventoryChanged?.Invoke();
    }

    public void Clear()
    {
        if (orderedCards.Count == 0)
        {
            return;
        }

        cardsByStage.Clear();
        orderedCards.Clear();

        isAssembledCodeDirty = true;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Insertion sort into the ordered list. The list never exceeds seven entries, so this
    /// beats re-sorting and keeps the view's redraw allocation-free.
    /// </summary>
    private void InsertInProcedureOrder(PuzzleCard card)
    {
        for (var i = 0; i < orderedCards.Count; i++)
        {
            if (orderedCards[i].Stage <= card.Stage)
            {
                continue;
            }

            orderedCards.Insert(i, card);
            return;
        }

        orderedCards.Add(card);
    }

    private static int CompareStages(VerificationStage left, VerificationStage right)
    {
        return ((int)left).CompareTo((int)right);
    }
}
