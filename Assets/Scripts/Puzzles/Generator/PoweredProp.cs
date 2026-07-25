using System;
using UnityEngine;

/// <summary>
/// Shows a prop's power state through colour while keeping it visible.
///
/// Switching the GameObject off instead makes the object vanish from the room, which
/// reads as missing geometry rather than "this equipment is not running" - the bunker's
/// desk fan and verification computer are physically there whether or not they have power.
/// </summary>
public class PoweredProp : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;

    [SerializeField] private Color unpoweredColor = new Color(0.24f, 0.24f, 0.26f);

    [SerializeField] private Color poweredColor = new Color(1f, 0.82f, 0.3f);

    [Tooltip("Optional extra object switched on only while powered, e.g. a glow quad.")]
    [SerializeField] private GameObject poweredOnlyVisual;

    [SerializeField] private bool startPowered;

    private MaterialPropertyBlock propertyBlock;

    public bool IsPowered { get; private set; }

    /// <summary>
    /// Raised only when the power state actually changes, for audio and VFX. The misfire calls
    /// <see cref="SetPowered"/> with false across every prop on each reset, and firing on those
    /// no-ops would power four props down audibly when none of them were on.
    ///
    /// Nothing is raised for the authored starting state, which is applied in Awake before any
    /// listener has subscribed - read <see cref="IsPowered"/> in OnEnable to seed from it.
    /// </summary>
    public event Action<bool> OnPowerChanged;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        propertyBlock = new MaterialPropertyBlock();
        SetPowered(startPowered);
    }

    /// <summary>Tints the prop to its powered or unpowered colour. Never hides the object.</summary>
    public void SetPowered(bool isPowered)
    {
        var hasChanged = IsPowered != isPowered;

        IsPowered = isPowered;

        if (poweredOnlyVisual != null)
        {
            poweredOnlyVisual.SetActive(isPowered);
        }

        if (targetRenderer != null)
        {
            var color = isPowered ? poweredColor : unpoweredColor;

            // MaterialPropertyBlock rather than .material, which would instantiate and leak
            // a fresh material per prop.
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        if (hasChanged)
        {
            OnPowerChanged?.Invoke(isPowered);
        }
    }
}
