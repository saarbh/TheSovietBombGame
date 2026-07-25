using System;
using UnityEngine;

/// <summary>
/// Authoring configuration for Room 4 (Threat Volume & Alarm Override Room).
/// Defines the reboot sequence commands, missile count telemetry thresholds,
/// default anomaly override codes, sticky note hints, and doctrine manual text.
/// </summary>
[CreateAssetMenu(fileName = "ThreatVolumeRoomConfig", menuName = "SovietBomb/Threat Volume Room Config")]
public class ThreatVolumeRoomConfig : PuzzleConfig
{
    [Header("Reboot Sequence")]
    [Tooltip("Commands required in sequence to reboot the frozen terminal CRT monitor.")]
    [SerializeField] private string[] rebootSequenceCommands = new[] { "init 0", "check-mem", "start-oko" };

    [Header("Telemetry Configuration")]
    [Tooltip("Initial detected ICBM count when telemetry comes online.")]
    [SerializeField] private int initialMissileCount = 1;

    [Tooltip("Updated ICBM count after telemetry spike delay.")]
    [SerializeField] private int updatedMissileCount = 5;

    [Tooltip("Delay in seconds between initial missile detection and telemetry spike.")]
    [SerializeField] private float telemetryUpdateDelaySeconds = 2.5f;

    [Header("Override Codes")]
    [Tooltip("The correct anomaly override code (e.g. OVERRIDE-05).")]
    [SerializeField] private string correctOverrideCode = "OVERRIDE-05";

    [Tooltip("The incorrect full-strike override code (e.g. OVERRIDE-99).")]
    [SerializeField] private string incorrectOverrideCode = "OVERRIDE-99";

    [Header("Documentation & Hints")]
    [Tooltip("Text printed on the sticky note taped to CRT monitor bezel.")]
    [TextArea(2, 4)]
    [SerializeField] private string stickyNoteText = "Type /help for system commands";

    [Tooltip("Strategic Defense Doctrine reference text for help modal and wall chart.")]
    [TextArea(5, 10)]
    [SerializeField] private string doctrineHelpText =
        "[STRATEGIC DEFENSE DOCTRINE MANUAL]\n" +
        "------------------------------------\n" +
        "• FULL FIRST STRIKE: >= 1,000 ICBMs (Mass saturation) -> Code: OVERRIDE-99\n" +
        "• SENSOR ANOMALY:    < 10 ICBMs    (High-altitude solar glare reflection) -> Code: OVERRIDE-05\n\n" +
        "RULE: Do not initiate counterstrike on single-digit cloud glare anomaly!";

    public string[] RebootSequenceCommands => rebootSequenceCommands;
    public int InitialMissileCount => initialMissileCount;
    public int UpdatedMissileCount => updatedMissileCount;
    public float TelemetryUpdateDelaySeconds => telemetryUpdateDelaySeconds;
    public string CorrectOverrideCode => correctOverrideCode;
    public string IncorrectOverrideCode => incorrectOverrideCode;
    public string StickyNoteText => stickyNoteText;
    public string DoctrineHelpText => doctrineHelpText;
}
