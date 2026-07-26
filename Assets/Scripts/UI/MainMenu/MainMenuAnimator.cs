using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.MainMenu
{
    /// <summary>
    /// Handles ambient visual animations for the main menu UI elements.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuAnimator : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float pulseScale = 1.03f;
        [SerializeField] private float pulseDuration = 2.0f;

        private UIDocument uiDocument;
        private Label titleLabel;
        private Label subtitleLabel;
        private Tween titlePulseTween;
        private Tween subtitleGlowTween;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            titleLabel = root.Q<Label>("title-label");
            subtitleLabel = root.Q<Label>("subtitle-label");

            StartTitlePulse();
            StartSubtitleGlow();
        }

        private void OnDisable()
        {
            titlePulseTween?.Kill();
            subtitleGlowTween?.Kill();
        }

        private void StartTitlePulse()
        {
            if (titleLabel == null)
            {
                return;
            }

            var startScale = new Vector2(1f, 1f);
            var targetScale = new Vector2(pulseScale, pulseScale);

            titlePulseTween = DOTween.To(() => startScale,
                                         x => titleLabel.style.scale = new Scale(x),
                                         targetScale,
                                         pulseDuration)
                                     .SetEase(Ease.InOutSine)
                                     .SetLoops(-1, LoopType.Yoyo)
                                     .SetUpdate(true);
        }

        private void StartSubtitleGlow()
        {
            if (subtitleLabel == null)
            {
                return;
            }

            var startColor = new Color(0.83f, 0.63f, 0.09f, 0.8f);
            var endColor = new Color(1.0f, 0.8f, 0.2f, 1.0f);

            subtitleGlowTween = DOTween.To(() => startColor,
                                           x => subtitleLabel.style.color = x,
                                           endColor,
                                           pulseDuration * 1.5f)
                                       .SetEase(Ease.InOutQuad)
                                       .SetLoops(-1, LoopType.Yoyo)
                                       .SetUpdate(true);
        }
    }
}
