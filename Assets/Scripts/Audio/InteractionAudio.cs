using UnityEngine;

/// <summary>
/// Gives every interactable in the game a sound, from one place on the player.
///
/// This is the "default SFX when a room does not have its own" rule made concrete. Resolution
/// runs outward from the most specific source to the least:
///
///   the object's <see cref="InteractionSfx"/> clip override
///   -> that component's <see cref="SfxId"/> in the room's bank
///   -> the same id in the game default bank
///   -> the generic press in whichever of those declares it
///
/// So a room ships silent-by-default-but-audible: nothing has to be wired for a new lever to
/// click, and wiring one <see cref="InteractionSfx"/> is enough to make it click differently.
/// </summary>
public class InteractionAudio : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PlayerInteraction interactionSystem;

    [Header("Sounds")]
    [Tooltip("Used when the interactable declares nothing of its own.")]
    [SerializeField] private SfxId fallbackInteractSfx = SfxId.InteractGeneric;

    [Tooltip("Soft tick as the crosshair lands on something usable. None to disable.")]
    [SerializeField] private SfxId targetAcquiredSfx = SfxId.TargetAcquired;

    // Resolved once per target change rather than per press: the walk up the hierarchy for the
    // room's bank is not something to repeat on every click.
    private InteractionSfx targetSfx;
    private SfxBank targetBank;
    private Transform targetTransform;

    private void Awake()
    {
        if (interactionSystem == null)
        {
            interactionSystem = GetComponentInParent<PlayerInteraction>();
        }

        if (interactionSystem == null)
        {
            interactionSystem = GetComponentInChildren<PlayerInteraction>();
        }
    }

    private void OnEnable()
    {
        if (interactionSystem == null)
        {
            Debug.LogError("[Audio] InteractionAudio has no PlayerInteraction; interactions will be silent.", this);
            return;
        }

        interactionSystem.OnInteractableTargetChanged += HandleTargetChanged;
        interactionSystem.OnInteractionExecuted += HandleInteractionExecuted;
    }

    private void OnDisable()
    {
        if (interactionSystem == null)
        {
            return;
        }

        interactionSystem.OnInteractableTargetChanged -= HandleTargetChanged;
        interactionSystem.OnInteractionExecuted -= HandleInteractionExecuted;
    }

    private void HandleTargetChanged(IInteractable target)
    {
        ResolveTarget(target);

        if (targetAcquiredSfx == SfxId.None || targetTransform == null)
        {
            return;
        }

        AudioManager.PlayAt(targetAcquiredSfx, targetTransform.position, targetBank);
    }

    private void HandleInteractionExecuted(IInteractable used)
    {
        // The interaction may already have retargeted us (a sealed panel drops its prompt), so
        // the cached target is only trusted when it is still the thing that was used.
        if (!ReferenceEquals(used, interactionSystem.CurrentTarget))
        {
            ResolveTarget(used);
        }

        if (targetTransform == null)
        {
            return;
        }

        if (targetSfx == null)
        {
            AudioManager.PlayAt(fallbackInteractSfx, targetTransform.position, targetBank);
            return;
        }

        if (targetSfx.ClipOverride != null)
        {
            AudioManager.PlayClipAt(targetSfx.ClipOverride, targetSfx.EmitPosition, targetSfx.OverrideVolume);
            return;
        }

        if (targetSfx.SfxOnInteract == SfxId.None)
        {
            // Explicitly silent: the object has a dedicated hook that will make its own noise.
            return;
        }

        AudioManager.PlayAt(targetSfx.SfxOnInteract, targetSfx.EmitPosition, targetBank);
    }

    private void ResolveTarget(IInteractable target)
    {
        // Every implementor in the game is a MonoBehaviour, so this is how the interface gets
        // back to a scene object without widening IInteractable itself.
        var targetComponent = target as Component;

        if (targetComponent == null)
        {
            targetSfx = null;
            targetBank = null;
            targetTransform = null;
            return;
        }

        targetSfx = targetComponent.GetComponentInParent<InteractionSfx>();
        targetBank = RoomAudioZone.BankFor(targetComponent);
        targetTransform = targetComponent.transform;
    }
}
