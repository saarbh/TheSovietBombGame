using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic base for every room puzzle. Subclasses close the generic with whatever
/// type describes their solved state (<c>class RadarPuzzle : BasePuzzle&lt;int&gt;</c>);
/// an open generic cannot be attached to a GameObject.
/// </summary>
public abstract class BasePuzzle<T> : MonoBehaviour, IPuzzle, IPuzzleResolution
{
    [Header("Config")]
    [SerializeField] protected PuzzleConfig puzzleConfig;

    [Tooltip("Register with GameManager's PuzzleTracker on Start. Off for isolated test scenes.")]
    [SerializeField] private bool registerWithTracker = true;

    protected T currentState;
    protected T targetState;

    private bool isSolved;
    private bool isRegistered;

    public bool IsSolved => isSolved;

    /// <summary>
    /// True once the player has filed an answer, whether or not it was right. Doors and
    /// other room-exit behaviour hang off this rather than <see cref="IsSolved"/>, since
    /// a wrong answer still ends the player's business in the room.
    /// </summary>
    public bool IsResolved { get; private set; }

    public PuzzleConfig Config => puzzleConfig;

    /// <summary>
    /// The card this room filed, or null while it is unresolved. A nullable struct, not a
    /// sentinel value, so "no answer yet" can never be confused with a blank card.
    /// </summary>
    public PuzzleCard? FiledCard { get; private set; }

    /// <summary>Raised once, the moment the puzzle transitions to solved.</summary>
    public event Action OnPuzzleSolved;

    /// <summary>Raised once the room is resolved. The flag is whether the answer was correct.</summary>
    public event Action<bool> OnResolved;

    protected virtual void Awake()
    {
        InitializePuzzle();
    }

    // Start, not Awake: GameManager assigns its Instance in Awake, so registering any
    // earlier is a race that silently drops the puzzle from the tracker.
    protected virtual void Start()
    {
        RegisterWithTracker();
    }

    protected virtual void OnDestroy()
    {
        UnregisterFromTracker();
    }

    /// <summary>
    /// Sets up <see cref="targetState"/> and any starting state. Subclasses must call base.
    /// </summary>
    public virtual void InitializePuzzle()
    {
        isSolved = false;
    }

    /// <summary>
    /// Compares current state against the target. Fires <see cref="OnPuzzleSolved"/> exactly
    /// once on the unsolved-to-solved transition, and never again for that instance.
    /// </summary>
    public virtual bool CheckSolve()
    {
        var matches = EqualityComparer<T>.Default.Equals(currentState, targetState);

        if (!matches || isSolved)
        {
            return matches;
        }

        isSolved = true;
        OnPuzzleSolved?.Invoke();

        return true;
    }

    /// <summary>
    /// Marks the room as finished with, right or wrong, files its card into the player's
    /// inventory and raises <see cref="OnResolved"/> exactly once. Subclasses call this
    /// when the player commits an answer.
    /// </summary>
    protected void MarkResolved(bool wasCorrect)
    {
        if (IsResolved)
        {
            return;
        }

        IsResolved = true;

        // Filed before OnResolved fires, so every listener - doors, printers, the evidence
        // panel - sees an inventory that already contains this room's card.
        FileCardForAttempt(wasCorrect);

        OnResolved?.Invoke(wasCorrect);
    }

    /// <summary>
    /// The card for the attempt just judged. Override only for a room whose card is not
    /// fully described by its <see cref="PuzzleConfig"/>.
    /// </summary>
    protected virtual PuzzleCard BuildCard(bool wasCorrect)
    {
        if (puzzleConfig == null)
        {
            // Config is authoring data; a missing asset must not stop the room emitting a
            // card, since the finale depends on every room producing one. The placeholder
            // is loud on purpose - a '?' in the final code is easier to trace than a digit
            // that happens to be plausible.
            Debug.LogWarning($"[{name}] resolved with no PuzzleConfig assigned; filing a placeholder card.", this);

            return new PuzzleCard(VerificationStage.Detect, "UNCONFIGURED", '?', string.Empty, wasCorrect, name);
        }

        return puzzleConfig.BuildCard(wasCorrect);
    }

    /// <summary>
    /// Returns the puzzle to its unsolved state. Note this does not decrement
    /// <see cref="PuzzleTracker"/> - a room that has already been counted stays counted.
    /// </summary>
    public virtual void ResetPuzzle()
    {
        isSolved = false;
        IsResolved = false;
        currentState = default;

        WithdrawFiledCard();
    }

    private void FileCardForAttempt(bool wasCorrect)
    {
        var card = BuildCard(wasCorrect);
        FiledCard = card;

        // Standalone single-room test scenes have no GameManager, and that is a supported
        // setup: the room still resolves and still prints, the card just has nowhere to go.
        if (GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.CardInventory.FileCard(card);
    }

    /// <summary>
    /// Takes this room's card back out of the inventory on reset, so a re-solve files a
    /// fresh one rather than being refused as a duplicate stage.
    /// </summary>
    private void WithdrawFiledCard()
    {
        if (!FiledCard.HasValue)
        {
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CardInventory.WithdrawCard(FiledCard.Value.Stage);
        }

        FiledCard = null;
    }

    private void RegisterWithTracker()
    {
        if (!registerWithTracker || isRegistered)
        {
            return;
        }

        // GameManager is a UnityEngine.Object, so an explicit == null comparison is
        // required - pattern matching would miss a destroyed manager. Standalone puzzle
        // scenes have no GameManager at all, and that is a supported setup, not an error.
        if (GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.PuzzleTracker.RegisterPuzzle(this);
        isRegistered = true;
    }

    private void UnregisterFromTracker()
    {
        if (!isRegistered || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.PuzzleTracker.UnregisterPuzzle(this);
        isRegistered = false;
    }
}
