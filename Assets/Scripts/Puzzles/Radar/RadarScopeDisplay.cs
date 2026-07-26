using System;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// The radar scope's contact list. It exists to answer the one question the room could not:
/// "did that knob do anything?"
///
/// The Radar room does not simulate radar - it is one correct knob combination read off the
/// operator note. With no feedback at all, though, the three dials looked inert, the note read
/// as flavour text, and the room played as a guessing game. So the scope now carries authored
/// clutter that the knobs genuinely filter: contacts drop off the glass as the player narrows
/// the settings, and the note's criteria leave exactly one track named.
///
/// It decides nothing and gates nothing. The answer still lives in the puzzle's authored
/// combination; this only reports which authored contacts survive the current dials. The two
/// are separate data and CAN drift apart - <see cref="LogContactMatrix"/> is the context-menu
/// check for that.
///
/// Each dial may have one "wildcard" position that filters nothing (CONTINUOUS sweep, ALL
/// track, RAW filter). All three start on theirs, so the room opens showing the full clutter,
/// which is what makes narrowing it feel like progress.
/// </summary>
public class RadarScopeDisplay : MonoBehaviour
{
    [Serializable]
    private class Contact
    {
        [Tooltip("How the track is named on the scope, e.g. TRACK 7.")]
        public string label = "TRACK";

        [Tooltip("SWEEP position this contact shows up under - how many consecutive sweeps it "
                 + "persists for.")]
        public int sweepIndex;

        [Tooltip("TRACK position this contact shows up under - which way it is travelling.")]
        public int trackIndex;

        [Tooltip("FILTER position this contact shows up under - how steady its return is.")]
        public int filterIndex;
    }

    [Header("Dials")]
    [SerializeField] private SelectorSwitch sweepDial;

    [Tooltip("SWEEP position that shows every persistence (CONTINUOUS). -1 for a dial with no "
             + "such position.")]
    [SerializeField] private int sweepWildcardIndex;

    [SerializeField] private SelectorSwitch trackDial;

    [Tooltip("TRACK position that shows every heading (ALL). -1 for none.")]
    [SerializeField] private int trackWildcardIndex;

    [SerializeField] private SelectorSwitch filterDial;

    [Tooltip("FILTER position that shows every return unfiltered (RAW). -1 for none.")]
    [SerializeField] private int filterWildcardIndex;

    [Header("Display")]
    [SerializeField] private TMP_Text scopeDisplay;

    [Tooltip("First line, always shown.")]
    [SerializeField] private string header = "SCOPE - STATION 4";

    [Tooltip("Count line. {0} is the number of contacts currently on the scope.")]
    [SerializeField] private string countFormat = "CONTACTS: {0}";

    [Tooltip("Shown instead of the count when the dials filter everything out.")]
    [SerializeField] private string emptyLine = "NO CONTACTS";

    [Tooltip("Appended when exactly one contact remains - the payoff for narrowing the dials.")]
    [SerializeField] private string isolatedLine = "TRACK ISOLATED";

    [Header("Contacts")]
    [Tooltip("The clutter on the scope. Exactly one of these should survive the combination the "
             + "puzzle counts as correct.")]
    [SerializeField] private Contact[] contacts = Array.Empty<Contact>();

    // Reused rather than rebuilt: this runs on every dial click, and a jam build has no budget
    // to spare on garbage that is trivially avoidable.
    private readonly StringBuilder builder = new StringBuilder(160);

    private void OnEnable()
    {
        Subscribe(sweepDial, true);
        Subscribe(trackDial, true);
        Subscribe(filterDial, true);
    }

    private void OnDisable()
    {
        Subscribe(sweepDial, false);
        Subscribe(trackDial, false);
        Subscribe(filterDial, false);
    }

    // Start, not OnEnable: SelectorSwitch takes its opening position in Awake and deliberately
    // does NOT raise OnSelectionChanged for it, so the first paint has to be pushed by hand or
    // the scope reads blank until the player touches something.
    private void Start()
    {
        Refresh();
    }

    private void Subscribe(SelectorSwitch dial, bool subscribe)
    {
        if (dial == null)
        {
            return;
        }

        if (subscribe)
        {
            dial.OnSelectionChanged += HandleDialChanged;
            return;
        }

        dial.OnSelectionChanged -= HandleDialChanged;
    }

    private void HandleDialChanged(SelectorSwitch changed)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (scopeDisplay == null)
        {
            return;
        }

        var visible = 0;

        builder.Clear();
        builder.Append(header);

        // Counted first, printed second: an operator reads the total, then what makes it up, so
        // the count line has to be assembled before the list it summarises.
        foreach (var contact in contacts)
        {
            if (contact == null || !IsVisible(contact))
            {
                continue;
            }

            visible++;
        }

        builder.Append('\n').Append(visible == 0 ? emptyLine : string.Format(countFormat, visible));

