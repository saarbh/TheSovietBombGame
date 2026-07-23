using System;
using UnityEngine;

/// <summary>
/// Sub-controller: raycasts from the camera each frame looking for an
/// <see cref="IInteractable"/> and reports target changes so the crosshair and
/// prompt UI can react. Does not decide what interacting means - it forwards to
/// the target.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 2.5f;
    [SerializeField] private LayerMask interactableMask = ~0;

    [Tooltip("Ray origin. Defaults to Camera.main if left empty.")]
    [SerializeField] private Transform rayOrigin;

    [Tooltip("Should the ray stop at trigger colliders? Doors using trigger volumes need this on.")]
    [SerializeField] private bool detectTriggers = true;

    private IInteractable currentTarget;

    /// <summary>The interactable currently under the crosshair, or null.</summary>
    public IInteractable CurrentTarget => currentTarget;

    /// <summary>
    /// Fires only when the target actually changes (including to null), so
    /// listeners don't rebuild UI every frame.
    /// </summary>
    public event Action<IInteractable> OnInteractableTargetChanged;

    private void Awake()
    {
        if (rayOrigin == null && Camera.main != null)
        {
            rayOrigin = Camera.main.transform;
        }
    }

    /// <summary>
    /// Re-evaluates what the player is looking at. Call once per frame from the
    /// owning <see cref="PlayerController"/>.
    /// </summary>
    public void DetectInteractable()
    {
        var found = Raycast();

        if (ReferenceEquals(found, currentTarget))
        {
            return;
        }

        currentTarget = found;
        OnInteractableTargetChanged?.Invoke(currentTarget);
    }

    /// <summary>
    /// Runs the current target's interaction, if any. Safe to call with no target.
    /// </summary>
    public void ExecuteInteraction(PlayerController player)
    {
        currentTarget?.Interact(player);
    }

    /// <summary>Prompt for the current target, or empty when nothing is targeted.</summary>
    public string GetCurrentPrompt()
    {
        return currentTarget != null ? currentTarget.GetPrompt() : string.Empty;
    }

    /// <summary>
    /// Drops the current target and notifies listeners - used when input is
    /// disabled so a stale prompt doesn't linger on screen.
    /// </summary>
    public void ClearTarget()
    {
        if (currentTarget == null)
        {
            return;
        }

        currentTarget = null;
        OnInteractableTargetChanged?.Invoke(null);
    }

    private IInteractable Raycast()
    {
        if (rayOrigin == null)
        {
            return null;
        }

        var interaction = detectTriggers
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;

        if (!Physics.Raycast(
                rayOrigin.position,
                rayOrigin.forward,
                out var hit,
                interactDistance,
                interactableMask,
                interaction))
        {
            return null;
        }

        // GetComponentInParent so a collider on a child mesh still resolves to the
        // interactable sitting on the root of the door/phone prefab.
        return hit.collider.GetComponentInParent<IInteractable>();
    }

    private void OnDrawGizmosSelected()
    {
        var origin = rayOrigin != null ? rayOrigin : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin.position, origin.position + (origin.forward * interactDistance));
    }
}
