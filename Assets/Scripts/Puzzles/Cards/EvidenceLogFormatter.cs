using System.Collections.Generic;
using System.Text;

/// <summary>
/// Turns a <see cref="PuzzleCardInventory"/> into the block of text a view displays.
///
/// Deliberately separate from any view: the on-screen panel, a carried clipboard prop and
/// the central console all need the same wording, and duplicating this formatting is how
/// three surfaces end up disagreeing about what the player is holding. Pure C# so it can
/// be unit tested without a scene.
///
/// One instance per view, reused across redraws - the StringBuilder is the whole point.
/// </summary>
public class EvidenceLogFormatter
{
    private const string MISSING_CHARACTER = "_";

    private readonly StringBuilder builder = new StringBuilder(512);

    /// <summary>Show the evidence sentence under each filed card, not just the code line.</summary>
    public bool IncludeEvidence { get; set; } = true;

    /// <summary>List required stages the player has not reached yet, so the gaps are visible.</summary>
    public bool IncludeMissingStages { get; set; } = true;

    /// <summary>
    /// The evidence log: one entry per stage, in the order the central console reads them.
    /// </summary>
    public string BuildLog(PuzzleCardInventory inventory)
    {
        builder.Clear();

        if (inventory is null)
        {
            return string.Empty;
        }

        if (inventory.CardCount == 0 && !IncludeMissingStages)
        {
            return "No evidence collected.";
        }

        AppendProcedureOrderedEntries(inventory);

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// The assembled code with a placeholder for every required stage still missing, e.g.
    /// "_ 9 _ _ _". Spaced out because the player has to read it off and type it in.
    /// </summary>
    public string BuildAssembledCodeLine(PuzzleCardInventory inventory)
    {
        builder.Clear();

        if (inventory is null)
        {
            return string.Empty;
        }

        // With no declared requirements there is nothing to pad against, so the code is
        // simply whatever has been collected - the isolated-test-scene case.
        if (inventory.RequiredStages.Count == 0)
        {
            AppendSpaced(inventory.AssembledCode);
            return builder.ToString();
        }

        for (var i = 0; i < inventory.RequiredStages.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            if (inventory.TryGetCard(inventory.RequiredStages[i], out var card))
            {
                builder.Append(card.CodeCharacter);
            }
            else
            {
                builder.Append(MISSING_CHARACTER);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Walks the whole procedure in order, printing each stage as either a filed card or a
    /// gap. Listing the filed cards first and the gaps afterwards would be easier, but it
    /// reads as CONFIRMATION-then-DETECTION and quietly teaches the player the wrong order -
    /// and the order is the one thing the finale actually tests them on.
    /// </summary>
    private void AppendProcedureOrderedEntries(PuzzleCardInventory inventory)
    {
        var procedure = VerificationStages.ProcedureOrder;

        for (var i = 0; i < procedure.Length; i++)
        {
            var stage = procedure[i];

            if (inventory.TryGetCard(stage, out var card))
            {
                AppendFiledCard(card);
                continue;
            }

            // Stages no room in this build covers are left out entirely, so the player is
            // never shown a slot they cannot possibly fill.
            if (IncludeMissingStages && Contains(inventory.RequiredStages, stage))
            {
                AppendMissingStage(stage);
            }
        }
    }

    private void AppendFiledCard(PuzzleCard card)
    {
        builder.Append(card.StageLabel);
        builder.Append(" - ");
        builder.Append(card.CodeCharacter);
        builder.Append('\n');

        // Evidence is the reason the room exists; the character alone tells the player
        // nothing about whether the launch is real.
        if (!IncludeEvidence || string.IsNullOrEmpty(card.Evidence))
        {
            builder.Append('\n');
            return;
        }

        builder.Append("    ");
        builder.Append(card.Evidence);
        builder.Append("\n\n");
    }

    private void AppendMissingStage(VerificationStage stage)
    {
        builder.Append(VerificationStages.DisplayLabel(stage));
        builder.Append(" - ");
        builder.Append(MISSING_CHARACTER);
        builder.Append("\n    not filed\n\n");
    }

    private static bool Contains(IReadOnlyList<VerificationStage> stages, VerificationStage stage)
    {
        for (var i = 0; i < stages.Count; i++)
        {
            if (stages[i] == stage)
            {
                return true;
            }
        }

        return false;
    }

    private void AppendSpaced(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(value[i]);
        }
    }
}
