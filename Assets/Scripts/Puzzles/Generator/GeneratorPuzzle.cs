using System;
using UnityEngine;

/// <summary>
/// The Emergency Generator Room. Three generators have fixed startup times (3s, 5s, 8s)
/// and must all reach full power at the same moment, which means pulling the levers
/// slowest-first: C, then B, then A.
///
/// Nothing is judged until the player pulls CONFIRM - up to that point they may start,
/// reset and retry freely. Confirming always produces a card; only a correctly synced
/// run produces the truthful one.
/// </summary>
public class GeneratorPuzzle : BasePuzzle<bool>, IConfirmablePuzzle
{
    [Header("Generators")]
    [Tooltip("All generators that must land together. The doc uses three: 8s, 5s and 3s.")]
    [SerializeField] private GeneratorUnit[] generators = Array.Empty<GeneratorUnit>();

    [Header("Tuning")]
    [Tooltip("Largest allowed spread between the first and last generator reaching full power.")]
    [SerializeField] private float syncToleranceSeconds = 0.75f;

    private bool areGeneratorsSynced;

    /// <summary>True once CONFIRM has been pulled; the answer can no longer be changed.</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>Spread between the first and last generator to reach power, in seconds.</summary>
    public float LastSyncSpread { get; private set; }

    /// <summary>
    /// True when every generator has finished starting, so the attempt is complete and
    /// can be filed. Confirming with generators idle or still spinning up is a nonsense
    /// action, and letting it through permanently seals the room before the player has
    /// done anything.
    /// </summary>
    public bool CanConfirm => !IsConfirmed && AreAllGeneratorsAtFullPower();

    /// <summary>Why CONFIRM is refused, for the lever's prompt. Null when it is available.</summary>
    public string ConfirmBlockedReason
    {
        get
        {
            if (IsConfirmed)
            {
                return "Result already filed";
            }

            return AreAllGeneratorsAtFullPower() ? null : "Generators not ready";
        }
    }

    /// <summary>The panel seals on CONFIRM; until then a retry costs nothing.</summary>
    public bool CanReset => !IsConfirmed;

    /// <summary><see cref="IConfirmablePuzzle"/> name for <see cref="ResetGenerators"/>.</summary>
    public void ResetAttempt()
    {
        ResetGenerators();
    }

    /// <summary>
    /// Raised when the final generator lands, with whether the three landed inside tolerance.
    /// Drives the "verification system online" hum or the comedy misfire.
    /// </summary>
    public event Action<bool> OnSyncEvaluated;

    /// <summary>Raised when CONFIRM is pulled, carrying the printed card.</summary>
    public event Action<PuzzleCard> OnConfirmed;

    /// <summary>Raised when the generators are returned to idle.</summary>
    public event Action OnGeneratorsReset;

    private void OnEnable()
    {
        foreach (var generator in generators)
        {
            if (generator == null)
            {
                continue;
            }

            generator.OnReachedFullPower += HandleGeneratorReachedFullPower;
        }
    }

    private void OnDisable()
    {
        foreach (var generator in generators)
        {
            if (generator == null)
            {
                continue;
            }

            generator.OnReachedFullPower -= HandleGeneratorReachedFullPower;
        }
    }

    public override void InitializePuzzle()
    {
        base.InitializePuzzle();

        targetState = true;
        currentState = false;
        areGeneratorsSynced = false;
    }

    /// <summary>
    /// Returns every generator to idle so the player can retry. Refused once confirmed.
    /// </summary>
    public void ResetGenerators()
    {
        if (IsConfirmed)
        {
            return;
        }

        foreach (var generator in generators)
        {
            if (generator == null)
            {
                continue;
            }

            generator.ResetToIdle();
        }

        areGeneratorsSynced = false;
        LastSyncSpread = 0f;

        Debug.Log("[Generator] RESET - all generators returned to idle.", this);
        OnGeneratorsReset?.Invoke();
    }

    /// <summary>
    /// Locks in the player's attempt and prints the room's card. Always produces a result:
    /// a wrong run still yields a code character, with misleading evidence attached.
    /// </summary>
    public PuzzleCard Confirm()
    {
        if (IsConfirmed)
        {
            return FiledCard ?? BuildCard(IsSolved);
        }

        if (!AreAllGeneratorsAtFullPower())
        {
            // Guarded rather than silently filed: sealing the room before the player
            // has run the generators would leave them with a dead puzzle and no reset.
            Debug.LogWarning("[Generator] CONFIRM refused - not every generator is at full power yet.", this);
            return BuildCard(false);
        }

        IsConfirmed = true;

        // The solve is only evaluated here, never while the player is still experimenting.
        // Judging on the fly would let a lucky sync be counted even if the player then
        // reset the room and confirmed something wrong.
        currentState = areGeneratorsSynced;
        CheckSolve();

        // Resolved regardless of correctness - the room is done with the player either way,
        // which is what the exit door keys off. This also files the card into the inventory,
        // so FiledCard is the authoritative copy from here on.
        MarkResolved(IsSolved);

        var card = FiledCard.Value;

        Debug.Log($"[Generator] CONFIRM filed - spread {LastSyncSpread:0.00}s, correct={card.WasCorrect}, "
                  + $"card \"{card.ToPrintedLine()}\". Room is now sealed.", this);

        OnConfirmed?.Invoke(card);

        return card;
    }

    public override void ResetPuzzle()
    {
        base.ResetPuzzle();

        IsConfirmed = false;
        areGeneratorsSynced = false;
        LastSyncSpread = 0f;

        ResetGenerators();
    }

    private void HandleGeneratorReachedFullPower(GeneratorUnit unit)
    {
        if (IsConfirmed || !AreAllGeneratorsAtFullPower())
        {
            return;
        }

        LastSyncSpread = CalculateCompletionSpread();
        areGeneratorsSynced = LastSyncSpread <= syncToleranceSeconds;

        Debug.Log($"[Generator] All three at full power. Spread {LastSyncSpread:0.000}s "
                  + $"(tolerance {syncToleranceSeconds:0.00}s) -> {(areGeneratorsSynced ? "SYNCED" : "MISFIRE")}. "
                  + "Pull CONFIRM to file, or RESET to retry.", this);

        OnSyncEvaluated?.Invoke(areGeneratorsSynced);
    }

    /// <summary>True when every wired generator has finished spinning up.</summary>
    public bool AreAllGeneratorsAtFullPower()
    {
        if (generators.Length == 0)
        {
            return false;
        }

        foreach (var generator in generators)
        {
            if (generator == null || !generator.IsAtFullPower)
            {
                return false;
            }
        }

        return true;
    }

    private float CalculateCompletionSpread()
    {
        var earliest = float.MaxValue;
        var latest = float.MinValue;

        foreach (var generator in generators)
        {
            if (generator == null)
            {
                continue;
            }

            earliest = Mathf.Min(earliest, generator.CompletionTime);
            latest = Mathf.Max(latest, generator.CompletionTime);
        }

        return latest - earliest;
    }
}
