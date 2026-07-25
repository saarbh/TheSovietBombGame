using UnityEngine;

/// <summary>
/// Voices a <see cref="RoomDoor"/>. Keyed to the start of the swing rather than its end, so
/// the hinge is heard with the movement instead of a full swing after it, and to the completed
/// event only for the latch settling.
/// </summary>
public class RoomDoorAudio : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private RoomDoor door;

    [Header("Swing")]
    [SerializeField] private SfxId openSfx = SfxId.DoorOpen;

    [SerializeField] private SfxId closeSfx = SfxId.DoorClose;

    [Header("Settle")]
    [Tooltip("Optional. Played as the leaf comes to rest closed - the latch, not the hinge.")]
    [SerializeField] private SfxId closedSettleSfx = SfxId.None;

    private SfxBank roomBank;

    private void Awake()
    {
        if (door == null)
        {
            door = GetComponent<RoomDoor>();
        }

        if (door == null)
        {
            door = GetComponentInParent<RoomDoor>();
        }

        roomBank = RoomAudioZone.BankFor(this);
    }

    private void OnEnable()
    {
        if (door == null)
        {
            Debug.LogError("[Audio] RoomDoorAudio has no RoomDoor; this door will be silent.", this);
            return;
        }

        door.OnSwingStarted += HandleSwingStarted;
        door.OnSwingCompleted += HandleSwingCompleted;
    }

    private void OnDisable()
    {
        if (door == null)
        {
            return;
        }

        door.OnSwingStarted -= HandleSwingStarted;
        door.OnSwingCompleted -= HandleSwingCompleted;
    }

    private void HandleSwingStarted(bool isOpening)
    {
        Play(isOpening ? openSfx : closeSfx);
    }

    private void HandleSwingCompleted(bool isOpen)
    {
        if (isOpen)
        {
            return;
        }

        Play(closedSettleSfx);
    }

    private void Play(SfxId id)
    {
        if (id == SfxId.None)
        {
            return;
        }

        AudioManager.PlayAt(id, transform.position, roomBank);
    }
}
