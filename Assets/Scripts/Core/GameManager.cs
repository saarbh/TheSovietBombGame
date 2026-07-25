using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-scoped singleton that owns run state: puzzle progress, the player's phone
/// call, whether they left the control room, and which ending that adds up to.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Tooltip("Seconds to let an ending cutscene breathe before gameplay is torn down.")]
    [SerializeField] private float endGameSettleSeconds = 0.5f;

    public static GameManager Instance { get; private set; }

    private readonly PuzzleTracker puzzleTracker = new PuzzleTracker();

    public PuzzleTracker PuzzleTracker => puzzleTracker;

    public bool IsGameOver { get; private set; }
    public EndingType? FinalEnding { get; private set; }

    /// <summary>The active game watch and countdown manager.</summary>
    public WatchManager WatchManager { get; private set; }

    /// <summary>Set by <c>ControlRoomTrigger</c> the first time the player leaves the control room.</summary>
    public bool HasLeftControlRoom { get; private set; }

    /// <summary>Latest choice made at the phone; defaults to <see cref="PhoneCallChoice.NoCallMade"/>.</summary>
    public PhoneCallChoice CallChoice { get; private set; } = PhoneCallChoice.NoCallMade;

    public event Action OnGameStarted;

    /// <summary>Raised when the run resolves. Cutscenes and UI listen here rather than being called directly.</summary>
    public event Action<EndingType> OnGameEnded;

    private void Awake()
    {
        // Scene singleton, not persistent: a reloaded scene gets a fresh manager.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        WatchManager = new WatchManager();
        WatchManager.OnTimeExpired += OnTimeExpiredHandler;
    }

    private void Start()
    {
        StartGame();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (WatchManager is not null)
        {
            WatchManager.OnTimeExpired -= OnTimeExpiredHandler;
        }
    }

    public void StartGame()
    {
        IsGameOver = false;
        FinalEnding = null;
        HasLeftControlRoom = false;
        CallChoice = PhoneCallChoice.NoCallMade;

        OnGameStarted?.Invoke();

        if (WatchManager is not null)
        {
            WatchManager.StartCountdownAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    public void EndGame(EndingType ending)
    {
        // First ending wins - a phone call landing on the same frame the timer
        // expires must not resolve the run twice.
        if (IsGameOver)
        {
            return;
        }

        IsGameOver = true;
        FinalEnding = ending;

        if (WatchManager is not null)
        {
            WatchManager.StopCountdown();
        }

        OnGameEnded?.Invoke(ending);
        SettleEndingAsync(ending).Forget();
    }

    public void RestartScene()
    {
        var active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

    public bool AreAllPuzzlesSolved()
    {
        return puzzleTracker.IsAllPuzzlesSolved;
    }

    /// <summary>
    /// Records that the player crossed the control room threshold. One-way flag.
    /// </summary>
    public void MarkControlRoomDeparted()
    {
        HasLeftControlRoom = true;
    }

    /// <summary>
    /// Records the player's phone call and immediately resolves the run, since the
    /// call is the last meaningful decision available.
    /// </summary>
    public void SubmitPhoneCall(PhoneCallChoice choice)
    {
        if (IsGameOver)
        {
            return;
        }

        CallChoice = choice;
        EndGame(EvaluateEnding(HasLeftControlRoom, choice));
    }

    /// <summary>
    /// Ending decision matrix, per the architecture spec:
    ///
    /// WorldSaved
    ///   1. Stayed in the control room and made no call.
    ///   2. Solved all 4 puzzles and made no call.
    ///   3. Solved all 4 puzzles and reported a false alarm.
    ///
    /// NuclearWar
    ///   1. Left the control room and made no call (with puzzles unsolved).
    ///   2. Failed the puzzles, so the only call available was "report incoming nuke".
    ///   3. Solved the puzzles but still reported an incoming nuke.
    /// </summary>
    public EndingType EvaluateEnding(bool leftControlRoom, PhoneCallChoice callChoice)
    {
        var allPuzzlesSolved = AreAllPuzzlesSolved();

        switch (callChoice)
        {
            case PhoneCallChoice.ReportIncomingNuke:
                return EndingType.NuclearWar;

            case PhoneCallChoice.ReportFalseAlarm:
                // PhoneInteractable only offers this option once every puzzle is
                // solved; the guard keeps the rule true even if that gate changes.
                return allPuzzlesSolved ? EndingType.WorldSaved : EndingType.NuclearWar;

            case PhoneCallChoice.NoCallMade:
            default:
                // Never leaving the post, or leaving but having pieced the truth
                // together, both count as not escalating a false alarm.
                return (!leftControlRoom || allPuzzlesSolved)
                    ? EndingType.WorldSaved
                    : EndingType.NuclearWar;
        }
    }

    /// <summary>
    /// Hooked to <c>WatchManager.OnTimeExpired</c>. The clock running out is not an
    /// automatic loss: under the decision matrix, sitting tight without calling is
    /// a win, so the timeout is evaluated against the same rules as a phone call.
    /// </summary>
    public void OnTimeExpiredHandler()
    {
        if (IsGameOver)
        {
            return;
        }

        EndGame(EvaluateEnding(HasLeftControlRoom, CallChoice));
    }

    private async UniTaskVoid SettleEndingAsync(EndingType ending)
    {
        var token = this.GetCancellationTokenOnDestroy();

        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(endGameSettleSeconds),
                DelayType.UnscaledDeltaTime,
                cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Input is released here so the ending cutscene owns the camera; the
        // cutscene itself decides what to show via OnGameEnded.
        Debug.Log($"[GameManager] Run resolved: {ending}");
    }
}
