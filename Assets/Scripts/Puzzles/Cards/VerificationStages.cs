using System;

/// <summary>
/// Helpers for <see cref="VerificationStage"/>. Kept separate from the enum so the enum
/// file stays the single, obvious statement of procedural order.
/// </summary>
public static class VerificationStages
{
    /// <summary>
    /// Every stage in procedural order. Pre-allocated: the central console and the
    /// evidence views both walk this every redraw, and Enum.GetValues allocates.
    /// </summary>
    public static readonly VerificationStage[] ProcedureOrder =
    {
        VerificationStage.Detect,
        VerificationStage.Confirm,
        VerificationStage.Classify,
        VerificationStage.Trace,
        VerificationStage.Authenticate,
        VerificationStage.Authorize,
        VerificationStage.Report,
    };

    /// <summary>
    /// The noun printed on a card for each stage, per the design doc: the console slots
    /// are verbs ("Confirm") but the printout reads "CONFIRMATION - 9".
    /// </summary>
    public static string DisplayLabel(VerificationStage stage)
    {
        switch (stage)
        {
            case VerificationStage.Detect:
                return "DETECTION";

            case VerificationStage.Confirm:
                return "CONFIRMATION";

            case VerificationStage.Classify:
                return "CLASSIFICATION";

            case VerificationStage.Trace:
                return "TRACE";

            case VerificationStage.Authenticate:
                return "AUTHENTICATION";

            case VerificationStage.Authorize:
                return "AUTHORIZATION";

            case VerificationStage.Report:
                return "REPORT";

            default:
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unmapped verification stage.");
        }
    }

    /// <summary>The verb the central console's slot is labelled with, e.g. "Confirm".</summary>
    public static string SlotLabel(VerificationStage stage)
    {
        return stage.ToString();
    }
}
