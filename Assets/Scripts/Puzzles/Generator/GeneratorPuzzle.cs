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
public class GeneratorPuzzle : BasePuzzle<bool>
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

    /// <summary>
    /// Raised when the final generator lands, with whether the three landed inside tolerance.
    /// Drives the "verification system online" hum or the comedy misfire.
    /// </summary>
    public event Action<bool> OnSyncEvaluated;

    /// <summary>Raised when CONFIRM is pulled, carrying the printed card.</summary>
    public event Action<GeneratorResult> OnConfirmed;

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
    public GeneratorResult Confirm()
    {
        if (IsConfirmed)
        {
            return BuildResult();
        }

        if (!AreAllGeneratorsAtFullPower())
        {
            // Guarded rather than silently filed: sealing the room before the player
            // has run the generators would leave them with a dead puzzle and no reset.
            Debug.LogWarning("[Generator] CONFIRM refused - not every generator is at full power yet.", this);
            return BuildResult();
        }

        IsConfirmed = true;

        // The solve is only evaluated here, never while the player is still experimenting.
        // Judging on the fly would let a lucky sync be counted even if the player then
        // reset the room and confirmed something wrong.
        currentState = areGeneratorsSynced;
        CheckSolve();

        var result = BuildResult();

        Debug.Log($"[Generator] CONFIRM filed - spread {LastSyncSpread:0.00}s, correct={result.WasCorrect}, "
                  + $"card \"{result.ToPrintedLine()}\". Room is now sealed.", this);

        OnConfirmed?.Invoke(result);

        return result;
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

    private GeneratorResult BuildResult()
    {
        var wasCorrect = IsSolved;

        if (puzzleConfig == null)
        {
            // Config is authoring data; a missing asset must not stop the room from
            // emitting a card, since the finale depends on every room producing one.
            return new GeneratorResult("CONFIRMATION", wasCorrect ? '9' : '?', string.Empty, wasCorrect);
        }

        // OutputFor keeps the correct/wrong split in one place: a failed room hands out a
        // DIFFERENT character, not just different prose. Emitting the correct digit either
        // way would make the assembled final code right no matter how badly the player
        // played, which removes the point of the central decoder.
        var output = puzzleConfig.OutputFor(wasCorrect);

        return new GeneratorResult(
            puzzleConfig.StageLabel,
            output.CodeCharacter,
            output.Evidence,
            wasCorrect);
    }
}
