using TMPro;
using UnityEngine;

/// <summary>
/// Diegetic 3D code card placed on desks/walls in the control room.
/// Displays the room unlock schedule and passcode directly via 3D TextMeshPro on its front.
/// Non-interactable—the player simply reads it directly in the 3D world.
/// </summary>
public class ControlRoomCodeCard : MonoBehaviour
{
    [Header("Card Data")]
    [SerializeField] private RoomConfig roomConfig;
    [SerializeField] private string cardTitle = "CODE CARD";

    [TextArea(3, 6)]
    [SerializeField] private string customTextOverride;

    [Header("3D Display")]
    [SerializeField] private TextMeshPro textMesh3D;

    public RoomConfig RoomConfig => roomConfig;

    private void Awake()
    {
        EnsureTextMesh();
        UpdateDisplay();
    }

    private void OnValidate()
    {
        EnsureTextMesh();
        UpdateDisplay();
    }

    private void EnsureTextMesh()
    {
        if (textMesh3D == null)
        {
            textMesh3D = GetComponentInChildren<TextMeshPro>();
        }
    }

    public void SetConfig(RoomConfig config)
    {
        roomConfig = config;
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        EnsureTextMesh();

        if (textMesh3D == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(customTextOverride))
        {
            textMesh3D.text = customTextOverride;
            return;
        }

        if (roomConfig == null)
        {
            textMesh3D.text = $"<b>{cardTitle}</b>\n<size=80%>NO CONFIG</size>";
            return;
        }

        // No unlock time is printed: doors are gated on the passcode alone, so advertising
        // a minute would be a instruction the player can never act on.
        textMesh3D.text = $"<b>{cardTitle} ({roomConfig.RoomId})</b>\n" +
                          $"<color=#FFD700><b>PASSCODE: {roomConfig.CorrectPasscode}</b></color>";
    }
}
