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

    [Header("Verification")]
    [Tooltip("Stages the central decoder expects. Set this to the rooms actually in the build - "
             + "the doc's five essential rooms are Detect, Confirm, Classify, Authenticate, Authorize.")]
    [SerializeField]
    private VerificationStage[] requiredStages =
    {
        VerificationStage.Detect,
        VerificationStage.Confirm,
        VerificationStage.Classify,
        VerificationStage.Authenticate,
        VerificationStage.Authorize,
    };

    public static GameManager Instance { get; private set; }

    private readonly PuzzleTracker puzzleTracker = new PuzzleTracker();
    private readonly PuzzleCardInventory cardInventory = new PuzzleCardInventory();

    public PuzzleTracker PuzzleTracker => puzzleTracker;

    /// <summary>Every card the player is carrying. Rooms file into this the moment they resolve.</summary>
    public PuzzleCardInventory CardInventory => cardInventory;

    /// <summary>
    /// True once a card exists for every required stage, right or wrong. This is what the
    /// central decoder gates on - note it is deliberately NOT the same question as
    /// <see cref="AreAllPuzzlesSolved"/>, since a filed wrong answer still counts as done.
    /// </summary>
    public bool IsVerificationComplete => cardInventory.IsComplete;

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

    private VictoryLoseManager victoryLoseManager;

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

        // Declared in Awake so a puzzle that somehow resolves on the first frame still
        // files against a fully configured inventory.
        cardInventory.SetRequiredStages(requiredStages);

        victoryLoseManager = new VictoryLoseManager(this);
    }

    private void Start()
    {
        StartGame();
    }

    private void Update()
    {
        if (victoryLoseManager != null)
        {
            victoryLoseManager.Update();
        }
    }

    private void OnGUI()
    {
        if (victoryLoseManager != null)
        {
            victoryLoseManager.DrawGUI();
        }
    }

    private void OnDestroy()
    {
        if (WatchManager is not null)
        {
            WatchManager.OnTimeExpired -= OnTimeExpiredHandler;
        }

        if (victoryLoseManager != null)
        {
            victoryLoseManager.Cleanup();
            victoryLoseManager = null;
        }
    }

    public void StartGame()
    {
        IsGameOver = false;
        FinalEnding = null;
        HasLeftControlRoom = false;
        CallChoice = PhoneCallChoice.NoCallMade;
        cardInventory.Clear();

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
        if (victoryLoseManager != null)
        {
            return victoryLoseManager.EvaluateEnding(leftControlRoom, callChoice, AreAllPuzzlesSolved());
        }
        return EndingType.NuclearWar;
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
