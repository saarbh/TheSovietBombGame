using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A hold-to-read on-screen list of the evidence the player is carrying.
///
/// Explicitly the throwaway half of the inventory. If the team decides the bunker reads
/// better with no screen HUD at all, delete this object from the scene and write a
/// clipboard prop against <see cref="PuzzleCardInventoryView"/> instead - the data layer,
/// the rooms and the formatter all stay exactly as they are.
/// </summary>
public class EvidenceHudView : PuzzleCardInventoryView
{
    [Header("References")]
    [Tooltip("Faded in and out as the panel is toggled. Also what a diegetic replacement would drop.")]
    [SerializeField] private CanvasGroup panel;

    [SerializeField] private TMP_Text logText;

    [Tooltip("Optional. Shows the code assembled so far, with a blank per unfiled stage.")]
    [SerializeField] private TMP_Text assembledCodeText;

    [Header("Input")]
    [Tooltip("Optional. Leave empty to bind Tab directly without touching the shared input asset.")]
    [SerializeField] private InputActionReference toggleAction;

    [Header("Behaviour")]
    [Tooltip("Off means the panel only appears when the player asks for it.")]
    [SerializeField] private bool startVisible;

    [Tooltip("Off leaves the panel permanently hidden - the switch for going fully diegetic.")]
    [SerializeField] private bool allowToggle = true;

    [Tooltip("Show the evidence sentence under each card, not just the stage and character.")]
    [SerializeField] private bool showEvidenceText = true;

    [SerializeField] private float fadeSeconds = 0.12f;

    private readonly EvidenceLogFormatter formatter = new EvidenceLogFormatter();

    private InputAction resolvedToggleAction;
    private bool ownsToggleAction;
    private bool isVisible;
    private Tween fadeTween;

    /// <summary>True while the panel is being shown.</summary>
    public bool IsVisible => isVisible;

    private void Awake()
    {
        formatter.IncludeEvidence = showEvidenceText;

        ResolveToggleAction();
        ApplyVisibility(startVisible, instant: true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (resolvedToggleAction is null)
        {
            return;
        }

        resolvedToggleAction.performed += HandleTogglePerformed;
        resolvedToggleAction.Enable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (resolvedToggleAction is not null)
        {
            resolvedToggleAction.performed -= HandleTogglePerformed;

            // Only disable an action this view created. Disabling one that came from the
            // shared asset would switch it off for every other listener too.
            if (ownsToggleAction)
            {
                resolvedToggleAction.Disable();
            }
        }

        fadeTween?.Kill();
        fadeTween = null;
    }

    private void OnDestroy()
    {
        if (ownsToggleAction)
        {
            resolvedToggleAction?.Dispose();
        }
    }

    /// <summary>Shows or hides the panel. Public so a cutscene or the console can drive it.</summary>
    public void SetVisible(bool visible)
    {
        ApplyVisibility(visible, instant: false);
    }

    protected override void Redraw(IReadOnlyList<PuzzleCard> cards)
    {
        // The card list is already in procedure order; the formatter needs the inventory
        // itself so it can also report the stages that are still missing.
        if (logText != null)
        {
            logText.text = formatter.BuildLog(Inventory);
        }

        if (assembledCodeText != null)
        {
            assembledCodeText.text = formatter.BuildAssembledCodeLine(Inventory);
        }
    }

    private void ResolveToggleAction()
    {
        if (toggleAction != null)
        {
            resolvedToggleAction = toggleAction.action;
            ownsToggleAction = false;
            return;
        }

        // Authored in code rather than added to InputSystem_Actions.inputactions: that
        // asset is shared by all four branches and is a standing merge-conflict risk.
        // A locally owned action is not a second input asset, just a private binding.
        resolvedToggleAction = new InputAction("ToggleEvidence", InputActionType.Button, "<Keyboard>/tab");
        ownsToggleAction = true;
    }

    private void HandleTogglePerformed(InputAction.CallbackContext context)
    {
        // Project convention guards input against a paused game. Lift this if a pause menu
        // ever needs the evidence panel readable while stopped.
        if (Time.timeScale == 0f)
        {
            return;
        }

        if (!allowToggle)
        {
            return;
        }

        SetVisible(!isVisible);
    }

    private void ApplyVisibility(bool visible, bool instant)
    {
        isVisible = visible;

        if (panel == null)
        {
            return;
        }

        // Never raycast-blocking: this is a read-only overlay and the player keeps mouse
        // look the whole time it is up, so the cursor is never released.
        panel.interactable = false;
        panel.blocksRaycasts = false;

        var targetAlpha = visible ? 1f : 0f;

        fadeTween?.Kill();

        if (instant || fadeSeconds <= 0f)
        {
            panel.alpha = targetAlpha;
            return;
        }

        fadeTween = panel.DOFade(targetAlpha, fadeSeconds).SetUpdate(true);
    }
}
