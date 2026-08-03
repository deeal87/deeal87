using System;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyStop.Game
{
    /// <summary>
    /// In-level chrome. Deliberately sparse: no timer, no score, no pop-ups during
    /// play (CONCEPT.md §7.1). The only interruption that may ever appear over the
    /// board is the rewind offer, and that one is a rescue, not a sales pitch.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public event Action HintRequested;
        public event Action UndoRequested;
        public event Action RestartRequested;
        public event Action MenuRequested;

        private Text _levelLabel;
        private Text _queueLabel;
        private RectTransform _banner;
        private Text _bannerText;
        private Button _bannerPrimary;
        private Text _bannerPrimaryLabel;
        private Button _bannerSecondary;
        private Text _bannerSecondaryLabel;
        private Button _undoButton;

        private void Awake() => BuildUi();

        private void BuildUi()
        {
            Canvas canvas = UiBuilder.CreateCanvas("HudCanvas", sortOrder: 10);
            canvas.transform.SetParent(transform, false);

            // Back to the terminal. Top-left, out of the way of the board, and
            // the only chrome above the level number.
            var menu = UiBuilder.TextButton(canvas.transform, "MenuButton", "\u2630",
                                            new Color(0.88f, 0.88f, 0.86f),
                                            new Color(0.30f, 0.28f, 0.25f),
                                            () => MenuRequested?.Invoke());
            UiBuilder.Place((RectTransform)menu.transform, new Vector2(0f, 1f),
                            new Vector2(0f, 1f), new Vector2(40f, -40f),
                            new Vector2(110f, 110f));

            _levelLabel = UiBuilder.Label(canvas.transform, "LevelLabel", "Level 1", 54,
                                          new Color(0.22f, 0.20f, 0.18f));
            UiBuilder.Place(_levelLabel.rectTransform, new Vector2(0.5f, 1f),
                            new Vector2(0.5f, 1f), new Vector2(0f, -70f),
                            new Vector2(600f, 80f));

            _queueLabel = UiBuilder.Label(canvas.transform, "QueueLabel", "", 34,
                                          new Color(0.40f, 0.37f, 0.34f));
            UiBuilder.Place(_queueLabel.rectTransform, new Vector2(0.5f, 1f),
                            new Vector2(0.5f, 1f), new Vector2(0f, -140f),
                            new Vector2(600f, 50f));

            var hint = UiBuilder.TextButton(canvas.transform, "HintButton", "Hinweis",
                                            new Color(0.98f, 0.90f, 0.72f),
                                            new Color(0.30f, 0.25f, 0.18f),
                                            () => HintRequested?.Invoke());
            UiBuilder.Place((RectTransform)hint.transform, new Vector2(0f, 0f),
                            new Vector2(0f, 0f), new Vector2(40f, 60f),
                            new Vector2(300f, 120f));

            _undoButton = UiBuilder.TextButton(canvas.transform, "UndoButton", "Zurück",
                                               new Color(0.88f, 0.88f, 0.86f),
                                               new Color(0.30f, 0.28f, 0.25f),
                                               () => UndoRequested?.Invoke());
            UiBuilder.Place((RectTransform)_undoButton.transform, new Vector2(0.5f, 0f),
                            new Vector2(0.5f, 0f), new Vector2(0f, 60f),
                            new Vector2(300f, 120f));

            var restart = UiBuilder.TextButton(canvas.transform, "RestartButton", "Neu",
                                               new Color(0.88f, 0.88f, 0.86f),
                                               new Color(0.30f, 0.28f, 0.25f),
                                               () => RestartRequested?.Invoke());
            UiBuilder.Place((RectTransform)restart.transform, new Vector2(1f, 0f),
                            new Vector2(1f, 0f), new Vector2(-40f, 60f),
                            new Vector2(300f, 120f));

            _banner = UiBuilder.Panel(canvas.transform, "Banner",
                                      new Color(0.99f, 0.97f, 0.92f, 0.98f));
            UiBuilder.Place(_banner, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 220f), new Vector2(940f, 260f));

            _bannerText = UiBuilder.Label(_banner, "BannerText", "", 40,
                                          new Color(0.26f, 0.23f, 0.20f));
            UiBuilder.Place(_bannerText.rectTransform, new Vector2(0.5f, 1f),
                            new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                            new Vector2(860f, 100f));

            _bannerPrimary = UiBuilder.TextButton(_banner, "BannerPrimary", "",
                                                  new Color(0.36f, 0.62f, 0.52f),
                                                  Color.white, null);
            UiBuilder.Place((RectTransform)_bannerPrimary.transform, new Vector2(0.5f, 0f),
                            new Vector2(1f, 0f), new Vector2(-15f, 30f),
                            new Vector2(400f, 110f));
            _bannerPrimaryLabel = _bannerPrimary.GetComponentInChildren<Text>();

            _bannerSecondary = UiBuilder.TextButton(_banner, "BannerSecondary", "",
                                                    new Color(0.88f, 0.88f, 0.86f),
                                                    new Color(0.30f, 0.28f, 0.25f), null);
            UiBuilder.Place((RectTransform)_bannerSecondary.transform, new Vector2(0.5f, 0f),
                            new Vector2(0f, 0f), new Vector2(15f, 30f),
                            new Vector2(400f, 110f));
            _bannerSecondaryLabel = _bannerSecondary.GetComponentInChildren<Text>();

            HideBanner();
        }

        public void SetLevel(int level, int chapter) =>
            _levelLabel.text = $"Level {level}";

        public void SetQueueInfo(int remaining, int total) =>
            _queueLabel.text = remaining > 0
                ? $"{remaining} von {total} warten noch"
                : "Alle sind eingestiegen";

        public void SetUndoAvailable(bool available) => _undoButton.interactable = available;

        /// <summary>
        /// Shows an offer. Both buttons are optional; whatever is passed as
        /// <paramref name="secondaryCaption"/> is always the "no thanks" side, and it
        /// is always present when the primary action is an offer of help.
        /// </summary>
        public void ShowBanner(string text, string primaryCaption, Action onPrimary,
                               string secondaryCaption = null, Action onSecondary = null)
        {
            _bannerText.text = text;

            _bannerPrimaryLabel.text = primaryCaption;
            _bannerPrimary.onClick.RemoveAllListeners();
            _bannerPrimary.onClick.AddListener(() =>
            {
                HideBanner();
                onPrimary?.Invoke();
            });
            _bannerPrimary.gameObject.SetActive(!string.IsNullOrEmpty(primaryCaption));

            bool hasSecondary = !string.IsNullOrEmpty(secondaryCaption);
            _bannerSecondary.gameObject.SetActive(hasSecondary);
            if (hasSecondary)
            {
                _bannerSecondaryLabel.text = secondaryCaption;
                _bannerSecondary.onClick.RemoveAllListeners();
                _bannerSecondary.onClick.AddListener(() =>
                {
                    HideBanner();
                    onSecondary?.Invoke();
                });
            }

            // A single-button banner is centred rather than shoved to one side.
            var primaryRect = (RectTransform)_bannerPrimary.transform;
            UiBuilder.Place(primaryRect, new Vector2(0.5f, 0f),
                            hasSecondary ? new Vector2(1f, 0f) : new Vector2(0.5f, 0f),
                            hasSecondary ? new Vector2(-15f, 30f) : new Vector2(0f, 30f),
                            new Vector2(400f, 110f));

            _banner.gameObject.SetActive(true);
        }

        public void HideBanner() => _banner.gameObject.SetActive(false);

        public bool IsBannerVisible => _banner != null && _banner.gameObject.activeSelf;
    }
}
