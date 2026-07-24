/// <summary>
/// Contract for anything the player can look at and use: doors, the phone,
/// the babushka doll. Implementors live on the interactable GameObject and must
/// be on a collider matching <c>PlayerInteraction.interactableMask</c>.
/// </summary>
public interface IInteractable
{
    void Interact(PlayerController player);

    /// <summary>Crosshair tooltip text, e.g. "[E] Open Door".</summary>
    string GetPrompt();
}
