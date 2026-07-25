using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base for anything that displays the player's collected cards. Owns the whole binding
/// and event dance so a concrete view only has to answer one question: given these cards,
/// what do you draw?
///
/// The point of the split is that the presentation is disposable. The on-screen panel is
/// only one implementation - a carried clipboard prop, a wall-mounted board or the central
/// console's slot readout are all just another subclass, and none of them require a change
/// to <see cref="PuzzleCardInventory"/>. Going fully diegetic means deleting a view, not
/// unpicking the data layer.
/// </summary>
public abstract class PuzzleCardInventoryView : MonoBehaviour
{
    private PuzzleCardInventory boundInventory;
    private bool hasStarted;

    /// <summary>The bound inventory, or null before <see cref="GameManager"/> exists.</summary>
    protected PuzzleCardInventory Inventory => boundInventory;

    // Binding happens on Start rather than Awake because GameManager assigns its Instance
    // in Awake and the relative order of two Awakes is undefined - binding any earlier is
    // a race that silently leaves the view blank for the whole run. OnEnable still handles
    // re-binding after the object is toggled off and on, which is the case the
    // subscribe-in-OnEnable convention actually exists to cover.
    protected virtual void OnEnable()
    {
        if (hasStarted)
        {
            Bind();
        }
    }

    protected virtual void Start()
    {
        hasStarted = true;
        Bind();
    }

    protected virtual void OnDisable()
    {
        Unbind();
    }

    /// <summary>
    /// Called whenever the held cards change, and once on bind. Cards arrive already
    /// sorted into verification-procedure order.
    /// </summary>
    protected abstract void Redraw(IReadOnlyList<PuzzleCard> cards);

    private void Bind()
    {
        // PuzzleCardInventory is a pure C# type, so pattern matching is correct here -
        // the UnityEngine.Object rule does not apply to it.
        if (boundInventory is not null)
        {
            return;
        }

        if (GameManager.Instance == null)
        {
            // Single-room test scenes run without a GameManager; the view simply stays empty.
            return;
        }

        boundInventory = GameManager.Instance.CardInventory;
        boundInventory.OnInventoryChanged += HandleInventoryChanged;

        HandleInventoryChanged();
    }

    private void Unbind()
    {
        if (boundInventory is null)
        {
            return;
        }

        boundInventory.OnInventoryChanged -= HandleInventoryChanged;
        boundInventory = null;
    }

    private void HandleInventoryChanged()
    {
        Redraw(boundInventory.CardsInProcedureOrder);
    }
}
