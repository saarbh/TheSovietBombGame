using DG.Tweening;
using UnityEngine;

/// <summary>
/// The game's one persistent audio service and, per the project standard, the only sanctioned
/// <c>DontDestroyOnLoad</c> singleton.
///
/// Two channels, not the spec's three: the ambient bed was cut, so <c>BGMType</c> has no
/// content and no owner. What survives is a single music track that plays for the whole run
/// and a pool of SFX voices.
///
/// Every call site goes through the static helpers, which no-op when no manager is in the
/// scene. That keeps the single-room test setup working exactly like <c>GameManager</c>:
/// the room still resolves and logs, the sound just has nowhere to go.
/// </summary>
[DefaultExecutionOrder(-200)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    [Tooltip("Looped for the whole run. Korobeiniki - there is no ambient bed behind it.")]
    [SerializeField] private AudioClip musicTrack;

    [SerializeField] private AudioSource musicSource;

    [SerializeField] private bool playMusicOnAwake = true;

    [SerializeField] private float musicFadeSeconds = 2f;

    [Header("SFX")]
    [Tooltip("Fallback clips for every room that does not declare its own.")]
    [SerializeField] private SfxBank defaultBank;

    [Tooltip("Non-positional channel: UI, the watch, anything the player carries.")]
    [SerializeField] private AudioSource uiSource;

    [Tooltip("Simultaneous positional sounds. Beyond this the oldest voice is stolen.")]
    [SerializeField] private int positionalVoiceCount = 12;

    [Header("Positional falloff")]
    [SerializeField] private float voiceMinDistance = 2.5f;

    [SerializeField] private float voiceMaxDistance = 20f;

    [Header("Mix")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.3f;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    private AudioSource[] voices;
    private int nextVoiceIndex;
    private Tween musicFade;

    /// <summary>The fallback bank, for components that resolve clips themselves.</summary>
    public SfxBank DefaultBank => defaultBank;

    /// <summary>Master-scaled SFX volume, for loops that manage their own AudioSource.</summary>
    public float SfxVolume => masterVolume * sfxVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A second manager arriving with a reloaded scene: the first one owns the music,
            // and letting this one live would double every sound.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureSources();
        BuildVoicePool();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        musicFade?.Kill();
        musicFade = null;
    }

    private void Start()
    {
        if (playMusicOnAwake)
        {
            PlayMusic(musicTrack);
        }
    }

    // --- Static entry points -----------------------------------------------------------------
    // Null-guarded so a scene without an AudioManager is silent rather than broken.

    /// <summary>Plays a positional one-shot, preferring the room's bank over the default.</summary>
    public static void PlayAt(SfxId id, Vector3 position, SfxBank roomBank)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.PlaySfxAt(id, position, roomBank);
    }

    /// <summary>Plays a non-positional one-shot - UI, the watch, the phone at the player's ear.</summary>
    public static void Play2D(SfxId id, SfxBank roomBank)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.PlaySfx2D(id, roomBank);
    }

    /// <summary>Plays an explicit clip positionally, for components that pick their own.</summary>
    public static void PlayClipAt(AudioClip clip, Vector3 position, float volume)
    {
        if (Instance == null || clip == null)
        {
            return;
        }

        Instance.PlayVoice(clip, position, volume, 1f);
    }

    // --- Instance API ------------------------------------------------------------------------

    public void PlaySfxAt(SfxId id, Vector3 position, SfxBank roomBank)
    {
        if (!Resolve(id, roomBank, out var clip, out var volume, out var pitch))
        {
            return;
        }

        PlayVoice(clip, position, volume, pitch);
    }

    public void PlaySfx2D(SfxId id, SfxBank roomBank)
    {
        if (!Resolve(id, roomBank, out var clip, out var volume, out var pitch))
        {
            return;
        }

        if (uiSource == null)
        {
            return;
        }

        uiSource.pitch = pitch;
        uiSource.PlayOneShot(clip, volume * SfxVolume);
    }

    /// <summary>Swaps the looping track. Cross-fades rather than cutting.</summary>
    public void PlayMusic(AudioClip track)
    {
        if (musicSource == null || track == null)
        {
            return;
        }

        musicFade?.Kill();

        musicSource.clip = track;
        musicSource.loop = true;
        musicSource.volume = 0f;
        musicSource.Play();

        // SetUpdate(true) so the fade still runs if the game is paused over a modal - a track
        // frozen half-faded is worse than no fade at all.
        musicFade = DOTween
            .To(() => musicSource.volume, value => musicSource.volume = value, masterVolume * musicVolume, musicFadeSeconds)
            .SetUpdate(true);
    }

    public void StopMusic()
    {
        if (musicSource == null)
        {
            return;
        }

        musicFade?.Kill();

        musicFade = DOTween
            .To(() => musicSource.volume, value => musicSource.volume = value, 0f, musicFadeSeconds)
            .SetUpdate(true)
            .OnComplete(musicSource.Stop);
    }

    /// <summary>Re-applies the mix to live sources. Call after changing a volume at runtime.</summary>
    public void ApplyMix()
    {
        if (musicSource != null && musicFade == null)
        {
            musicSource.volume = masterVolume * musicVolume;
        }

        if (uiSource != null)
        {
            uiSource.volume = 1f;
        }
    }

    // --- Internals ---------------------------------------------------------------------------

    /// <summary>Room bank first, game default second. This is the whole fallback rule.</summary>
    private bool Resolve(SfxId id, SfxBank roomBank, out AudioClip clip, out float volume, out float pitch)
    {
        if (roomBank != null && roomBank.TryResolve(id, out clip, out volume, out pitch))
        {
            return true;
        }

        if (defaultBank != null && defaultBank.TryResolve(id, out clip, out volume, out pitch))
        {
            return true;
        }

        clip = null;
        volume = 0f;
        pitch = 1f;

        return false;
    }

    private void PlayVoice(AudioClip clip, Vector3 position, float volume, float pitch)
    {
        var voice = TakeVoice();

        if (voice == null)
        {
            return;
        }

        // Moved rather than parented: a voice attached to a lever would be destroyed with it
        // mid-sound, and AudioSource.PlayClipAtPoint allocates a GameObject per call.
        voice.transform.position = position;
        voice.clip = clip;
        voice.volume = volume * SfxVolume;
        voice.pitch = pitch;
        voice.Play();
    }

    private AudioSource TakeVoice()
    {
        if (voices == null || voices.Length == 0)
        {
            return null;
        }

        // Prefer a free voice; fall back to round-robin, which steals the oldest.
        for (var i = 0; i < voices.Length; i++)
        {
            var candidate = voices[(nextVoiceIndex + i) % voices.Length];

            if (candidate != null && !candidate.isPlaying)
            {
                nextVoiceIndex = (nextVoiceIndex + i + 1) % voices.Length;
                return candidate;
            }
        }

        var stolen = voices[nextVoiceIndex];
        nextVoiceIndex = (nextVoiceIndex + 1) % voices.Length;

        return stolen;
    }

    private void EnsureSources()
    {
        // The prefab wires both. Created here only so a hand-made manager object still works
        // instead of failing silently on its first sound.
        if (musicSource == null)
        {
            musicSource = CreateChildSource("MusicSource", spatial: false);
        }

        if (uiSource == null)
        {
            uiSource = CreateChildSource("UiSource", spatial: false);
        }

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        uiSource.playOnAwake = false;
        uiSource.volume = 1f;
    }

    private void BuildVoicePool()
    {
        var count = Mathf.Max(1, positionalVoiceCount);
        voices = new AudioSource[count];

        for (var i = 0; i < count; i++)
        {
            var voice = CreateChildSource($"Voice_{i:00}", spatial: true);
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = voiceMinDistance;
            voice.maxDistance = voiceMaxDistance;
            voices[i] = voice;
        }
    }

    private AudioSource CreateChildSource(string sourceName, bool spatial)
    {
        var host = new GameObject(sourceName);
        host.transform.SetParent(transform, worldPositionStays: false);

        var source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = spatial ? 1f : 0f;

        return source;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        positionalVoiceCount = Mathf.Clamp(positionalVoiceCount, 1, 64);
        voiceMaxDistance = Mathf.Max(voiceMaxDistance, voiceMinDistance + 0.1f);

        if (Application.isPlaying)
        {
            ApplyMix();
        }
    }
#endif
}
