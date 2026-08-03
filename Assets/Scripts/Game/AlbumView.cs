using System;
using System.Collections.Generic;
using SunnyStop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyStop.Game
{
    /// <summary>
    /// The postcards the player chose to keep.
    ///
    /// This is the only collection in the game, and it is worth being precise
    /// about what it is not: there is nothing to complete, no set to finish, no
    /// counter urging you toward all 240. You keep a card because you wanted to
    /// keep it. The screen therefore shows no "12 / 240" anywhere — that number
    /// would turn a keepsake into a chore (CONCEPT.md §6, §8).
    ///
    /// Mirrors tools/webpreview/game.js `showAlbum`: warm cards on the dark
    /// terminal ground, newest first, each captioned with where and when it was
    /// earned rather than with the card's internal category.
    /// </summary>
    public sealed class AlbumView : MonoBehaviour
    {
        private static readonly Color Void = new Color(0.027f, 0.043f, 0.071f);
        private static readonly Color Rail = new Color(0.114f, 0.169f, 0.239f);
        private static readonly Color Led = new Color(1f, 0.702f, 0f);
        private static readonly Color Ink = new Color(0.910f, 0.933f, 0.965f);
        private static readonly Color Dim = new Color(0.478f, 0.553f, 0.643f);
        private static readonly Color Card = new Color(0.992f, 0.988f, 0.980f);
        private static readonly Color CardInk = new Color(0.106f, 0.118f, 0.141f);
        private static readonly Color Accent = new Color(0.878f, 0.541f, 0.118f);

        private Canvas _canvas;
        private RectTransform _content;
        private MessageBook _book;
        private Action _onClose;

        public bool IsShowing => _canvas != null && _canvas.gameObject.activeSelf;

        public void Initialise(MessageBook book, Action onClose)
        {
            _book = book;
            _onClose = onClose;
            BuildChrome();
        }

        private void BuildChrome()
        {
            _canvas = UiBuilder.CreateCanvas("AlbumCanvas", sortOrder: 140);
            _canvas.transform.SetParent(transform, false);

            RectTransform back = UiBuilder.Panel(_canvas.transform, "Night", Void);
            UiBuilder.Stretch(back);

            Text title = UiBuilder.Label(back, "Title", "POSTKARTEN", 40, Led,
                                         TextAnchor.MiddleLeft);
            UiBuilder.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(48f, -80f), new Vector2(600f, 60f));
            title.fontStyle = FontStyle.Bold;

            Button close = UiBuilder.TextButton(back, "Close", "Zurück", Rail, Ink, Close);
            UiBuilder.Place((RectTransform)close.transform, new Vector2(1f, 1f),
                            new Vector2(1f, 1f), new Vector2(-40f, -50f),
                            new Vector2(240f, 96f));

            RectTransform frame = UiBuilder.Panel(back, "Frame", new Color(0f, 0f, 0f, 0f));
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(0f, 0f);
            frame.offsetMax = new Vector2(0f, -150f);

            ScrollRect scroll;
            _content = UiBuilder.ScrollArea(frame, "Scroll", out scroll);

            _canvas.gameObject.SetActive(false);
        }

        public void Show()
        {
            Rebuild();
            _canvas.gameObject.SetActive(true);
        }

        public void Hide() => _canvas.gameObject.SetActive(false);

        private void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Destroy(_content.GetChild(i).gameObject);
            }

            List<SaveGame.KeptCard> kept = SaveGame.KeptCards();
            float y = -30f;

            if (kept.Count == 0)
            {
                RectTransform empty = UiBuilder.Panel(_content, "Empty",
                                                      new Color(0f, 0f, 0f, 0f));
                UiBuilder.Place(empty, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                new Vector2(0f, y), new Vector2(960f, 260f));
                Text note = UiBuilder.Label(empty, "Note",
                    "Noch keine Karte behalten.\n\nNach einem gewonnenen Level auf "
                    + "„Behalten“ tippen — dann liegt sie hier.",
                    34, Dim);
                UiBuilder.Stretch(note.rectTransform, 40f);
                _content.sizeDelta = new Vector2(0f, 320f);
                return;
            }

            // Newest first: the one you just kept is the one you want to re-read.
            for (int i = kept.Count - 1; i >= 0; i--)
            {
                SaveGame.KeptCard entry = kept[i];
                Postcard card = _book != null ? _book.ById(entry.Id) : null;
                if (card == null) continue;
                y -= BuildCard(card, entry, y) + 24f;
            }

            _content.sizeDelta = new Vector2(0f, Mathf.Abs(y) + 60f);
        }

        /// <summary>Draws one card and returns the height it used.</summary>
        private float BuildCard(Postcard card, SaveGame.KeptCard entry, float y)
        {
            // Two lines of quote is the common case; a long one gets three. The
            // shipping build measures the text properly with a ContentSizeFitter
            // on a prefab - this is the runtime-built stand-in.
            int lines = Mathf.Clamp(card.Text.Length / 42 + 1, 1, 4);
            float height = 150f + lines * 46f;

            RectTransform panel = UiBuilder.Panel(_content, "Card", Card);
            UiBuilder.Place(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, y), new Vector2(960f, height));

            RectTransform stripe = UiBuilder.Panel(panel, "Stripe", Accent);
            UiBuilder.Place(stripe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            Vector2.zero, new Vector2(960f, 10f));

            Text quote = UiBuilder.Label(panel, "Quote",
                                         "„" + card.Text + "“", 38, CardInk,
                                         TextAnchor.UpperLeft);
            UiBuilder.Place(quote.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -46f), new Vector2(880f, lines * 46f + 10f));

            // Where and when it was earned - something the player recognises.
            // The card's own Category ("playful") is system vocabulary.
            var caption = new List<string>();
            if (entry.Level > 0) caption.Add($"Level {entry.Level}");
            if (entry.HasWhen) caption.Add(entry.When.ToString("d. MMMM yyyy"));
            Text who = UiBuilder.Label(panel, "Who",
                                       caption.Count > 0
                                           ? string.Join(" · ", caption)
                                           : "Behalten",
                                       24, Dim, TextAnchor.LowerLeft);
            UiBuilder.Place(who.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(40f, 28f), new Vector2(500f, 32f));

            return height;
        }

        private void Close()
        {
            Hide();
            if (_onClose != null) _onClose();
        }
    }
}
