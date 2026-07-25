using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sub-controller: raycasts from the camera each frame looking for an
/// <see cref="IInteractable"/> and reports target changes so the crosshair and
/// prompt UI can react. Does not decide what interacting means - it forwards to
/// the target.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 4f;
    [SerializeField] private LayerMask interactableMask = ~0;

    [Tooltip("Ray origin. Defaults to Camera.main if left empty.")]
    [SerializeField] private Transform rayOrigin;

    [Tooltip("Should the ray stop at trigger colliders? Doors using trigger volumes need this on.")]
    [SerializeField] private bool detectTriggers = true;

    [Tooltip("Crosshair GameObject shown only while aiming at an interactable (leave empty to disable this behaviour).")]
    [SerializeField] private GameObject interactCrosshair;

    [Header("Highlighting Settings")]
    [Tooltip("Overlay material added to the highlighted object while aimed at it " +
             "(e.g. Assets/Materials/UI_Mat/FresnelHighlight). Removed automatically on unhover.")]
    [SerializeField] private Material highlightOverlayMaterial;

    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private IInteractable currentTarget;
    private Collider lastHitCollider;

    /// <summary>The interactable currently under the crosshair, or null.</summary>
    public IInteractable CurrentTarget => currentTarget;

    /// <summary>
    /// Fires only when the target actually changes (including to null), so
    /// listeners don't rebuild UI every frame.
    /// </summary>
    public event Action<IInteractable> OnInteractableTargetChanged;

    private void Awake()
    {
        EnsureRayOrigin();

        // Hidden until the player actually aims at something interactable.
        if (interactCrosshair != null)
        {
            interactCrosshair.SetActive(false);
        }
    }

    private void OnDisable()
    {
        RemoveHighlight();
    }

    private void EnsureRayOrigin()
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
        EnsureRayOrigin();
        SetTarget(Raycast());
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
        SetTarget(null);
    }

    /// <summary>
    /// Central point for changing the target: fires the change event and shows/hides
    /// the interact crosshair. No-op when the target hasn't actually changed.
    /// </summary>
    private void SetTarget(IInteractable target)
    {
        if (ReferenceEquals(target, currentTarget))
        {
            return;
        }

        RemoveHighlight();

        currentTarget = target;
        Debug.Log($"[PlayerInteraction] Active Target Changed To: {(currentTarget != null ? currentTarget.GetType().Name : "None")}");

        if (currentTarget != null && currentTarget is MonoBehaviour mb)
        {
            ApplyHighlight(FindHighlightTarget(mb.gameObject));
        }

        if (interactCrosshair != null)
        {
            interactCrosshair.SetActive(currentTarget != null);
        }

        OnInteractableTargetChanged?.Invoke(currentTarget);
    }

    private GameObject FindHighlightTarget(GameObject obj)
    {
        if (obj == null)
        {
            return null;
        }

        // If the object itself has renderers, highlight it.
        if (obj.GetComponentInChildren<Renderer>() != null)
        {
            return obj;
        }

        // Otherwise, if the parent has renderers, highlight the parent.
        if (obj.transform.parent != null && obj.transform.parent.GetComponentInChildren<Renderer>() != null)
        {
            return obj.transform.parent.gameObject;
        }

        return obj;
    }

    private void ApplyHighlight(GameObject obj)
    {
        if (obj == null || highlightOverlayMaterial == null)
        {
            return;
        }

        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null)
            {
                continue;
            }

            if (!originalMaterials.ContainsKey(r))
            {
                originalMaterials[r] = r.sharedMaterials;
            }

            // Append ONLY the Fresnel overlay as an extra submaterial - the object's own
            // materials are left untouched (no colour/emission change). RemoveHighlight
            // restores the original sharedMaterials, which drops this overlay again.
            var current = r.sharedMaterials;
            var withOverlay = new Material[current.Length + 1];
            Array.Copy(current, withOverlay, current.Length);
            withOverlay[current.Length] = highlightOverlayMaterial;
            r.sharedMaterials = withOverlay;
        }
    }

    private void RemoveHighlight()
    {
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.sharedMaterials = kvp.Value;
            }
        }

        originalMaterials.Clear();
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
            if (lastHitCollider != null)
            {
                Debug.Log($"[PlayerInteraction] Raycast lost target. Last hit: {lastHitCollider.name}");
                lastHitCollider = null;
            }
            return null;
        }

        if (hit.collider != lastHitCollider)
        {
            lastHitCollider = hit.collider;
            
            // Check parent first, then children
            var parentComponents = hit.collider.GetComponentsInParent<MonoBehaviour>();
            IInteractable foundInteractable = null;
            foreach (var mb in parentComponents)
            {
                if (mb != null && mb.enabled && mb is IInteractable interactable)
                {
                    foundInteractable = interactable;
                    break;
                }
            }

            if (foundInteractable == null)
            {
                var childComponents = hit.collider.GetComponentsInChildren<MonoBehaviour>();
                foreach (var mb in childComponents)
                {
                    if (mb != null && mb.enabled && mb is IInteractable interactable)
                    {
                        foundInteractable = interactable;
                        break;
                    }
                }
            }

            Debug.Log($"[PlayerInteraction] Raycast hit: {hit.collider.name} (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}). Interactable found: {(foundInteractable != null ? foundInteractable.GetType().Name : "None")}");
        }

        // Try parent first
        var componentsForInteraction = hit.collider.GetComponentsInParent<MonoBehaviour>();
        foreach (var mb in componentsForInteraction)
        {
            if (mb != null && mb.enabled && mb is IInteractable interactable)
            {
                return interactable;
            }
        }

        // If parent failed, try children
        var childComponentsForInteraction = hit.collider.GetComponentsInChildren<MonoBehaviour>();
        foreach (var mb in childComponentsForInteraction)
        {
            if (mb != null && mb.enabled && mb is IInteractable interactable)
            {
                return interactable;
            }
        }

        return null;
    }

    private void OnGUI()
    {
        if (currentTarget == null)
        {
            return;
        }

        var prompt = currentTarget.GetPrompt();

        if (string.IsNullOrEmpty(prompt))
        {
            return;
        }

        var style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 14;
        style.richText = true;

        var width = 300f;
        var height = 30f;
        // Position it slightly below the center of the screen
        var rect = new Rect((Screen.width - width) / 2f, (Screen.height / 2f) + 25f, width, height);

        GUI.Box(rect, prompt, style);
    }

    private void OnDrawGizmosSelected()
    {
        var origin = rayOrigin != null ? rayOrigin : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin.position, origin.position + (origin.forward * interactDistance));
    }
}
