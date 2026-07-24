using System;

/// <summary>
/// Contract every room puzzle satisfies so <see cref="PuzzleTracker"/> can count
/// progress without knowing what the puzzle actually is.
/// </summary>
public interface IPuzzle
{
    bool IsSolved { get; }

    /// <summary>Raised once, the moment the puzzle transitions to solved.</summary>
    event Action OnPuzzleSolved;

    void InitializePuzzle();

    bool CheckSolve();

    void ResetPuzzle();
}
