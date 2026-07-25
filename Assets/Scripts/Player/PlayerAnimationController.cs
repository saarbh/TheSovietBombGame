using UnityEngine;

/// <summary>
/// Drives the player Animator through a three-state machine - Idle, Moving,
/// Interacting - kept deliberately small so it can sit next to the composed
/// <see cref="PlayerController"/> without owning any input or physics itself.
///
/// Tuned for <c>PetrovAnimationController</c>, whose locomotion is entirely
/// <c>Speed</c>-driven (Idle&lt;-&gt;Walking transitions on Speed &gt; 0.1) and whose
/// interact state is the <c>TakingItem</c> trigger. Locomotion speed is read from
/// <see cref="PlayerMovement"/> each frame; Interacting is a one-shot entered via
/// <see cref="TriggerInteract"/> that holds for <see cref="interactDuration"/> (or
/// until <see cref="NotifyInteractFinished"/> fires from an animation event).
///
/// The moving bool is optional: leave <see cref="movingParam"/> empty for
/// Speed-only controllers like Petrov, or set it for controllers that gate
/// Idle/Walking on a bool instead of a float threshold.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    public enum PlayerAnimState
    {
        Idle,
        Moving,
        Interacting,
    }

    [Header("References")]
    [SerializeField] private Animator animator;

    [Tooltip("Source of planar speed. Leave empty to drive Idle/Moving via SetSpeed() instead.")]
    [SerializeField] private PlayerMovement movement;

    [Header("Animator Parameters")]
    [Tooltip("Float parameter. Petrov: \"Speed\".")]
    [SerializeField] private string speedParam = "Speed";

    [Tooltip("Optional bool parameter. Leave empty for Speed-driven controllers like Petrov.")]
    [SerializeField] private string movingParam = "";

    [Tooltip("Trigger parameter for the interact one-shot. Petrov: \"TakingItem\".")]
    [SerializeField] private string interactParam = "TakingItem";

    [Tooltip("Optional bool parameter for a seated pose. Petrov: \"Sitting\". Leave empty to disable.")]
    [SerializeField] private string sittingParam = "Sitting";

    [Header("Tuning")]
    [Tooltip("Planar speed (m/s) at or below which the player counts as idle. Match the controller's Speed threshold (Petrov: 0.1).")]
    [SerializeField] private float moveThreshold = 0.1f;

    [Tooltip("How long the Interacting state holds before returning to locomotion. Match the " +
             "interact clip length, or call NotifyInteractFinished from an animation event for a frame-accurate exit.")]
    [SerializeField] private float interactDuration = 0.6f;

    private int speedHash;
    private int movingHash;
    private int interactHash;
    private int sittingHash;

    private bool hasMovingParam;
    private bool hasSittingParam;

    private PlayerAnimState currentState = PlayerAnimState.Idle;
    private float interactTimer;
    private float currentSpeed;
    private bool isSitting;

    /// <summary>Current animation state. Read-only for other systems (audio, IK, head bob).</summary>
    public PlayerAnimState CurrentState => currentState;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        // Cache parameter hashes once - StringToHash every frame is wasteful.
        speedHash = Animator.StringToHash(speedParam);
        interactHash = Animator.StringToHash(interactParam);

        hasMovingParam = !string.IsNullOrEmpty(movingParam);
        if (hasMovingParam)
        {
            movingHash = Animator.StringToHash(movingParam);
        }

        hasSittingParam = !string.IsNullOrEmpty(sittingParam);
        if (hasSittingParam)
        {
            sittingHash = Animator.StringToHash(sittingParam);
        }
    }

    private void Update()
    {
        if (movement != null)
        {
            currentSpeed = movement.CurrentPlanarSpeed;
        }

        if (currentState == PlayerAnimState.Interacting)
        {
            TickInteract();
        }
        else
        {
            UpdateLocomotion();
        }

        // Speed drives the controller's Idle<->Walking transitions, so push it every frame.
        if (animator != null)
        {
            animator.SetFloat(speedHash, currentSpeed);
        }
    }

    /// <summary>Feed planar speed manually when no <see cref="PlayerMovement"/> is wired.</summary>
    public void SetSpeed(float planarSpeed)
    {
        currentSpeed = Mathf.Max(0f, planarSpeed);
    }

    /// <summary>
    /// Enter the Interacting state and fire the interact trigger. Call from wherever
    /// an interaction is confirmed, e.g. inside PlayerController.OnInteractInput.
    /// </summary>
    public void TriggerInteract()
    {
        currentState = PlayerAnimState.Interacting;
        interactTimer = interactDuration;

        if (animator != null)
        {
            if (hasMovingParam)
            {
                animator.SetBool(movingHash, false);
            }

            animator.SetTrigger(interactHash);
        }
    }

    /// <summary>
    /// End the Interacting state early. Hook this to an animation event on the last
    /// frame of the interact clip for a frame-accurate exit instead of the timer.
    /// </summary>
    public void NotifyInteractFinished()
    {
        if (currentState != PlayerAnimState.Interacting)
        {
            return;
        }

        interactTimer = 0f;
        TransitionToLocomotion();
    }

    /// <summary>Toggle the optional seated pose (Petrov "Sitting" bool). No-op if unconfigured.</summary>
    public void SetSitting(bool sitting)
    {
        isSitting = sitting;

        if (animator != null && hasSittingParam)
        {
            animator.SetBool(sittingHash, sitting);
        }
    }

    private void TickInteract()
    {
        interactTimer -= Time.deltaTime;
        if (interactTimer <= 0f)
        {
            TransitionToLocomotion();
        }
    }

    private void UpdateLocomotion()
    {
        var moving = currentSpeed > moveThreshold;
        var next = moving ? PlayerAnimState.Moving : PlayerAnimState.Idle;

        if (next == currentState)
        {
            return;
        }

        currentState = next;

        if (animator != null && hasMovingParam)
        {
            animator.SetBool(movingHash, moving);
        }
    }

    private void TransitionToLocomotion()
    {
        // Recompute from current speed so we land in the right state the instant the interact ends.
        currentState = currentSpeed > moveThreshold ? PlayerAnimState.Moving : PlayerAnimState.Idle;

        if (animator != null && hasMovingParam)
        {
            animator.SetBool(movingHash, currentState == PlayerAnimState.Moving);
        }
    }
}
