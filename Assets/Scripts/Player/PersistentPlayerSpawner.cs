using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the DontDestroyOnLoad player working across scene loads: finds the new scene's
/// <see cref="PlayerSpawnAnchor"/>, parks the player on it, and power-cycles the
/// GameObject so every component re-runs OnEnable and starts listening again.
///
/// Also the player's singleton guard. The first instance survives every load; any player
/// already sitting in a scene the travelling player arrives in destroys itself, so a scene
/// can keep its own Player for solo testing without producing two at runtime.
///
/// Lives on the player root, next to <see cref="PlayerController"/>.
/// </summary>
public class PersistentPlayerSpawner : MonoBehaviour
{
    /// <summary>The one surviving player. Null before the first one wakes.</summary>
    public static PersistentPlayerSpawner Instance { get; private set; }

    // Set on the loser of the singleton race so its OnEnable does not subscribe and its
    // Update-time work never starts - it is destroyed at the end of the frame, not instantly.
    private bool isDuplicate;

    // Set once the player lands in a new scene; cleared when they take their first step
    // and the cursor is locked.
    private bool awaitingFirstMove;

    // True in scenes with no anchor (menu, cutscene): the player is parked and the mouse
    // belongs to the UI.
    private bool isDormant;

    [Tooltip("Player root that gets moved and toggled. Defaults to this GameObject.")]
    [SerializeField] private GameObject playerRoot;

    [Tooltip("CharacterController is disabled while teleporting - it overwrites direct " +
             "transform writes otherwise. Defaults to one found on the root.")]
    [SerializeField] private CharacterController characterController;

    [Tooltip("Which anchor to use. Empty = the scene's default (an anchor with no id).")]
    [SerializeField] private string spawnId = string.Empty;

    [Tooltip("Fallback used when a scene has no anchor at all. Off = leave the player where it is.")]
    [SerializeField] private bool warnWhenNoAnchor = true;

    [Header("Input")]
    [Tooltip("PlayerInput to revive after the power-cycle. Defaults to one on the root.")]
    [SerializeField] private PlayerInput playerInput;

    [Tooltip("Lock the cursor the first time the player actually moves in the new scene, " +
             "rather than the instant they arrive. Movement works with a free cursor; only " +
             "mouse look needs the lock, so this keeps the cursor usable until play begins.")]
    [SerializeField] private bool lockCursorOnFirstMove = true;

    [Tooltip("Planar speed that counts as 'started moving'.")]
    [SerializeField] private float moveLockThreshold = 0.05f;

