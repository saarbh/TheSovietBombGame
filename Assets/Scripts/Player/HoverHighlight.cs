using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Fresnel rim highlight for an interactable. Appends an additive outline
/// material (TheHotWar/FresnelHighlight) to the object's renderers and fades its
/// "_Highlight" property in while the player is looking at this object.
///
/// Subscribes to <see cref="PlayerInteraction.OnInteractableTargetChanged"/> and
/// lights up only when the reported target is this object's interactable, mirroring
/// the crosshair/prompt UI subscription pattern rather than polling every frame.
/// </summary>
public class HoverHighlight : MonoBehaviour
{
    private static readonly int HighlightId = Shader.PropertyToID("_Highlight");

    [Header("Outline")]
    [Tooltip("Material using the TheHotWar/FresnelHighlight shader.")]
    [SerializeField] private Material outlineMaterial;

    [Tooltip("Renderers to outline. Empty = every renderer under this object, found on Awake.")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Fade")]
    [Tooltip("Seconds to fade the rim in/out on hover enter/exit.")]
    [SerializeField] private float fadeDuration = 0.12f;

    [Header("Hover Source")]
    [Tooltip("Interaction system reporting the hovered target. Empty = found in scene on enable.")]
    [SerializeField] private PlayerInteraction interaction;

    private readonly List<Material> outlineInstances = new List<Material>();
    private IInteractable self;
    private Tween fadeTween;
    private float highlight;

    private void Awake()
    {
        // The interactable usually lives on this object or an ancestor (door/phone root).
        self = GetComponentInParent<IInteractable>();

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
        }

        BuildOutlineInstances();
        ApplyHighlight(0f);
    }

    private void OnEnable()
    {
        if (interaction == null)
        {
            interaction = FindFirstObjectByType<PlayerInteraction>();
        }

        if (interaction != null)
        {
            interaction.OnInteractableTargetChanged += HandleTargetChanged;
        }
    }

    private void OnDisable()
    {
        if (interaction != null)
        {
            interaction.OnInteractableTargetChanged -= HandleTargetChanged;
        }

        if (fadeTween != null)
        {
            fadeTween.Kill();
            fadeTween = null;
        }
    }

    private void OnDestroy()
    {
        // Material instances were created at runtime, so they are ours to destroy.
        foreach (var instance in outlineInstances)
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }
    }

    /// <summary>Fade the rim in (true) or out (false). Public so other systems can drive it.</summary>
    public void SetHighlighted(bool highlighted)
    {
        if (outlineInstances.Count == 0)
        {
            return;
        }

        if (fadeTween != null)
        {
            fadeTween.Kill();
        }

        var target = highlighted ? 1f : 0f;
        fadeTween = DOTween.To(() => highlight, ApplyHighlight, target, fadeDuration)
            .SetUpdate(true);
    }

    private void HandleTargetChanged(IInteractable target)
    {
        // IInteractable is a plain C# interface reference here, so ReferenceEquals is a
        // real identity check - no UnityEngine.Object fake-null semantics are involved.
        var isMe = self != null && ReferenceEquals(target, self);
        SetHighlighted(isMe);
    }

    private void BuildOutlineInstances()
    {
        if (outlineMaterial == null)
        {
            Debug.LogWarning($"{nameof(HoverHighlight)} on '{name}' has no outline material assigned.", this);
            return;
        }

        foreach (var renderer in targetRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            // Append a unique additive material so the base look is untouched and
            // no shared asset is mutated - only this renderer's material list grows.
            var materials = new List<Material>(renderer.sharedMaterials);
            var instance = new Material(outlineMaterial);
            materials.Add(instance);
            renderer.sharedMaterials = materials.ToArray();
            outlineInstances.Add(instance);
        }
    }

    private void ApplyHighlight(float value)
    {
        highlight = value;

        foreach (var instance in outlineInstances)
        {
            if (instance != null)
            {
                instance.SetFloat(HighlightId, value);
            }
        }
    }
}
