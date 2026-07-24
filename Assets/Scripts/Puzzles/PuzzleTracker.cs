using System;
using System.Collections.Generic;

/// <summary>
/// Counts how many room puzzles are solved. Plain C# - owned by <see cref="GameManager"/>,
/// never a MonoBehaviour, so it can be unit tested without a scene.
/// </summary>
public class PuzzleTracker
{
    public const int TOTAL_ROOM_PUZZLES = 4;

    private readonly HashSet<IPuzzle> registeredPuzzles = new HashSet<IPuzzle>();
    private int solvedPuzzlesCount;

    public int SolvedPuzzlesCount => solvedPuzzlesCount;

    public bool IsAllPuzzlesSolved => solvedPuzzlesCount >= TOTAL_ROOM_PUZZLES;

    /// <summary>Raised after each increment, with (solved, total).</summary>
    public event Action<int, int> OnPuzzleCounterIncremented;

    /// <summary>
    /// Subscribes to a puzzle so its solve is counted exactly once.
    /// Re-registering the same instance is a no-op.
    /// </summary>
    public void RegisterPuzzle(IPuzzle puzzle)
    {
        if (puzzle == null || !registeredPuzzles.Add(puzzle))
        {
            return;
        }

        puzzle.OnPuzzleSolved += IncrementSolvedCount;
    }

    public void UnregisterPuzzle(IPuzzle puzzle)
    {
        if (puzzle == null || !registeredPuzzles.Remove(puzzle))
        {
            return;
        }

        puzzle.OnPuzzleSolved -= IncrementSolvedCount;
    }

    public void IncrementSolvedCount()
    {
        // Clamped so a puzzle that double-fires its event can't inflate the counter
        // past the total and falsely trigger the all-solved gate.
        solvedPuzzlesCount = Math.Min(solvedPuzzlesCount + 1, TOTAL_ROOM_PUZZLES);
        OnPuzzleCounterIncremented?.Invoke(solvedPuzzlesCount, TOTAL_ROOM_PUZZLES);
    }

    public void Reset()
    {
        foreach (var puzzle in registeredPuzzles)
        {
            puzzle.OnPuzzleSolved -= IncrementSolvedCount;
        }

        registeredPuzzles.Clear();
        solvedPuzzlesCount = 0;
    }
}
