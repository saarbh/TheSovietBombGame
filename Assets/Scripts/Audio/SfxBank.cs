using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lookup from <see cref="SfxId"/> to actual clips. One bank is the game-wide default; a
/// room may carry its own and override any subset of it - anything the room bank does not
/// declare falls through to the default, which is what lets a new room ship with no audio
/// authoring at all and still make noise.
///
/// Deliberately an entry list rather than the spec's enum-indexed array: an array indexed by
/// a cast enum silently rebinds every clip the moment someone inserts a member, and it cannot
/// hold the per-sound volume, pitch spread and variation set that keeps a lever pull from
/// sounding identical fifty times in seven minutes.
/// </summary>
[CreateAssetMenu(fileName = "SfxBank", menuName = "The Hot War/Audio/SFX Bank")]
public class SfxBank : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SfxId id = SfxId.None;

        [Tooltip("One is chosen per play. Two or more stops a repeated action sounding mechanical.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();

        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("Random pitch range. Keep it narrow - anything past ~0.1 either side reads as a bug.")]
        public Vector2 pitchRange = new Vector2(0.96f, 1.04f);

        // Runtime only: which variation played last, so the next pick avoids it. Not serialized,
        // so nothing here dirties the asset while play mode is running.
        [NonSerialized] public int lastClipIndex = -1;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    private Dictionary<SfxId, Entry> lookup;

    /// <summary>
    /// Resolves a sound to a concrete clip, volume and pitch. False when this bank has nothing
    /// for the id, which is the caller's cue to fall through to the default bank.
    /// </summary>
    public bool TryResolve(SfxId id, out AudioClip clip, out float volume, out float pitch)
    {
        clip = null;
        volume = 1f;
        pitch = 1f;

        if (id == SfxId.None)
        {
            return false;
        }

        EnsureLookup();

        if (!lookup.TryGetValue(id, out var entry) || entry.clips.Length == 0)
        {
            return false;
        }

        clip = PickClip(entry);

        if (clip == null)
        {
            return false;
        }

        volume = entry.volume;
        pitch = UnityEngine.Random.Range(entry.pitchRange.x, entry.pitchRange.y);

        return true;
    }

    /// <summary>True when this bank declares the id itself, ignoring any fallback.</summary>
    public bool Declares(SfxId id)
    {
        EnsureLookup();
        return lookup.ContainsKey(id);
    }

    private static AudioClip PickClip(Entry entry)
    {
        if (entry.clips.Length == 1)
        {
            return entry.clips[0];
        }

        // Re-rolls once off the previous pick rather than looping until it differs, so a bank
        // whose variations are all the same clip cannot spin here.
        var index = UnityEngine.Random.Range(0, entry.clips.Length);

        if (index == entry.lastClipIndex)
        {
            index = (index + 1) % entry.clips.Length;
        }

        entry.lastClipIndex = index;

        return entry.clips[index];
    }

    private void EnsureLookup()
    {
        if (lookup != null)
        {
            return;
        }

        lookup = new Dictionary<SfxId, Entry>(entries.Length);

        foreach (var entry in entries)
        {
            if (entry == null || entry.id == SfxId.None)
            {
                continue;
            }

            if (lookup.ContainsKey(entry.id))
            {
                Debug.LogWarning($"[Audio] Bank '{name}' declares {entry.id} twice; the first entry wins.", this);
                continue;
            }

            lookup[entry.id] = entry;
        }
    }

    private void OnDisable()
    {
        // Dropped so an edit to the asset while play mode is running is picked up on the next
        // domain reload rather than serving a stale table for the rest of the session.
        lookup = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        lookup = null;

        foreach (var entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            // An inverted range makes Random.Range return the low end every time, which reads
            // as "the pitch variation silently stopped working".
            if (entry.pitchRange.y < entry.pitchRange.x)
            {
                entry.pitchRange = new Vector2(entry.pitchRange.y, entry.pitchRange.x);
            }
        }
    }
#endif
}
