using UnityEngine;

/// <summary>
/// Declares what a single interactable sounds like when it is used. Purely a tag: it holds no
/// behaviour and subscribes to nothing, because the player's <see cref="InteractionAudio"/>
/// is what actually plays the sound.
///
/// Attaching one is optional. An interactable with no tag gets the generic press from whatever
/// bank applies to it, which is what makes a newly built room audible before anyone has
/// authored a single clip for it.
/// </summary>
[DisallowMultipleComponent]
public class InteractionSfx : MonoBehaviour
{
    [Tooltip("Sound this object makes when used. Set to None for an object whose audio is "
             + "driven by a dedicated hook, so the generic press does not double up on it.")]
    [SerializeField] private SfxId sfxOnInteract = SfxId.InteractGeneric;

    [Tooltip("Optional. Played instead of anything in the banks - for a one-off sound not worth "
             + "an SfxId of its own.")]
    [SerializeField] private AudioClip clipOverride;

    [Range(0f, 1f)]
    [SerializeField] private float overrideVolume = 1f;

    [Tooltip("Optional. Where the sound comes from. Defaults to this object.")]
    [SerializeField] private Transform emitFrom;

    public SfxId SfxOnInteract => sfxOnInteract;

    public AudioClip ClipOverride => clipOverride;

    public float OverrideVolume => overrideVolume;

    public Vector3 EmitPosition => emitFrom == null ? transform.position : emitFrom.position;
}