        if (visible == 1 && !string.IsNullOrEmpty(isolatedLine))
        {
            builder.Append('\n').Append(isolatedLine);
        }

        foreach (var contact in contacts)
        {
            if (contact == null || !IsVisible(contact))
            {
                continue;
            }

            builder.Append("\n> ").Append(contact.label);
        }

        scopeDisplay.text = builder.ToString();
    }

    /// <summary>
    /// A contact is on the scope when every dial either sits on its wildcard position or matches
    /// the position that contact appears under. A dial left unwired filters nothing, so a
    /// half-authored room still reads rather than going dark.
    /// </summary>
    private bool IsVisible(Contact contact)
    {
        return Passes(sweepDial, sweepWildcardIndex, contact.sweepIndex)
               && Passes(trackDial, trackWildcardIndex, contact.trackIndex)
               && Passes(filterDial, filterWildcardIndex, contact.filterIndex);
    }

    private static bool Passes(SelectorSwitch dial, int wildcardIndex, int contactIndex)
    {
        if (dial == null)
        {
            return true;
        }

        var current = dial.CurrentIndex;

        return current == wildcardIndex || current == contactIndex;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Prints how many contacts every dial combination leaves on the scope, and which ones
    /// isolate exactly one. Authoring aid: the room is only honest if the combination the puzzle
    /// calls correct is one of the isolating ones, and that is not something the Inspector can
    /// show you.
    /// </summary>
    [ContextMenu("Log Contact Matrix")]
    private void LogContactMatrix()
    {
        if (sweepDial == null || trackDial == null || filterDial == null)
        {
            Debug.LogWarning($"[Radar] '{name}' cannot build a contact matrix - one of the dials "
                             + "is unwired.", this);
            return;
        }

        var report = new StringBuilder();
        report.AppendLine($"[Radar] Contact matrix for '{name}' (sweep x track x filter):");

        var isolating = 0;

        for (var s = 0; s < sweepDial.OptionCount; s++)
        {
            for (var t = 0; t < trackDial.OptionCount; t++)
            {
                for (var f = 0; f < filterDial.OptionCount; f++)
                {
                    var count = CountVisibleAt(s, t, f);

                    if (count == 1)
                    {
                        isolating++;
                    }

                    report.AppendLine($"  [{s},{t},{f}] -> {count}{(count == 1 ? "  <-- isolates" : string.Empty)}");
                }
            }
        }

        report.AppendLine($"  {isolating} combination(s) isolate a single contact.");
        Debug.Log(report.ToString(), this);
    }

    /// <summary>Editor-only count for a hypothetical dial position, without moving any dial.</summary>
    private int CountVisibleAt(int sweep, int track, int filter)
    {
        var count = 0;

        foreach (var contact in contacts)
        {
            if (contact == null)
            {
                continue;
            }

            var passes = (sweep == sweepWildcardIndex || sweep == contact.sweepIndex)
                         && (track == trackWildcardIndex || track == contact.trackIndex)
                         && (filter == filterWildcardIndex || filter == contact.filterIndex);

            if (passes)
            {
                count++;
            }
        }

        return count;
    }

    private void OnValidate()
    {
        ValidateIndices(sweepDial, "SWEEP", c => c.sweepIndex);
        ValidateIndices(trackDial, "TRACK", c => c.trackIndex);
        ValidateIndices(filterDial, "FILTER", c => c.filterIndex);

        // Two contacts with identical signatures can never be told apart, so one of them is
        // permanently invisible padding rather than clutter the player can filter.
        for (var i = 0; i < contacts.Length; i++)
        {
            for (var j = i + 1; j < contacts.Length; j++)
            {
                if (contacts[i] == null || contacts[j] == null)
                {
                    continue;
                }

                if (contacts[i].sweepIndex == contacts[j].sweepIndex
                    && contacts[i].trackIndex == contacts[j].trackIndex
                    && contacts[i].filterIndex == contacts[j].filterIndex)
                {
                    Debug.LogWarning(
                        $"[Radar] '{name}' contacts '{contacts[i].label}' and '{contacts[j].label}' "
                        + "have the same signature; no dial setting can ever separate them.", this);
                }
            }
        }
    }

    private void ValidateIndices(SelectorSwitch dial, string dialName, Func<Contact, int> selector)
    {
        if (dial == null || dial.OptionCount == 0)
        {
            return;
        }

        foreach (var contact in contacts)
        {
            if (contact == null)
            {
                continue;
            }

            var index = selector(contact);

            if (index < 0 || index >= dial.OptionCount)
            {
                Debug.LogWarning(
                    $"[Radar] '{name}' contact '{contact.label}' has {dialName} index {index}, but "
                    + $"that dial only has {dial.OptionCount} positions. It will never be visible.",
                    this);
            }
        }
    }
#endif
}
