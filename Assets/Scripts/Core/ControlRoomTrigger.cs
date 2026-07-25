using UnityEngine;

/// <summary>
/// A volume at the corridor mouth that fires once, the first time the player steps out of the
/// control room. Marks the control room departed, which arms the ending logic.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ControlRoomTrigger : MonoBehaviour
{
    [Tooltip("Flag the control room as departed (arms the ending logic).")]
    [SerializeField] private bool markControlRoomDeparted = true;

    private bool hasFired;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasFired)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        hasFired = true;

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
