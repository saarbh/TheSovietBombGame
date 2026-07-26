using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace UI.MainMenu
{
    /// <summary>
    /// Controls UI interaction, audio settings, keymap display, loading screen overlay, and scene transitions for Petrov.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        private const string PREFS_MASTER_VOL = "Petrov_MasterVolume";
        private const string PREFS_MUSIC_VOL = "Petrov_MusicVolume";
        private const string PREFS_SFX_VOL = "Petrov_SfxVolume";
        private const string PREFS_AMB_VOL = "Petrov_AmbienceVolume";

        [Header("Scene Config")]
        [SerializeField] private string targetSceneName = "SampleScene";

        [Header("Audio References")]
        [SerializeField] private AudioSource musicAudioSource;
        [SerializeField] private AudioSource ambAudioSource;
        [SerializeField] private AudioSource sfxAudioSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip hoverSfx;
        [SerializeField] private AudioClip clickSfx;

        [Header("Input Config")]
        [SerializeField] private InputActionAsset inputActionsAsset;

        private UIDocument uiDocument;
        private Button startButton;
        private Button optionsButton;
        private Button quitButton;
        private Button optionsBackButton;
        private VisualElement buttonsContainer;
        private VisualElement optionsModal;
        private VisualElement loadingOverlay;
        private VisualElement loadingProgressFill;
        private Label loadingStatusLabel;
        private Label loadingPercentLabel;

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

        private AsyncOperation currentAsyncOp;
        private bool isTransitioning;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            if (inputActionsAsset != null)
            {
                actionsInstance = Instantiate(inputActionsAsset);
                Core.InputSettingsManager.LoadOverrides(actionsInstance);
            }
        }

        private void Start()
        {
            EnsureAudioPlaying();
        }

        private void OnEnable()
        {
            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            buttonsContainer = root.Q<VisualElement>("buttons-container");
            optionsModal = root.Q<VisualElement>("options-modal");
            loadingOverlay = root.Q<VisualElement>("loading-overlay");
            loadingProgressFill = root.Q<VisualElement>("loading-progress-fill");
            loadingStatusLabel = root.Q<Label>("loading-status-label");
            loadingPercentLabel = root.Q<Label>("loading-percent-label");

            startButton = root.Q<Button>("start-button");
            optionsButton = root.Q<Button>("options-button");
            quitButton = root.Q<Button>("quit-button");
            optionsBackButton = root.Q<Button>("options-back-button");

            masterSlider = root.Q<Slider>("master-volume-slider");
            musicSlider = root.Q<Slider>("music-volume-slider");
            sfxSlider = root.Q<Slider>("sfx-volume-slider");
            ambienceSlider = root.Q<Slider>("ambience-volume-slider");

            masterValueLabel = root.Q<Label>("master-volume-value");
            musicValueLabel = root.Q<Label>("music-volume-value");
            sfxValueLabel = root.Q<Label>("sfx-volume-value");
            ambienceValueLabel = root.Q<Label>("ambience-volume-value");

            rebindForwardButton = root.Q<Button>("rebind-forward-btn");
            rebindBackwardButton = root.Q<Button>("rebind-backward-btn");
            rebindLeftButton = root.Q<Button>("rebind-left-btn");
            rebindRightButton = root.Q<Button>("rebind-right-btn");
            rebindInteractButton = root.Q<Button>("rebind-interact-btn");
            rebindWatchButton = root.Q<Button>("rebind-watch-btn");
            rebindEvidenceButton = root.Q<Button>("rebind-evidence-btn");

            RegisterButtonEvents(startButton, OnStartClicked);
            RegisterButtonEvents(optionsButton, OnOptionsClicked);
            RegisterButtonEvents(quitButton, OnQuitClicked);
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
            AnimateEntrance();
        }

        private void OnDisable()
        {
            if (activeRebindOp != null)
            {
                activeRebindOp.Cancel();
            }

            UnregisterButtonEvents(startButton, OnStartClicked);
            UnregisterButtonEvents(optionsButton, OnOptionsClicked);
            UnregisterButtonEvents(quitButton, OnQuitClicked);
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

        private void EnsureAudioPlaying()
        {
            if (musicAudioSource != null && !musicAudioSource.isPlaying)
            {
                musicAudioSource.Play();
            }

            if (ambAudioSource != null && !ambAudioSource.isPlaying)
            {
                ambAudioSource.Play();
            }
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

        private void OnMasterVolumeChanged(ChangeEvent<float> evt)
        {
            SetMasterVolume(evt.newValue);
        }

        private void OnMusicVolumeChanged(ChangeEvent<float> evt)
        {
            SetMusicVolume(evt.newValue);
        }

        private void OnSfxVolumeChanged(ChangeEvent<float> evt)
        {
            SetSfxVolume(evt.newValue);
        }

        private void OnAmbienceVolumeChanged(ChangeEvent<float> evt)
        {
            SetAmbienceVolume(evt.newValue);
        }

        private void SetMasterVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PREFS_MASTER_VOL, volume);
            if (masterValueLabel != null)
            {
                masterValueLabel.text = Mathf.RoundToInt(volume * 100f) + "%";
            }
        }

        private void SetMusicVolume(float volume)
        {
            var clamped = Mathf.Clamp01(volume);
            if (musicAudioSource != null)
            {
                musicAudioSource.volume = clamped;
            }

            PlayerPrefs.SetFloat(PREFS_MUSIC_VOL, clamped);
            if (musicValueLabel != null)
            {
                musicValueLabel.text = Mathf.RoundToInt(clamped * 100f) + "%";
            }
        }

        private void SetSfxVolume(float volume)
        {
            var clamped = Mathf.Clamp01(volume);
            if (sfxAudioSource != null)
            {
                sfxAudioSource.volume = clamped;
            }

            PlayerPrefs.SetFloat(PREFS_SFX_VOL, clamped);
            if (sfxValueLabel != null)
            {
                sfxValueLabel.text = Mathf.RoundToInt(clamped * 100f) + "%";
            }
        }

        private void SetAmbienceVolume(float volume)
        {
            var clamped = Mathf.Clamp01(volume);
            if (ambAudioSource != null)
            {
                ambAudioSource.volume = clamped;
            }

            PlayerPrefs.SetFloat(PREFS_AMB_VOL, clamped);
            if (ambienceValueLabel != null)
            {
                ambienceValueLabel.text = Mathf.RoundToInt(clamped * 100f) + "%";
            }
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
            PlaySfx(hoverSfx);
        }

        private void OnStartClicked()
        {
            if (isTransitioning)
            {
                return;
            }

            isTransitioning = true;
            PlaySfx(clickSfx);

            StartMissionWithLoadingScreenAsync().Forget();
        }

        private void OnOptionsClicked()
        {
            if (isTransitioning)
            {
                return;
            }

            PlaySfx(clickSfx);

            if (buttonsContainer != null)
            {
                buttonsContainer.AddToClassList("hidden");
            }

            if (optionsModal != null)
            {
                optionsModal.RemoveFromClassList("hidden");
            }
        }

        private void OnOptionsBackClicked()
        {
            PlaySfx(clickSfx);

            if (optionsModal != null)
            {
                optionsModal.AddToClassList("hidden");
            }

            if (buttonsContainer != null)
            {
                buttonsContainer.RemoveFromClassList("hidden");
            }
        }

        private void OnQuitClicked()
        {
            if (isTransitioning)
            {
                return;
            }

            PlaySfx(clickSfx);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private async UniTaskVoid StartMissionWithLoadingScreenAsync()
        {
            if (loadingOverlay != null)
            {
                loadingOverlay.RemoveFromClassList("hidden");
            }

            currentAsyncOp = SceneManager.LoadSceneAsync(targetSceneName);
            currentAsyncOp.allowSceneActivation = false;

            var statusMessages = new string[]
            {
                "ESTABLISHING SECURE PROTOCOLS...",
                "CALIBRATING RADAR TELEMETRY...",
                "INITIALIZING BUNKER POWER SYSTEM...",
                "SYNCHRONIZING PETROV DEFENSE ARRAYS...",
                "MISSION READY."
            };

            var cancellationToken = this.GetCancellationTokenOnDestroy();

            while (!currentAsyncOp.isDone)
            {
                var progress = Mathf.Clamp01(currentAsyncOp.progress / 0.9f);

                if (loadingProgressFill != null)
                {
                    loadingProgressFill.style.width = Length.Percent(progress * 100f);
                }

                if (loadingPercentLabel != null)
                {
                    loadingPercentLabel.text = Mathf.RoundToInt(progress * 100f) + "%";
                }

                if (loadingStatusLabel != null)
                {
                    var msgIndex = Mathf.Clamp(Mathf.FloorToInt(progress * statusMessages.Length), 0, statusMessages.Length - 1);
                    loadingStatusLabel.text = statusMessages[msgIndex];
                }

                if (currentAsyncOp.progress >= 0.9f)
                {
                    if (loadingProgressFill != null)
                    {
                        loadingProgressFill.style.width = Length.Percent(100f);
                    }

                    if (loadingPercentLabel != null)
                    {
                        loadingPercentLabel.text = "100%";
                    }

                    if (loadingStatusLabel != null)
                    {
                        loadingStatusLabel.text = "MISSION READY.";
                    }

                    await UniTask.Delay(System.TimeSpan.FromSeconds(0.4f), ignoreTimeScale: true, cancellationToken: cancellationToken);

                    // Lock cursor for gameplay before scene activation
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;

                    currentAsyncOp.allowSceneActivation = true;
                    break;
                }

                await UniTask.Delay(30, ignoreTimeScale: true, cancellationToken: cancellationToken);
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

            buttonsContainer.style.opacity = 0f;
            DOTween.To(() => buttonsContainer.style.opacity.value,
                       x => buttonsContainer.style.opacity = x,
                       1f,
                       0.6f)
                   .SetEase(Ease.OutCubic)
                   .SetUpdate(true);
        }
    }
}
