using System;
using System.Collections;
using SunnyStop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyStop.Game
{
    /// <summary>
    /// The moment the whole product rests on (CONCEPT.md §6.1).
    ///
    /// Rules this screen must never break:
    ///   * no timer, no forced dwell, no countdown before you may continue;
    ///   * no close button that is really an ad;
    ///   * tapping anywhere continues, so it costs ~2 seconds if you don't care;
    ///   * it never appears when the player has set the tone to Off.
    /// If any of those slip, this becomes the interstitial we replaced.
    /// </summary>
    public sealed class PostcardView : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _backdrop;
        private RectTransform _card;
        private Text _message;
        private Text _caption;
        private Button _keepButton;
        private Text _keepLabel;
        private CanvasGroup _group;

        private Postcard _current;
        private int _currentLevel;
        private Action _onContinue;
        private bool _kept;

        public bool IsShowing => _canvas != null && _canvas.gameObject.activeSelf;

        private void Awake() => BuildUi();

        private void BuildUi()
        {
            _canvas = UiBuilder.CreateCanvas("PostcardCanvas", sortOrder: 100);
            _canvas.transform.SetParent(transform, false);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();

            _backdrop = UiBuilder.Panel(_canvas.transform, "Backdrop",
                                        new Color(0.99f, 0.94f, 0.86f, 0.97f));
            UiBuilder.Stretch(_backdrop);

            // Tapping the backdrop continues - no hunting for a tiny X.
            var backdropButton = _backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(Continue);

            _card = UiBuilder.Panel(_backdrop, "Card", new Color(1f, 0.99f, 0.96f));
            UiBuilder.Place(_card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 80f), new Vector2(860f, 700f));

            var accent = UiBuilder.Panel(_card, "Accent", new Color(0.95f, 0.78f, 0.42f));
            UiBuilder.Place(accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            Vector2.zero, new Vector2(860f, 18f));

            // Explicit ink, never inherited. The browser preview had exactly this
            // bug: the win screen sat outside the themed container, fell back to
            // the default black, and became unreadable on a dark card.
            _message = UiBuilder.Label(_card, "Message", "", 52,
                                       new Color(0.16f, 0.17f, 0.13f));
            UiBuilder.Place(_message.rectTransform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
                            new Vector2(740f, 420f));

            _caption = UiBuilder.Label(_card, "Caption", "", 30,
                                       new Color(0.55f, 0.50f, 0.45f));
            UiBuilder.Place(_caption.rectTransform, new Vector2(0.5f, 0f),
                            new Vector2(0.5f, 0f), new Vector2(0f, 60f),
                            new Vector2(740f, 60f));

            _keepButton = UiBuilder.TextButton(_backdrop, "Keep", "Behalten",
                                               new Color(0.98f, 0.90f, 0.72f),
                                               new Color(0.30f, 0.25f, 0.18f), Keep);
            UiBuilder.Place((RectTransform)_keepButton.transform, new Vector2(0.5f, 0f),
                            new Vector2(1f, 0f), new Vector2(-20f, 260f),
                            new Vector2(380f, 130f));
            _keepLabel = _keepButton.GetComponentInChildren<Text>();

            Button next = UiBuilder.TextButton(_backdrop, "Next", "Weiter",
                                               new Color(0.36f, 0.62f, 0.52f),
                                               Color.white, Continue);
            UiBuilder.Place((RectTransform)next.transform, new Vector2(0.5f, 0f),
                            new Vector2(0f, 0f), new Vector2(20f, 260f),
                            new Vector2(380f, 130f));

            _canvas.gameObject.SetActive(false);
        }

        public void Show(Postcard card, int level, Action onContinue)
        {
            _current = card;
            _currentLevel = level;
            _onContinue = onContinue;
            _kept = false;

            _message.text = card.Text;
            _caption.text = $"Level {level}  ·  {DateTime.Now:dd.MM.yyyy}";
            _keepLabel.text = "Behalten";
            _keepButton.interactable = true;

            _canvas.gameObject.SetActive(true);
            StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            const float duration = 0.32f;
            float elapsed = 0f;
            Vector3 from = new Vector3(0.92f, 0.92f, 1f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _group.alpha = t;
                _card.localScale = Vector3.Lerp(from, Vector3.one, 1f - (1f - t) * (1f - t));
                yield return null;
            }
            _group.alpha = 1f;
            _card.localScale = Vector3.one;
        }

        private void Keep()
        {
            if (_current == null || _kept) return;
            SaveGame.KeepCard(_current.Id, _currentLevel);
            SaveGame.Flush();
            _kept = true;
            _keepLabel.text = "Im Album ✓";
            _keepButton.interactable = false;
        }

        private void Continue()
        {
            if (!IsShowing) return;
            _canvas.gameObject.SetActive(false);
            Action callback = _onContinue;
            _onContinue = null;
            callback?.Invoke();
        }
    }
}
