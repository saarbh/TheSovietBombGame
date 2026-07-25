using TMPro;
using UnityEngine;

/// <summary>
/// Controls the door hint display card in the 3D world.
/// Takes a <see cref="RoomConfig"/> and formats the card text using string interpolation.
/// </summary>
public class DoorHintController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private RoomConfig roomConfig;

    [Header("References")]
    [SerializeField] private TMP_Text textMesh;

    public RoomConfig RoomConfig => roomConfig;

    private void Awake()
    {
        EnsureReferences();
        UpdateDisplay();
    }

    private void OnValidate()
    {
        EnsureReferences();
        UpdateDisplay();
    }

    private void EnsureReferences()
    {
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TMP_Text>();
        }
    }

    /// <summary>
    /// Assigns a new <see cref="RoomConfig"/> and updates the card text display.
    /// </summary>
    public void SetConfig(RoomConfig config)
    {
        roomConfig = config;
        UpdateDisplay();
    }

    /// <summary>
    /// Updates the card text based on the assigned <see cref="RoomConfig"/>.
    /// </summary>
    public void UpdateDisplay()
    {
        EnsureReferences();

        if (textMesh == null)
        {
            return;
        }

        if (roomConfig == null)
        {
            return;
        }

        textMesh.text = $"{roomConfig.RoomId} Code: {roomConfig.CorrectPasscode}\n\nOpens at {roomConfig.ExpectedUnlockTimeMinutes}";
    }
}
