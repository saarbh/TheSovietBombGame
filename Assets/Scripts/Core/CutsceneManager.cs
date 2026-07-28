using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Manages instantiation and playback of start and ending cutscenes.
/// Ensures the global watch/timer starts only after the start cutscene finishes.
/// </summary>
public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance { get; private set; }

    [Header("Start Cutscene")]
    [SerializeField] private GameObject startCutscenePrefab;
    [SerializeField] private bool playStartCutsceneOnGameStarted = true;

    [Header("Ending Cutscenes")]
    [SerializeField] private GameObject victoryCutscenePrefab;
    [SerializeField] private GameObject nuclearWarCutscenePrefab;

    [Header("Playback Options")]
    [SerializeField] private bool allowSkipWithSpaceOrEscape = true;

    private GameObject activeCutsceneInstance;
    private bool isCutscenePlaying;

    public bool IsCutscenePlaying => isCutscenePlaying;

    /// <summary>
    /// Returns true if a valid start cutscene prefab is assigned and enabled.
    /// </summary>
    public bool HasStartCutscene => playStartCutsceneOnGameStarted && startCutscenePrefab != null;

    /// <summary>
    /// Raised when a cutscene starts.
    /// </summary>
    public event Action<string> OnCutsceneStarted;

    /// <summary>
    /// Raised when a cutscene ends.
    /// </summary>
    public event Action<string> OnCutsceneEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            SubscribeToGameManager();
        }
    }

    private void Start()
    {
        // Re-check subscription in Start in case GameManager Awake ran after CutsceneManager OnEnable
        if (GameManager.Instance != null)
        {
            SubscribeToGameManager();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromGameManager();
    }

    private void SubscribeToGameManager()
    {
        UnsubscribeFromGameManager();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStarted += HandleGameStarted;
            GameManager.Instance.OnGameEnded += HandleGameEnded;
        }
    }

    private void UnsubscribeFromGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStarted -= HandleGameStarted;
            GameManager.Instance.OnGameEnded -= HandleGameEnded;
        }
    }

    private void HandleGameStarted()
    {
        if (HasStartCutscene)
        {
            PlayStartCutsceneAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
        else
        {
            // If no start cutscene, immediately start the watch countdown
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartWatchCountdown();
            }
        }
    }

    private void HandleGameEnded(EndingType ending)
    {
        var prefabToInstantiate = ending == EndingType.WorldSaved ? victoryCutscenePrefab : nuclearWarCutscenePrefab;

        if (prefabToInstantiate != null)
        {
            PlayCutscenePrefabAsync(prefabToInstantiate, $"Ending_{ending}", this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    /// <summary>
    /// Instantiates and plays the start cutscene, then starts the watch countdown upon completion.
    /// </summary>
    public async UniTask PlayStartCutsceneAsync(CancellationToken ct)
    {
        if (startCutscenePrefab == null)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartWatchCountdown();
            }

            return;
        }

        await PlayCutscenePrefabAsync(startCutscenePrefab, "StartCutscene", ct);

        // Crucial requirement: global watch starts only AFTER the cutscene ends
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartWatchCountdown();
        }
    }

    /// <summary>
    /// Core method to instantiate a cutscene prefab, run its PlayableDirector (if present),
    /// wait for completion, and clean up.
    /// </summary>
    public async UniTask PlayCutscenePrefabAsync(GameObject prefab, string cutsceneName, CancellationToken ct)
    {
        if (prefab == null)
        {
            return;
        }

        // Clean up previous instance if any
        if (activeCutsceneInstance != null)
        {
            Destroy(activeCutsceneInstance);
            activeCutsceneInstance = null;
        }

        isCutscenePlaying = true;
        OnCutsceneStarted?.Invoke(cutsceneName);

        activeCutsceneInstance = Instantiate(prefab);
        activeCutsceneInstance.name = $"[Cutscene]_{cutsceneName}";

        var director = activeCutsceneInstance.GetComponentInChildren<PlayableDirector>();

        if (director != null)
        {
            director.time = 0;
            director.Play();

            try
            {
                while (director != null && director.state == PlayState.Playing)
                {
                    if (allowSkipWithSpaceOrEscape && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)))
                    {
                        director.Stop();
                        break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Task canceled (e.g. object destroyed)
            }
        }
        else
        {
            // If no PlayableDirector, let the instantiated object exist for a brief moment or until destroyed externally
            await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: ct);
        }

        isCutscenePlaying = false;
        OnCutsceneEnded?.Invoke(cutsceneName);
    }

    /// <summary>
    /// Manually triggers a cutscene by prefab.
    /// </summary>
    public void TriggerCutscene(GameObject cutscenePrefab, string name = "CustomCutscene")
    {
        PlayCutscenePrefabAsync(cutscenePrefab, name, this.GetCancellationTokenOnDestroy()).Forget();
    }
}
