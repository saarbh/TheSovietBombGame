using UnityEngine;
using UnityEngine.InputSystem;

namespace Core
{
    /// <summary>
    /// Manages loading, saving, and dynamic rebinding of player input settings using the Unity Input System.
    /// </summary>
    public static class InputSettingsManager
    {
        public const string KEYMAP_PREFS_KEY = "Petrov_KeymapOverrides";
        public const string EVIDENCE_KEY_PREFS_KEY = "Petrov_EvidenceKey_Override";
        public const string DEFAULT_EVIDENCE_PATH = "<Keyboard>/tab";

        /// <summary>
        /// Loads overrides from PlayerPrefs and applies them to the given action asset.
        /// </summary>
        public static void LoadOverrides(InputActionAsset actions)
        {
            if (actions == null)
            {
                return;
            }

            var rebinds = PlayerPrefs.GetString(KEYMAP_PREFS_KEY, string.Empty);
            if (!string.IsNullOrEmpty(rebinds))
            {
                actions.Disable();
                actions.LoadBindingOverridesFromJson(rebinds);
                actions.Enable();
            }
        }

        /// <summary>
        /// Saves the current overrides of the action asset to PlayerPrefs.
        /// </summary>
        public static void SaveOverrides(InputActionAsset actions)
        {
            if (actions == null)
            {
                return;
            }

            var rebinds = actions.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(KEYMAP_PREFS_KEY, rebinds);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Gets the current key display string for the Evidence Log toggle.
        /// </summary>
        public static string GetEvidenceKeyDisplayName()
        {
            var path = PlayerPrefs.GetString(EVIDENCE_KEY_PREFS_KEY, DEFAULT_EVIDENCE_PATH);
            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        /// <summary>
        /// Configures and starts a rebinding operation for a specific action and binding index.
        /// </summary>
        public static InputActionRebindingExtensions.RebindingOperation RebindAction(
            InputAction action,
            int bindingIndex,
            System.Action onComplete,
            System.Action onCancel)
        {
            if (action == null)
            {
                return null;
            }

            action.Disable();

            var rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Gamepad>/leftStick")
                .WithControlsExcluding("<Gamepad>/rightStick")
                .WithExpectedControlType("Key")
                .WithCancelingThrough("<Keyboard>/escape");

            rebind.OnComplete(operation =>
            {
                action.Enable();
                onComplete?.Invoke();
                operation.Dispose();
            });

            rebind.OnCancel(operation =>
            {
                action.Enable();
                onCancel?.Invoke();
                operation.Dispose();
            });

            rebind.Start();
            return rebind;
        }

        /// <summary>
        /// Performs rebinding for the Evidence Log standalone key.
        /// </summary>
        public static InputActionRebindingExtensions.RebindingOperation RebindEvidenceKey(
            System.Action<string> onComplete,
            System.Action onCancel)
        {
            var currentPath = PlayerPrefs.GetString(EVIDENCE_KEY_PREFS_KEY, DEFAULT_EVIDENCE_PATH);
            var tempAction = new InputAction("ToggleEvidence", InputActionType.Button, currentPath);

            var rebind = tempAction.PerformInteractiveRebinding(0)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Gamepad>/leftStick")
                .WithControlsExcluding("<Gamepad>/rightStick")
                .WithExpectedControlType("Key")
                .WithCancelingThrough("<Keyboard>/escape");

            rebind.OnComplete(operation =>
            {
                var newPath = tempAction.bindings[0].overridePath;
                if (string.IsNullOrEmpty(newPath))
                {
                    newPath = tempAction.bindings[0].path;
                }

                PlayerPrefs.SetString(EVIDENCE_KEY_PREFS_KEY, newPath);
                PlayerPrefs.Save();

                onComplete?.Invoke(newPath);
                operation.Dispose();
            });

            rebind.OnCancel(operation =>
            {
                onCancel?.Invoke();
                operation.Dispose();
            });

            rebind.Start();
            return rebind;
        }
    }
}
