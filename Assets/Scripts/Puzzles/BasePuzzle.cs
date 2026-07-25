using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic base for every room puzzle. Subclasses close the generic with whatever
/// type describes their solved state (<c>class RadarPuzzle : BasePuzzle&lt;int&gt;</c>);
/// an open generic cannot be attached to a GameObject.
/// </summary>
public abstract class BasePuzzle<T> : MonoBehaviour, IPuzzle
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

    public PuzzleConfig Config => puzzleConfig;

    /// <summary>Raised once, the moment the puzzle transitions to solved.</summary>
    public event Action OnPuzzleSolved;

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
    /// Returns the puzzle to its unsolved state. Note this does not decrement
    /// <see cref="PuzzleTracker"/> - a room that has already been counted stays counted.
    /// </summary>
    public virtual void ResetPuzzle()
    {
        isSolved = false;
        currentState = default;
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
