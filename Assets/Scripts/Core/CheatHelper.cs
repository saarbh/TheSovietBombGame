using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A helper component that displays an overlay listing all available developer cheats.
/// It automatically loads the cheats list from cheats.json and listens for input to toggle/hide the overlay.
/// </summary>
public class CheatHelper : MonoBehaviour
{
    [Serializable]
    private class CheatItem
    {
        public string key;
        public string description;
    }

    [Serializable]
    private class CheatList
    {
        public List<CheatItem> cheats;
    }

    private List<CheatItem> cheatItems = new List<CheatItem>();
    private bool isToastVisible = false;
    private GUIStyle toastStyle;
    private GUIStyle headerStyle;
    private GUIStyle itemStyle;

    private bool wasHPressedLastFrame = false;
    private bool wasTPressedLastFrame = false;
    private bool[] wasDigitPressedLastFrame = new bool[4];
    private bool[] wasNumpadPressedLastFrame = new bool[4];

    private void Start()
    {
        LoadCheats();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        var isShiftPressed = keyboard.shiftKey.isPressed;

        bool hPressed = keyboard.hKey.isPressed;
        bool tPressed = keyboard.tKey.isPressed;
        bool d1 = keyboard.digit1Key.isPressed;
        bool d2 = keyboard.digit2Key.isPressed;
        bool d3 = keyboard.digit3Key.isPressed;
        bool d4 = keyboard.digit4Key.isPressed;
        bool n1 = keyboard.numpad1Key.isPressed;
        bool n2 = keyboard.numpad2Key.isPressed;
        bool n3 = keyboard.numpad3Key.isPressed;
        bool n4 = keyboard.numpad4Key.isPressed;

        // Toggle cheat helper overlay on Shift + H
        if (isShiftPressed && hPressed && !wasHPressedLastFrame)
        {
            isToastVisible = !isToastVisible;
        }

        // Close toast if user triggers any cheat keys
        if (isToastVisible)
        {
            var digitPressed = (d1 && !wasDigitPressedLastFrame[0]) ||
                               (d2 && !wasDigitPressedLastFrame[1]) ||
                               (d3 && !wasDigitPressedLastFrame[2]) ||
                               (d4 && !wasDigitPressedLastFrame[3]) ||
                               (n1 && !wasNumpadPressedLastFrame[0]) ||
                               (n2 && !wasNumpadPressedLastFrame[1]) ||
                               (n3 && !wasNumpadPressedLastFrame[2]) ||
                               (n4 && !wasNumpadPressedLastFrame[3]);

            var timeCheatPressed = isShiftPressed && tPressed && !wasTPressedLastFrame;

            if (digitPressed || timeCheatPressed)
            {
                isToastVisible = false;
            }
        }

        wasHPressedLastFrame = hPressed;
        wasTPressedLastFrame = tPressed;
        wasDigitPressedLastFrame[0] = d1;
        wasDigitPressedLastFrame[1] = d2;
        wasDigitPressedLastFrame[2] = d3;
        wasDigitPressedLastFrame[3] = d4;
        wasNumpadPressedLastFrame[0] = n1;
        wasNumpadPressedLastFrame[1] = n2;
        wasNumpadPressedLastFrame[2] = n3;
        wasNumpadPressedLastFrame[3] = n4;
    }

    private void OnGUI()
    {
        if (!isToastVisible)
        {
            return;
        }

        InitializeStyles();

        var width = 600f;
        var headerHeight = 50f;
        var rowHeight = 35f;
        var padding = 20f;
        var height = headerHeight + (cheatItems.Count * rowHeight) + padding;

        var rect = new Rect((Screen.width - width) / 2f, 130f, width, height);

        GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        GUI.Box(rect, string.Empty, toastStyle);

        // Header
        var headerRect = new Rect(rect.x, rect.y + 10f, rect.width, headerHeight);
        GUI.Label(headerRect, "DEVELOPER CHEAT SHEET", headerStyle);

        // Cheat rows
        var currentY = rect.y + headerHeight + 5f;
        foreach (var item in cheatItems)
        {
            var keyRect = new Rect(rect.x + 30f, currentY, 200f, rowHeight);
            var descRect = new Rect(rect.x + 240f, currentY, rect.width - 270f, rowHeight);

            GUI.Label(keyRect, $"<b>{item.key}</b>", itemStyle);
            GUI.Label(descRect, item.description, itemStyle);

            currentY += rowHeight;
        }
    }

    private void LoadCheats()
    {
        try
        {
            var jsonAsset = Resources.Load<TextAsset>("cheats");
            if (jsonAsset != null)
            {
                var loaded = JsonUtility.FromJson<CheatList>(jsonAsset.text);
                if (loaded != null && loaded.cheats != null)
                {
                    cheatItems = loaded.cheats;
                }
            }
            else
            {
                Debug.LogWarning("[CheatHelper] Failed to load cheats.json from Resources.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CheatHelper] Error loading cheats: {ex.Message}");
        }
    }

    private void InitializeStyles()
    {
        if (toastStyle == null)
        {
            toastStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(15, 15, 10, 10)
            };
        }

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = Color.yellow;
        }

        if (itemStyle == null)
        {
            itemStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 18,
                richText = true
            };
            itemStyle.normal.textColor = Color.white;
        }
    }
}
