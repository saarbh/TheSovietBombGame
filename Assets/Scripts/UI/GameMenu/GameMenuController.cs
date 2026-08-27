using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace UI.GameMenu
{
    /// <summary>
    /// In-game pause menu controller for Petrov.
    /// Provides Continue, Options (audio settings and player key rebinding), Restart, and Main Menu actions.
    /// Manages cursor locking, game pausing (timeScale), action map switching, crosshair canvas toggling, and input integration.
    /// Uses serialized fields to reference the active player and crosshair directly from the scene.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameMenuController : MonoBehaviour
    {
        private const string PREFS_MASTER_VOL = "Petrov_MasterVolume";
        private const string PREFS_MUSIC_VOL = "Petrov_MusicVolume";
        private const string PREFS_SFX_VOL = "Petrov_SfxVolume";
        private const string PREFS_AMB_VOL = "Petrov_AmbienceVolume";



        [Header("Scene References")]
        [Tooltip("Reference to the player controller to disable interaction/movement during pause.")]
        [SerializeField] private PlayerController playerController;

        [Tooltip("Reference to PlayerInput on the active player to toggle between Player and UI action maps.")]
        [SerializeField] private PlayerInput playerInput;

        [Header("Crosshair")]
        [Tooltip("The crosshair Canvas or GameObject to disable while the menu is open to prevent cursor overlap.")]
        [SerializeField] private GameObject crosshairCanvas;

        [Header("Scene Config")]
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";

        [Header("Audio References")]
        [SerializeField] private AudioSource sfxAudioSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip hoverSfx;
        [SerializeField] private AudioClip clickSfx;

        [Header("Input Config")]
        [SerializeField] private InputActionAsset inputActionsAsset;
        [SerializeField] private InputActionReference backActionReference;

        private UIDocument uiDocument;
        private VisualElement rootElement;
        private VisualElement menuRoot;
        private VisualElement buttonsContainer;
        private VisualElement optionsModal;

        private Button continueButton;
        private Button optionsButton;
        private Button restartButton;
        private Button mainMenuButton;
        private Button optionsBackButton;

        private Slider masterSlider;
        private Slider musicSlider;
        private Slider sfxSlider;
        private Slider ambienceSlider;

        private Label masterValueLabel;
        private Label musicValueLabel;
        private Label sfxValueLabel;
        private Label ambienceValueLabel;

        private InputActionAsset actionsInstance;

        // Rebind buttons
        private Button rebindForwardButton;
        private Button rebindBackwardButton;
        private Button rebindLeftButton;
        private Button rebindRightButton;
        private Button rebindInteractButton;
        private Button rebindWatchButton;
        private Button rebindEvidenceButton;

        private InputActionRebindingExtensions.RebindingOperation activeRebindOp;

        private bool isMenuOpen;
        private bool isOptionsOpen;
        private bool isUIInitialized;
        private Tween fadeTween;

        // Dedicated InputAction for toggling the pause menu. Independent of PlayerInput.
        private InputAction escapeAction;
        private UnityEngine.EventSystems.EventSystem cachedEventSystem;

        /// <summary>True while the game menu is currently visible and active.</summary>
        public bool IsMenuOpen => isMenuOpen;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();

            EnsurePlayerReference();

            if (UnityEngine.InputSystem.InputSystem.settings != null)
            {
                UnityEngine.InputSystem.InputSystem.settings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            }

            if (inputActionsAsset != null)
            {
                actionsInstance = Instantiate(inputActionsAsset);
                Core.InputSettingsManager.LoadOverrides(actionsInstance);
            }

            escapeAction = new InputAction("PauseMenuToggle", InputActionType.Button, "<Keyboard>/escape");
            escapeAction.AddBinding("<Gamepad>/start");
            escapeAction.performed += OnEscapePerformed;
        }

        private void OnEnable()
        {
            SetupUI();
            escapeAction?.Enable();
        }

        private void OnDisable()
        {
            escapeAction?.Disable();

            if (playerInput != null && playerInput.inputIsActive)
            {
                playerInput.SwitchCurrentActionMap("Player");
            }

            if (cachedEventSystem != null)
            {
                cachedEventSystem.enabled = true;
                cachedEventSystem = null;
            }

            if (activeRebindOp != null)
            {
                activeRebindOp.Cancel();
            }

            UnregisterUIEvents();
            fadeTween?.Kill();
        }

        private void OnDestroy()
        {
            if (escapeAction != null)
            {
                escapeAction.performed -= OnEscapePerformed;
                escapeAction.Dispose();
                escapeAction = null;
            }
        }

        private void EnsurePlayerReference()
        {
            if (playerController == null)
            {
                playerController = PlayerController.Instance != null
                    ? PlayerController.Instance
                    : FindFirstObjectByType<PlayerController>();
            }

            if (playerController != null && playerInput == null)
            {
                playerInput = playerController.GetComponent<PlayerInput>();
            }

            if (crosshairCanvas == null)
            {
                var crosshair = GameObject.Find("Canvas") ?? GameObject.Find("CrosshairCanvas") ?? GameObject.Find("Crosshair");
                if (crosshair != null)
                {
                    crosshairCanvas = crosshair;
                }
            }
        }

        /// <summary>Called by the Input System exactly once per Escape / Start key press.</summary>
        private void OnEscapePerformed(InputAction.CallbackContext ctx)
        {
            if (activeRebindOp != null)
            {
                return;
            }

            Debug.Log($"[{nameof(GameMenuController)}] Escape/Pause performed (isMenuOpen: {isMenuOpen}). Toggling menu.");
            ToggleMenu();
        }


        private void SetupUI()
        {
            if (isUIInitialized)
            {
                return;
            }

            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                return;
            }

            rootElement = uiDocument.rootVisualElement;
            if (rootElement == null)
            {
                return;
            }

            isUIInitialized = true;

            // Register global pointer callbacks on the root UI element to log any click/pointer interaction
            rootElement.RegisterCallback<PointerDownEvent>(evt =>
            {
                Debug.Log($"[{nameof(GameMenuController)}] UI Toolkit PointerDownEvent on: '{((VisualElement)evt.target)?.name}' (target type: {evt.target?.GetType().Name}, button: {evt.button}, pos: {evt.position})");
            }, TrickleDown.TrickleDown);

            rootElement.RegisterCallback<ClickEvent>(evt =>
            {
                Debug.Log($"[{nameof(GameMenuController)}] UI Toolkit ClickEvent on: '{((VisualElement)evt.target)?.name}' (target type: {evt.target?.GetType().Name}, button: {evt.button}, pos: {evt.position})");
            }, TrickleDown.TrickleDown);

            menuRoot = rootElement.Q<VisualElement>("game-menu-root");
            buttonsContainer = rootElement.Q<VisualElement>("buttons-container");
            optionsModal = rootElement.Q<VisualElement>("options-modal");

            if (menuRoot != null)
            {
                menuRoot.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
                menuRoot.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
                menuRoot.style.left = 0f;
                menuRoot.style.top = 0f;
                menuRoot.style.right = 0f;
                menuRoot.style.bottom = 0f;
                menuRoot.style.position = Position.Absolute;
                menuRoot.style.display = DisplayStyle.None;
                menuRoot.AddToClassList("hidden");
            }

            if (buttonsContainer != null)
            {
                buttonsContainer.style.display = DisplayStyle.Flex;
                buttonsContainer.style.opacity = 1f;
                buttonsContainer.RemoveFromClassList("hidden");
            }

            if (optionsModal != null)
            {
                optionsModal.style.display = DisplayStyle.None;
                optionsModal.AddToClassList("hidden");
            }

            continueButton = rootElement.Q<Button>("continue-button");
            optionsButton = rootElement.Q<Button>("options-button");
            restartButton = rootElement.Q<Button>("restart-button");
            mainMenuButton = rootElement.Q<Button>("main-menu-button");
            optionsBackButton = rootElement.Q<Button>("options-back-button");

            masterSlider = rootElement.Q<Slider>("master-volume-slider");
            musicSlider = rootElement.Q<Slider>("music-volume-slider");
            sfxSlider = rootElement.Q<Slider>("sfx-volume-slider");
            ambienceSlider = rootElement.Q<Slider>("ambience-volume-slider");

            masterValueLabel = rootElement.Q<Label>("master-volume-value");
            musicValueLabel = rootElement.Q<Label>("music-volume-value");
            sfxValueLabel = rootElement.Q<Label>("sfx-volume-value");
            ambienceValueLabel = rootElement.Q<Label>("ambience-volume-value");

            rebindForwardButton = rootElement.Q<Button>("rebind-forward-btn");
            rebindBackwardButton = rootElement.Q<Button>("rebind-backward-btn");
            rebindLeftButton = rootElement.Q<Button>("rebind-left-btn");
            rebindRightButton = rootElement.Q<Button>("rebind-right-btn");
            rebindInteractButton = rootElement.Q<Button>("rebind-interact-btn");
            rebindWatchButton = rootElement.Q<Button>("rebind-watch-btn");
            rebindEvidenceButton = rootElement.Q<Button>("rebind-evidence-btn");

            RegisterButtonEvents(continueButton, OnContinueClicked);
            RegisterButtonEvents(optionsButton, OnOptionsClicked);
            RegisterButtonEvents(restartButton, OnRestartClicked);
            RegisterButtonEvents(mainMenuButton, OnMainMenuClicked);
            RegisterButtonEvents(optionsBackButton, OnOptionsBackClicked);

            RegisterButtonEvents(rebindForwardButton, OnRebindForwardClicked);
            RegisterButtonEvents(rebindBackwardButton, OnRebindBackwardClicked);
            RegisterButtonEvents(rebindLeftButton, OnRebindLeftClicked);
            RegisterButtonEvents(rebindRightButton, OnRebindRightClicked);
            RegisterButtonEvents(rebindInteractButton, OnRebindInteractClicked);
            RegisterButtonEvents(rebindWatchButton, OnRebindWatchClicked);
            RegisterButtonEvents(rebindEvidenceButton, OnRebindEvidenceClicked);

            InitializeAudioSliders();
            UpdateKeymapLabels();
        }

        private void UnregisterUIEvents()
        {
            if (!isUIInitialized)
            {
                return;
            }

            isUIInitialized = false;

            UnregisterButtonEvents(continueButton, OnContinueClicked);
            UnregisterButtonEvents(optionsButton, OnOptionsClicked);
            UnregisterButtonEvents(restartButton, OnRestartClicked);
            UnregisterButtonEvents(mainMenuButton, OnMainMenuClicked);
            UnregisterButtonEvents(optionsBackButton, OnOptionsBackClicked);

            UnregisterButtonEvents(rebindForwardButton, OnRebindForwardClicked);
            UnregisterButtonEvents(rebindBackwardButton, OnRebindBackwardClicked);
            UnregisterButtonEvents(rebindLeftButton, OnRebindLeftClicked);
            UnregisterButtonEvents(rebindRightButton, OnRebindRightClicked);
            UnregisterButtonEvents(rebindInteractButton, OnRebindInteractClicked);
            UnregisterButtonEvents(rebindWatchButton, OnRebindWatchClicked);
            UnregisterButtonEvents(rebindEvidenceButton, OnRebindEvidenceClicked);

            UnbindSliderEvents();
        }

        /// <summary>Toggles between open and closed state.</summary>
        public void ToggleMenu()
        {
            Debug.Log($"[{nameof(GameMenuController)}] ToggleMenu called (currently isMenuOpen: {isMenuOpen}).");

            if (isMenuOpen)
            {
                if (isOptionsOpen)
                {
                    OnOptionsBackClicked();
                }
                else
                {
                    CloseMenu();
                }
            }
            else
            {
                OpenMenu();
            }
        }

        /// <summary>Opens the in-game pause menu, freezes game time, and reveals cursor.</summary>
        public void OpenMenu()
        {
            if (isMenuOpen)
            {
                return;
            }

            EnsurePlayerReference();

            if (!isUIInitialized)
            {
                SetupUI();
            }

            isMenuOpen = true;
            isOptionsOpen = false;

            // Disable EventSystem while menu is open so UI Toolkit uses its native direct input pipeline (identical to MainMenuScene)
            cachedEventSystem = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current
                : FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (cachedEventSystem != null)
            {
                cachedEventSystem.enabled = false;
            }

            // Hide crosshair to avoid collision with menu UI and cursor
            if (crosshairCanvas != null)
            {
                crosshairCanvas.SetActive(false);
            }

            // Disable gameplay movement/look and switch input to UI map
            if (playerController != null)
            {
                playerController.SetInputEnabled(false);
            }

            if (playerInput != null && playerInput.inputIsActive)
            {
                playerInput.SwitchCurrentActionMap("UI");
            }

            // Unlock and reveal mouse cursor
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            // Pause game timescale
            Time.timeScale = 0f;

            if (menuRoot != null)
            {
                menuRoot.style.display = DisplayStyle.Flex;
                menuRoot.RemoveFromClassList("hidden");
            }

            if (buttonsContainer != null)
            {
                buttonsContainer.style.display = DisplayStyle.Flex;
                buttonsContainer.style.opacity = 1f;
                buttonsContainer.RemoveFromClassList("hidden");
            }

            if (optionsModal != null)
            {
                optionsModal.style.display = DisplayStyle.None;
                optionsModal.AddToClassList("hidden");
            }

            AnimateEntrance();
            Debug.Log($"[{nameof(GameMenuController)}] OpenMenu executed. Time.timeScale: {Time.timeScale}, Cursor: {UnityEngine.Cursor.lockState}, Visible: {UnityEngine.Cursor.visible}");
        }

        /// <summary>Closes the in-game menu, resumes game time, and re-locks cursor.</summary>
        public void CloseMenu()
        {
            if (!isMenuOpen)
            {
                return;
            }

            EnsurePlayerReference();

            isMenuOpen = false;
            isOptionsOpen = false;

            if (menuRoot != null)
            {
                menuRoot.style.display = DisplayStyle.None;
                menuRoot.AddToClassList("hidden");
            }

            if (optionsModal != null)
            {
                optionsModal.style.display = DisplayStyle.None;
                optionsModal.AddToClassList("hidden");
            }

            // Unpause game timescale
            Time.timeScale = 1f;

            // Switch PlayerInput back to gameplay action map
            if (playerInput != null && playerInput.inputIsActive)
            {
                playerInput.SwitchCurrentActionMap("Player");
            }

            // Re-enable EventSystem if we disabled it
            if (cachedEventSystem != null)
            {
                cachedEventSystem.enabled = true;
                cachedEventSystem = null;
            }

            // Re-enable player movement and camera look
            if (playerController != null)
            {
                playerController.SetInputEnabled(true);
            }

            // Restore crosshair
            if (crosshairCanvas != null)
            {
                crosshairCanvas.SetActive(true);
            }

            // Re-lock mouse cursor for gameplay
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Debug.Log($"[{nameof(GameMenuController)}] CloseMenu executed. Time.timeScale: {Time.timeScale}, Cursor: {UnityEngine.Cursor.lockState}, Visible: {UnityEngine.Cursor.visible}");
        }

        private void OnContinueClicked()
        {
            Debug.Log($"[{nameof(GameMenuController)}] CONTINUE button clicked.");
            PlaySfx(clickSfx);
            CloseMenu();
        }

        private void OnOptionsClicked()
        {
            Debug.Log($"[{nameof(GameMenuController)}] OPTIONS button clicked.");
            PlaySfx(clickSfx);
            isOptionsOpen = true;

            if (buttonsContainer != null)
            {
                buttonsContainer.style.display = DisplayStyle.None;
                buttonsContainer.AddToClassList("hidden");
            }

            if (optionsModal != null)
            {
                optionsModal.style.display = DisplayStyle.Flex;
                optionsModal.RemoveFromClassList("hidden");
            }
        }

        private void OnOptionsBackClicked()
        {
            Debug.Log($"[{nameof(GameMenuController)}] OPTIONS BACK button clicked.");
            PlaySfx(clickSfx);
            isOptionsOpen = false;

            if (optionsModal != null)
            {
                optionsModal.style.display = DisplayStyle.None;
                optionsModal.AddToClassList("hidden");
            }

            if (buttonsContainer != null)
            {
                buttonsContainer.style.display = DisplayStyle.Flex;
                buttonsContainer.RemoveFromClassList("hidden");
            }
        }

        private void OnRestartClicked()
        {
            Debug.Log($"[{nameof(GameMenuController)}] RESTART button clicked.");
            PlaySfx(clickSfx);

            // Restore timescale, action map, EventSystem, and cursor before scene reload
            Time.timeScale = 1f;

            if (playerInput != null && playerInput.inputIsActive)
            {
                playerInput.SwitchCurrentActionMap("Player");
            }

            if (cachedEventSystem != null)
            {
                cachedEventSystem.enabled = true;
                cachedEventSystem = null;
            }

            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartScene();
            }
            else
            {
                var activeScene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(activeScene.buildIndex);
            }
        }

        private void OnMainMenuClicked()
        {
            Debug.Log($"[{nameof(GameMenuController)}] MAIN MENU button clicked.");
            PlaySfx(clickSfx);

            // Restore timescale, action map, EventSystem, and free cursor for main menu
            Time.timeScale = 1f;

            if (playerInput != null && playerInput.inputIsActive)
            {
                playerInput.SwitchCurrentActionMap("Player");
            }

            if (cachedEventSystem != null)
            {
                cachedEventSystem.enabled = true;
                cachedEventSystem = null;
            }

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void InitializeAudioSliders()
        {
            var masterVol = PlayerPrefs.GetFloat(PREFS_MASTER_VOL, 1.0f);
            var musicVol = PlayerPrefs.GetFloat(PREFS_MUSIC_VOL, 0.7f);
            var sfxVol = PlayerPrefs.GetFloat(PREFS_SFX_VOL, 0.8f);
            var ambVol = PlayerPrefs.GetFloat(PREFS_AMB_VOL, 0.5f);

            SetMasterVolume(masterVol);
            SetMusicVolume(musicVol);
            SetSfxVolume(sfxVol);
            SetAmbienceVolume(ambVol);

            if (masterSlider != null)
            {
                masterSlider.value = masterVol;
                masterSlider.RegisterValueChangedCallback(OnMasterVolumeChanged);
            }

            if (musicSlider != null)
            {
                musicSlider.value = musicVol;
                musicSlider.RegisterValueChangedCallback(OnMusicVolumeChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.value = sfxVol;
                sfxSlider.RegisterValueChangedCallback(OnSfxVolumeChanged);
            }

            if (ambienceSlider != null)
            {
                ambienceSlider.value = ambVol;
                ambienceSlider.RegisterValueChangedCallback(OnAmbienceVolumeChanged);
            }
        }

        private void UnbindSliderEvents()
        {
            masterSlider?.UnregisterValueChangedCallback(OnMasterVolumeChanged);
            musicSlider?.UnregisterValueChangedCallback(OnMusicVolumeChanged);
            sfxSlider?.UnregisterValueChangedCallback(OnSfxVolumeChanged);
            ambienceSlider?.UnregisterValueChangedCallback(OnAmbienceVolumeChanged);
        }

        private void OnMasterVolumeChanged(ChangeEvent<float> evt) => SetMasterVolume(evt.newValue);
        private void OnMusicVolumeChanged(ChangeEvent<float> evt) => SetMusicVolume(evt.newValue);
        private void OnSfxVolumeChanged(ChangeEvent<float> evt) => SetSfxVolume(evt.newValue);
        private void OnAmbienceVolumeChanged(ChangeEvent<float> evt) => SetAmbienceVolume(evt.newValue);

        private void SetMasterVolume(float volume)
        {
            var clamped = Mathf.Clamp01(volume);
            AudioListener.volume = clamped;
            PlayerPrefs.SetFloat(PREFS_MASTER_VOL, clamped);

            if (masterValueLabel != null)
            {
                masterValueLabel.text = Mathf.RoundToInt(clamped * 100f) + "%";
            }
        }

        private void SetMusicVolume(float volume)
        {
            var clamped = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PREFS_MUSIC_VOL, clamped);

            if (musicValueLabel != null)
            {
                musicValueLabel.text = Mathf.RoundToInt(clamped * 100f) + "%";
            }
        }

        private void SetSfxVolume(float volume)
        {
            var clamped = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PREFS_SFX_VOL, clamped);

            if (sfxValueLabel != null)
            {
                sfxValueLabel.text = Mathf.RoundToInt(clamped * 100f) + "%";
            }
        }

        private void SetAmbienceVolume(float volume)
        {
            var clamped = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PREFS_AMB_VOL, clamped);

            if (ambienceValueLabel != null)
            {
                ambienceValueLabel.text = Mathf.RoundToInt(clamped * 100f) + "%";
            }
        }

        private void OnRebindForwardClicked() => StartRebindingMove("up", rebindForwardButton);
        private void OnRebindBackwardClicked() => StartRebindingMove("down", rebindBackwardButton);
        private void OnRebindLeftClicked() => StartRebindingMove("left", rebindLeftButton);
        private void OnRebindRightClicked() => StartRebindingMove("right", rebindRightButton);
        private void OnRebindInteractClicked() => StartRebindingAction("Player/Interact", rebindInteractButton);
        private void OnRebindWatchClicked() => StartRebindingAction("Player/Watch", rebindWatchButton);
        private void OnRebindEvidenceClicked() => StartRebindingEvidence();

        private void StartRebindingMove(string direction, Button button)
        {
            if (actionsInstance == null || activeRebindOp != null)
            {
                return;
            }

            var moveAction = actionsInstance.FindAction("Player/Move");
            if (moveAction == null)
            {
                return;
            }

            var bindingIndex = FindBindingIndex(moveAction, direction);
            if (bindingIndex == -1)
            {
                return;
            }

            button.text = "...";
            button.AddToClassList("waiting");

            activeRebindOp = Core.InputSettingsManager.RebindAction(
                moveAction,
                bindingIndex,
                () =>
                {
                    Core.InputSettingsManager.SaveOverrides(actionsInstance);
                    button.RemoveFromClassList("waiting");
                    activeRebindOp = null;
                    UpdateKeymapLabels();
                    PlaySfx(clickSfx);
                },
                () =>
                {
                    button.RemoveFromClassList("waiting");
                    activeRebindOp = null;
                    UpdateKeymapLabels();
                }
            );
        }

        private void StartRebindingAction(string actionPath, Button button)
        {
            if (actionsInstance == null || activeRebindOp != null)
            {
                return;
            }

            var action = actionsInstance.FindAction(actionPath);
            if (action == null)
            {
                return;
            }

            var bindingIndex = FindKeyboardBindingIndex(action);
            if (bindingIndex == -1)
            {
                return;
            }

            button.text = "...";
            button.AddToClassList("waiting");

            activeRebindOp = Core.InputSettingsManager.RebindAction(
                action,
                bindingIndex,
                () =>
                {
                    Core.InputSettingsManager.SaveOverrides(actionsInstance);
                    button.RemoveFromClassList("waiting");
                    activeRebindOp = null;
                    UpdateKeymapLabels();
                    PlaySfx(clickSfx);
                },
                () =>
                {
                    button.RemoveFromClassList("waiting");
                    activeRebindOp = null;
                    UpdateKeymapLabels();
                }
            );
        }

        private void StartRebindingEvidence()
        {
            if (activeRebindOp != null)
            {
                return;
            }

            if (rebindEvidenceButton != null)
            {
                rebindEvidenceButton.text = "...";
                rebindEvidenceButton.AddToClassList("waiting");
            }

            activeRebindOp = Core.InputSettingsManager.RebindEvidenceKey(
                (newPath) =>
                {
                    if (rebindEvidenceButton != null)
                    {
                        rebindEvidenceButton.RemoveFromClassList("waiting");
                    }
                    activeRebindOp = null;
                    UpdateKeymapLabels();
                    PlaySfx(clickSfx);
                },
                () =>
                {
                    if (rebindEvidenceButton != null)
                    {
                        rebindEvidenceButton.RemoveFromClassList("waiting");
                    }
                    activeRebindOp = null;
                    UpdateKeymapLabels();
                }
            );
        }

        private void UpdateKeymapLabels()
        {
            if (actionsInstance == null)
            {
                return;
            }

            var moveAction = actionsInstance.FindAction("Player/Move");
            if (moveAction != null)
            {
                UpdateBindingLabel(moveAction, "up", rebindForwardButton);
                UpdateBindingLabel(moveAction, "down", rebindBackwardButton);
                UpdateBindingLabel(moveAction, "left", rebindLeftButton);
                UpdateBindingLabel(moveAction, "right", rebindRightButton);
            }

            var interactAction = actionsInstance.FindAction("Player/Interact");
            if (interactAction != null)
            {
                UpdateKeyboardBindingLabel(interactAction, rebindInteractButton);
            }

            var watchAction = actionsInstance.FindAction("Player/Watch");
            if (watchAction != null)
            {
                UpdateKeyboardBindingLabel(watchAction, rebindWatchButton);
            }

            if (rebindEvidenceButton != null)
            {
                rebindEvidenceButton.text = Core.InputSettingsManager.GetEvidenceKeyDisplayName();
            }
        }

        private void UpdateBindingLabel(InputAction action, string bindingName, Button button)
        {
            if (button == null)
            {
                return;
            }

            var index = FindBindingIndex(action, bindingName);
            if (index != -1)
            {
                var path = action.bindings[index].effectivePath;
                button.text = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
        }

        private void UpdateKeyboardBindingLabel(InputAction action, Button button)
        {
            if (button == null)
            {
                return;
            }

            var index = FindKeyboardBindingIndex(action);
            if (index != -1)
            {
                var path = action.bindings[index].effectivePath;
                button.text = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
        }

        private int FindBindingIndex(InputAction action, string bindingName, string pathPrefix = "<Keyboard>/")
        {
            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isPartOfComposite && binding.name.Equals(bindingName, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (binding.path.StartsWith(pathPrefix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private int FindKeyboardBindingIndex(InputAction action, string pathPrefix = "<Keyboard>/")
        {
            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (!binding.isComposite && !binding.isPartOfComposite)
                {
                    if (binding.path.StartsWith(pathPrefix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private void RegisterButtonEvents(Button button, System.Action onClickAction)
        {
            if (button == null)
            {
                return;
            }

            button.clicked += onClickAction;
            button.RegisterCallback<PointerEnterEvent>(OnButtonPointerEnter);
        }

        private void UnregisterButtonEvents(Button button, System.Action onClickAction)
        {
            if (button == null)
            {
                return;
            }

            button.clicked -= onClickAction;
            button.UnregisterCallback<PointerEnterEvent>(OnButtonPointerEnter);
        }

        private void OnButtonPointerEnter(PointerEnterEvent evt)
        {
            Debug.Log($"[{nameof(GameMenuController)}] Pointer hover on button: '{((VisualElement)evt.target)?.name}'");
            PlaySfx(hoverSfx);
        }

        private void PlaySfx(AudioClip clip)
        {
            if (sfxAudioSource != null && clip != null)
            {
                sfxAudioSource.PlayOneShot(clip);
            }
        }

        private void AnimateEntrance()
        {
            if (buttonsContainer == null)
            {
                return;
            }

            buttonsContainer.style.opacity = 1f;
        }
    }
}