    [Tooltip("Movement read to detect the first step. Defaults to one on the root.")]
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("Look controller switched off in scenes with no anchor, so the menu camera " +
             "isn't dragged around by the mouse. Defaults to one under the root.")]
    [SerializeField] private CameraController cameraController;

    private void Awake()
    {
        if (playerRoot == null)
        {
            playerRoot = gameObject;
        }

        // Unity's overridden == is required here: a player destroyed by an earlier scene
        // change leaves a non-null C# reference that must still count as "no instance".
        if (Instance != null && Instance != this)
        {
            isDuplicate = true;

            // The travelling player wins. The scene's own copy goes, so designers can keep
            // a Player in each scene for solo testing without doubling up in a real run.
            Debug.Log($"[{nameof(PersistentPlayerSpawner)}] Duplicate player in scene " +
                      $"'{gameObject.scene.name}' destroyed; the persistent one survives.", Instance);

            Destroy(playerRoot);
            return;
        }

        Instance = this;

        // Marking the root here as well as in PlayerController keeps this component
        // self-contained; DontDestroyOnLoad is idempotent, so the repeat is harmless.
        if (playerRoot.transform.parent == null)
        {
            DontDestroyOnLoad(playerRoot);
        }
        else
        {
            Debug.LogWarning($"[{nameof(PersistentPlayerSpawner)}] '{playerRoot.name}' is not a root " +
                             "GameObject, so it cannot survive a scene load. Unparent it.", this);
        }

        if (characterController == null)
        {
            characterController = playerRoot.GetComponent<CharacterController>();
        }

        if (playerInput == null)
        {
            playerInput = playerRoot.GetComponent<PlayerInput>();
        }

        if (playerMovement == null)
        {
            playerMovement = playerRoot.GetComponent<PlayerMovement>();
        }

        if (cameraController == null)
        {
            cameraController = playerRoot.GetComponentInChildren<CameraController>(true);
        }
    }

    private void OnEnable()
    {
        if (isDuplicate)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        if (isDuplicate)
        {
            return;
        }

        // sceneLoaded does not fire for the scene the player is already sitting in at
        // startup, so the very first scene is evaluated here. Without this a player left
        // in the menu scene stays fully controllable until the first transition.
        // Applied immediately rather than deferred a frame, so the player is never
        // controllable even briefly; Update below re-asserts it against later Starts.
        MoveToSceneAnchor();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        // Only the reigning instance clears the slot - a duplicate being torn down must
        // not blank out the player that beat it.
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive loads don't replace the world the player is standing in, so moving
        // them would be wrong.
        if (mode != LoadSceneMode.Single)
        {
            return;
        }

        MoveToSceneAnchor();
    }

    /// <summary>
    /// Places the player on this scene's anchor and restarts its components.
    /// Public so a transition/cutscene can call it directly.
    /// </summary>
    public void MoveToSceneAnchor()
    {
        var anchor = FindAnchor();

        if (anchor == null)
        {
            // No anchor means this isn't a scene the player is played in - a menu, a
            // cutscene, a loading screen. Go dormant rather than letting the player walk
            // around invisibly behind the UI and steal the mouse.
            SetControlEnabled(false);
            return;
        }

        SetControlEnabled(true);
        Teleport(anchor.SpawnPosition, anchor.SpawnRotation);
    }

    /// <summary>
    /// Turns the player's control surface on or off without destroying it, so it can sit
    /// out a menu scene and still carry its state into the next gameplay scene. Disabling
    /// hands the mouse back to the UI; the GameObject itself stays alive.
    /// </summary>
    public void SetControlEnabled(bool isEnabled)
    {
        awaitingFirstMove = false;
        isDormant = !isEnabled;

        if (cameraController != null)
        {
            cameraController.enabled = isEnabled;
            cameraController.LookEnabled = isEnabled;
        }

        if (playerMovement != null)
        {
            playerMovement.ResetMotion();
            playerMovement.enabled = isEnabled;
        }

        var controller = playerRoot.GetComponent<PlayerController>();

        if (controller != null)
        {
            controller.SetInputEnabled(isEnabled);
        }

        if (playerInput != null)
        {
            if (isEnabled)
            {
                playerInput.ActivateInput();
            }
            else
            {
                playerInput.DeactivateInput();
            }
        }

        if (isEnabled)
        {
            return;
        }

        // Menu scenes drive their own UI, so the mouse goes back to the player-as-user.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>Place the player at an explicit pose, then restart its components.</summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        // Off, move, on - all in one synchronous block. The object must be re-enabled
        // before this method returns, because a disabled GameObject can't run the code
        // that would switch it back on.
        var wasActive = playerRoot.activeSelf;
        playerRoot.SetActive(false);

        // Belt and braces: even while inactive, re-enabling a CharacterController that
        // still holds the old pose can snap the player back.
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        playerRoot.transform.SetPositionAndRotation(position, rotation);

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        playerRoot.SetActive(true);

        if (!wasActive)
        {
            return;
        }

        // Deliberately not restoring input inline: the Input System finishes its own
        // re-initialisation after this callback and would overwrite anything set here.
        // That is why toggling the object by hand a moment later worked and doing it
        // in-frame did not.
        ReviveInputAsync().Forget();

        // Clear any motion queued before the load so the first frame in the new scene
        // doesn't inherit a stale stride.
        var controller = playerRoot.GetComponent<PlayerController>();

        if (controller != null)
        {
            controller.SetInputEnabled(true);
        }
    }

    /// <summary>
    /// Power-cycles <see cref="PlayerInput"/> a frame after the scene load, once the
    /// Input System has settled. This reproduces the manual off/on that revives the
    /// player, so movement and look come back without touching the Inspector.
    /// </summary>
    private async UniTaskVoid ReviveInputAsync()
    {
        if (playerInput == null)
        {
            return;
        }

        var token = this.GetCancellationTokenOnDestroy();

        // Let the load frame finish - PlayerInput re-initialises itself during it.
        await UniTask.NextFrame(token);

        if (playerInput == null)
        {
            return;
        }

        // Cycling the component (not the GameObject) is what actually re-pairs devices;
        // a frame between off and on is required or the Input System coalesces the two.
        playerInput.enabled = false;
        await UniTask.NextFrame(token);

        if (playerInput == null)
        {
            return;
        }

        playerInput.enabled = true;

        RestoreInput();
    }

    /// <summary>
    /// Brings PlayerInput back after the power-cycle. Disabling the GameObject tears the
    /// input down - the action map goes null, the actions asset is disabled and every
    /// device is unpaired - and OnEnable alone does not put that back, which leaves the
    /// player standing in the right place unable to move or look.
    /// </summary>
    private void RestoreInput()
    {
        if (playerInput == null)
        {
            return;
        }

        if (playerInput.actions != null && !playerInput.actions.enabled)
        {
            playerInput.actions.Enable();
        }

        // Re-selecting the default map re-pairs devices; without a current map the
        // Send Messages callbacks never fire.
        if (playerInput.currentActionMap == null && !string.IsNullOrEmpty(playerInput.defaultActionMap))
        {
            playerInput.SwitchCurrentActionMap(playerInput.defaultActionMap);
        }

        playerInput.ActivateInput();

        // The cursor is deliberately left free here. PlayerInputHandler only locks it in
        // Start, which does not re-run on a SetActive toggle, so Update below takes over
        // and locks on the player's first step instead of on arrival.
        awaitingFirstMove = lockCursorOnFirstMove;
    }

    private void Update()
    {
        // PlayerController.Start calls InitializePlayer, which switches input back on.
        // Script execution order decides whether that lands before or after Start here,
        // so dormancy is re-asserted rather than trusted to win the race.
        if (isDormant)
        {
            var controller = playerRoot.GetComponent<PlayerController>();

            if (controller != null && controller.IsInputEnabled)
            {
                controller.SetInputEnabled(false);
            }

            return;
        }

        if (!awaitingFirstMove || playerMovement == null)
        {
            return;
        }

        if (playerMovement.CurrentPlanarSpeed <= moveLockThreshold)
        {
            return;
        }

        // First step taken - hand the mouse over to looking. Walking never needed the
        // lock, so nothing was blocked while we waited.
        awaitingFirstMove = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private PlayerSpawnAnchor FindAnchor()
    {
        var anchors = FindObjectsByType<PlayerSpawnAnchor>(FindObjectsSortMode.None);

        if (anchors == null || anchors.Length == 0)
        {
            return null;
        }

        // Named anchor wins when a spawnId is set; otherwise take the scene default
        // (an anchor with no id), falling back to whatever is there.
        foreach (var anchor in anchors)
        {
            if (!string.IsNullOrEmpty(spawnId))
            {
                if (anchor.SpawnId == spawnId)
                {
                    return anchor;
                }

                continue;
            }

            if (string.IsNullOrEmpty(anchor.SpawnId))
            {
                return anchor;
            }
        }

        return anchors[0];
    }

}
