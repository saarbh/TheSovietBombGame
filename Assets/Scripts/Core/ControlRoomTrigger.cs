using UnityEngine;

/// <summary>
/// A volume at the corridor mouth that fires once, the first time the player steps out of the
/// control room. It kicks off the room progression (opening the first room's door) and marks
/// the control room departed, which the ending logic reads as activating the verification
/// protocol - per the doc, leaving the post is what arms automatic reporting.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ControlRoomTrigger : MonoBehaviour
{
    [Tooltip("Progression to start. Its first door opens when the player crosses this trigger.")]
    [SerializeField] private RoomProgressionController progression;

    [Tooltip("Also flag the control room as departed (arms the ending logic). Off leaves this a "
             + "pure door trigger with no effect on how the run resolves.")]
    [SerializeField] private bool markControlRoomDeparted = true;

    private bool hasFired;

    private void Reset()
    {
        // Authoring convenience: a trigger volume is only useful as a trigger.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasFired)
        {
            return;
        }

        // A CharacterController raises trigger events on the volume it enters; the player root
        // carries the controller, the PlayerController and the Player tag together.
        if (!other.CompareTag("Player"))
        {
            return;
        }

        hasFired = true;

        if (progression != null)
        {
            progression.Begin();
        }
        else
        {
            Debug.LogError("[ControlRoomTrigger] No RoomProgressionController assigned; the first door will not open.", this);
        }

        if (markControlRoomDeparted)
        {
            var player = other.GetComponent<PlayerController>();

            if (player != null)
            {
                player.MarkControlRoomDeparted();
            }
        }
    }
}
