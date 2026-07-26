using DG.Tweening;
using UnityEngine;

/// <summary>
/// A looping emitter that only costs anything while the player is in its room: the room
/// ambience itself, a generator's hum, the verification computer's cooling fan, the desk fan
/// the misfire powers up.
///
/// Two independent gates decide whether it is audible, and both must be open:
/// <see cref="SetZoneActive"/> is the room's answer ("the player is in here"), and
/// <see cref="SetGameplayActive"/> is the object's ("this machine is running"). A hum with
/// only the second gate open would keep decoding audio in a room three doors away, which on
/// WebGL is exactly the cost this component exists to avoid - when the zone closes the
/// AudioSource is stopped and disabled outright, not merely turned down.
/// </summary>
public class RoomLoopSource : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Leave empty to use the AudioSource on this object, or to have one added.")]
    [SerializeField] private AudioSource source;

    [SerializeField] private AudioClip loopClip;

    [Header("Level")]
    [Range(0f, 1f)]
    [SerializeField] private float targetVolume = 0.5f;

    [SerializeField] private float fadeInSeconds = 0.5f;

    [SerializeField] private float fadeOutSeconds = 0.35f;

    [Header("Falloff")]
    [SerializeField] private bool spatial = true;

    [SerializeField] private float minDistance = 2f;

    [SerializeField] private float maxDistance = 14f;

    [Header("Behaviour")]
    [Tooltip("On for room ambience, which runs the whole time the player is inside. Off for a "
             + "machine that also has to be switched on by the gameplay that owns it.")]
    [SerializeField] private bool runsWhileRoomIsOccupied = true;

    [Tooltip("Starts the clip at a random offset so several copies of the same hum do not "
             + "phase together into one loud sound.")]
    [SerializeField] private bool randomiseStartTime = true;

    private bool isZoneActive;
    private bool isGameplayActive;
    private Tween volumeFade;

    /// <summary>True when both gates are open and the loop should be audible.</summary>
    public bool ShouldPlay => isZoneActive && (runsWhileRoomIsOccupied || isGameplayActive);

    private void Awake()
    {
        if (source == null)
        {
            source = GetComponent<AudioSource>();
        }

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.clip = loopClip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        source.spatialBlend = spatial ? 1f : 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.enabled = false;

        // A loop authored outside any zone - a single-room test scene, or a corridor prop -
        // would otherwise never be switched on by anything.
        isZoneActive = GetComponentInParent<RoomAudioZone>() == null;
    }

    private void OnDisable()
    {
        volumeFade?.Kill();
        volumeFade = null;

        if (source != null)
        {
            source.Stop();
            source.enabled = false;
        }
    }

    private void OnDestroy()
    {
        volumeFade?.Kill();
        volumeFade = null;
    }

    private void Start()
    {
        // Deferred to Start so the owning zone has finished registering in its own Awake.
        Apply(instant: true);
    }

    /// <summary>Called by the room's <see cref="RoomAudioZone"/> as the player enters and leaves.</summary>
    public void SetZoneActive(bool isActive)
    {
        if (isZoneActive == isActive)
        {
            return;
        }

        isZoneActive = isActive;
        Apply(instant: false);
    }

    /// <summary>
    /// Called by whatever owns the machine - a generator reaching full power, a prop being
    /// powered by the misfire. Ignored by loops marked as room ambience.
    /// </summary>
    public void SetGameplayActive(bool isActive)
    {
        if (isGameplayActive == isActive)
        {
            return;
        }

        isGameplayActive = isActive;
        Apply(instant: false);
    }

    /// <summary>Swaps the looping clip mid-run, e.g. a generator's spin-up giving way to its hum.</summary>
    public void SetClip(AudioClip clip)
    {
        if (loopClip == clip)
        {
            return;
        }

        loopClip = clip;

        if (source == null)
        {
            return;
        }

        var wasPlaying = source.isPlaying;
        source.clip = clip;

        if (wasPlaying && clip != null)
        {
            source.Play();
        }
    }

    private void Apply(bool instant)
    {
        if (source == null || loopClip == null)
        {
            return;
        }

        volumeFade?.Kill();
        volumeFade = null;

        if (ShouldPlay)
        {
            source.enabled = true;

            if (!source.isPlaying)
            {
                source.volume = 0f;
                source.time = randomiseStartTime ? Random.Range(0f, Mathf.Max(0f, loopClip.length - 0.1f)) : 0f;
                source.Play();
            }

            FadeTo(LiveTargetVolume(), instant ? 0f : fadeInSeconds, stopWhenDone: false);
            return;
        }

        FadeTo(0f, instant ? 0f : fadeOutSeconds, stopWhenDone: true);
    }

    private void FadeTo(float volume, float duration, bool stopWhenDone)
    {
        if (duration <= 0f)
        {
            source.volume = volume;

            if (stopWhenDone)
            {
                Silence();
            }

            return;
        }

        volumeFade = DOTween
            .To(() => source.volume, value => source.volume = value, volume, duration)
            .SetUpdate(true);

        if (stopWhenDone)
        {
            volumeFade.OnComplete(Silence);
        }
    }

    /// <summary>
    /// Disabling the component, not just stopping it, is the point: a disabled AudioSource is
    /// off the audio thread entirely instead of holding a decoder for a room nobody is in.
    /// </summary>
    private void Silence()
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.enabled = false;
    }

    private float LiveTargetVolume()
    {
        var mix = AudioManager.Instance == null ? 1f : AudioManager.Instance.SfxVolume;
        return targetVolume * mix;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
    }
#endif
}
