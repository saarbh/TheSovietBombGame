using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.GameMenu
{
    /// <summary>
    /// Handles ambient visual animations for the in-game menu UI elements while paused.
    /// Uses unscaled time / SetUpdate(true) to animate smoothly when Time.timeScale == 0.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameMenuAnimator : MonoBehaviour
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
        }

        private void OnDisable()
        {
            titlePulseTween?.Kill();
            subtitleGlowTween?.Kill();
        }
    }
}
