using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyStop.Game
{
    /// <summary>
    /// The route: 200 stops on one line, in eight chapters (CONCEPT.md §10).
    ///
    /// Every level is a stop and every stop is reachable, because nothing here
    /// is locked behind a level you have not beaten. That is deliberate: the
    /// design has no lives, no energy and no gates, so a player who is stuck on
    /// 137 can go and play 138 instead of being told to wait or pay
    /// (CONCEPT.md §8). The stop's fill says what you have done with it, never
    /// what you are allowed to do.
    ///
    /// Mirrors tools/webpreview/game.js `showMap`. The stops sit on a serpentine
    /// so the line reads as a route rather than a grid, and the road between
    /// them is drawn as rotated segments — cheap, and it lets the art pass drop
    /// in a real spline without touching the layout maths.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        private const int StopsPerChapter = 25;
        private const float StopPitch = 130f;    // vertical distance between stops
        private const float BandHeight = 92f;    // the chapter heading strip
        private const float StopSize = 96f;
        private const float MilestoneSize = 126f;

        private static readonly Color Ground = new Color(0.914f, 0.906f, 0.882f);
        private static readonly Color Ink = new Color(0.106f, 0.118f, 0.141f);
        private static readonly Color Muted = new Color(0.478f, 0.502f, 0.565f);
        private static readonly Color Road = new Color(0.29f, 0.31f, 0.30f, 0.55f);
        private static readonly Color Surface = new Color(0.992f, 0.988f, 0.980f);
        private static readonly Color Done = new Color(0.227f, 0.498f, 0.388f);
        private static readonly Color Accent = new Color(0.878f, 0.541f, 0.118f);
        private static readonly Color AccentSoft = new Color(0.969f, 0.890f, 0.753f);

        private static readonly string[] ChapterNames =
        {
            "Morgen", "Mittag", "Abend", "Nacht",
            "Regen", "Schnee", "Fest", "Morgengrauen"
        };

        private Canvas _canvas;
        private RectTransform _content;
        private ScrollRect _scroll;
        private Action<int> _onPick;
        private Action _onClose;
        private bool _built;

        public bool IsShowing => _canvas != null && _canvas.gameObject.activeSelf;

        public void Initialise(Action<int> onPick, Action onClose)
        {
            _onPick = onPick;
            _onClose = onClose;
            BuildChrome();
        }

        private void BuildChrome()
        {
            _canvas = UiBuilder.CreateCanvas("MapCanvas", sortOrder: 130);
            _canvas.transform.SetParent(transform, false);

            RectTransform back = UiBuilder.Panel(_canvas.transform, "Ground", Ground);
            UiBuilder.Stretch(back);

            RectTransform head = UiBuilder.Panel(back, "Head",
                                                 new Color(0.463f, 0.514f, 0.596f));
            UiBuilder.Place(head, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            Vector2.zero, new Vector2(1080f, 150f));

            Text title = UiBuilder.Label(head, "Title", "LINIE 200", 44, Color.white,
                                         TextAnchor.MiddleLeft);
            UiBuilder.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(48f, 0f), new Vector2(600f, 60f));
            title.fontStyle = FontStyle.Bold;

            Button close = UiBuilder.TextButton(head, "Close", "Zurück", Surface, Ink,
                                                Close);
            UiBuilder.Place((RectTransform)close.transform, new Vector2(1f, 0.5f),
                            new Vector2(1f, 0.5f), new Vector2(-40f, 0f),
                            new Vector2(240f, 96f));

            RectTransform frame = UiBuilder.Panel(back, "Frame", new Color(0f, 0f, 0f, 0f));
            frame.anchorMin = new Vector2(0f, 0f);
            frame.anchorMax = new Vector2(1f, 1f);
            frame.offsetMin = new Vector2(0f, 0f);
            frame.offsetMax = new Vector2(0f, -150f);

            _content = UiBuilder.ScrollArea(frame, "Scroll", out _scroll);

            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Builds the stops once, then only repaints their state.</summary>
        public void Show(IReadOnlyList<int> levels, int current)
        {
            if (!_built)
            {
                BuildStops(levels);
                _built = true;
            }
            Repaint(current);
            _canvas.gameObject.SetActive(true);
            ScrollTo(levels, current);
        }

        public void Hide() => _canvas.gameObject.SetActive(false);

        private readonly Dictionary<int, RectTransform> _stops =
            new Dictionary<int, RectTransform>();
        private readonly Dictionary<int, Text> _stopLabels = new Dictionary<int, Text>();
        private float _height;

        private void BuildStops(IReadOnlyList<int> levels)
        {
            float y = -40f;
            int chapter = 0;

            for (int i = 0; i < levels.Count; i++)
            {
                int level = levels[i];
                int thisChapter = Mathf.Clamp((level - 1) / StopsPerChapter + 1, 1, 8);
                if (thisChapter != chapter)
                {
                    chapter = thisChapter;
                    y -= BuildBand(chapter, y);
                }

                // Serpentine: the same sine the browser build uses, so a stop
                // sits in the same place in both.
                float amplitude = 300f;
                float x = Mathf.Sin(i * 0.94f) * amplitude;

                if (i > 0 && _lastPoint.HasValue)
                {
                    BuildRoad(_lastPoint.Value, new Vector2(x, y));
                }
                BuildStop(level, x, y);
                _lastPoint = new Vector2(x, y);
                y -= StopPitch;
            }

            _height = Mathf.Abs(y) + 80f;
            _content.sizeDelta = new Vector2(0f, _height);
        }

        private Vector2? _lastPoint;

        private float BuildBand(int chapter, float y)
        {
            RectTransform band = UiBuilder.Panel(_content, $"Band{chapter}",
                                                 new Color(1f, 1f, 1f, 0.55f));
            UiBuilder.Place(band, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, y - 20f), new Vector2(1000f, 76f));

            Text name = UiBuilder.Label(band, "Name",
                                        $"{chapter}. {ChapterNames[chapter - 1]}", 38, Ink,
                                        TextAnchor.MiddleLeft);
            UiBuilder.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(36f, 0f), new Vector2(500f, 50f));
            name.fontStyle = FontStyle.Bold;

            int from = (chapter - 1) * StopsPerChapter + 1;
            Text range = UiBuilder.Label(band, "Range",
                                         $"Level {from}–{chapter * StopsPerChapter}", 28,
                                         Muted, TextAnchor.MiddleRight);
            UiBuilder.Place(range.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(-36f, 0f), new Vector2(420f, 44f));

            _lastPoint = null;   // the road does not run across a chapter heading
            return BandHeight;
        }

        /// <summary>One straight segment of tarmac between two stops.</summary>
        private void BuildRoad(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 1f) return;

            RectTransform seg = UiBuilder.Panel(_content, "Road", Road);
            seg.anchorMin = new Vector2(0.5f, 1f);
            seg.anchorMax = new Vector2(0.5f, 1f);
            seg.pivot = new Vector2(0.5f, 0.5f);
            seg.anchoredPosition = from + delta * 0.5f;
            seg.sizeDelta = new Vector2(length, 26f);
            seg.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            Image image = seg.GetComponent<Image>();
            if (image != null) image.raycastTarget = false;
        }

        private void BuildStop(int level, float x, float y)
        {
            bool milestone = level % StopsPerChapter == 0;
            float size = milestone ? MilestoneSize : StopSize;

            RectTransform stop = UiBuilder.Panel(_content, $"Stop{level}", Surface);
            UiBuilder.Place(stop, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                            new Vector2(x, y), new Vector2(size, size));

            var button = stop.gameObject.AddComponent<Button>();
            button.targetGraphic = stop.GetComponent<Image>();
            int captured = level;
            button.onClick.AddListener(() =>
            {
                Hide();
                if (_onPick != null) _onPick(captured);
            });

            Text label = UiBuilder.Label(stop, "No", level.ToString(),
                                         milestone ? 40 : 32, Ink);
            UiBuilder.Stretch(label.rectTransform);
            if (milestone) label.fontStyle = FontStyle.Bold;

            _stops[level] = stop;
            _stopLabels[level] = label;
        }

        private void Repaint(int current)
        {
            // Read the save once. Asking SaveGame per stop would re-parse the
            // whole cleared list 200 times for one screen.
            var cleared = new HashSet<string>(SaveGame.ClearedLevels());

            foreach (KeyValuePair<int, RectTransform> pair in _stops)
            {
                int level = pair.Key;
                bool milestone = level % StopsPerChapter == 0;
                bool isCleared = cleared.Contains(level.ToString());

                Image face = pair.Value.GetComponent<Image>();
                Text label = _stopLabels[level];

                if (isCleared)
                {
                    if (face != null) face.color = Done;
                    label.color = Color.white;
                }
                else if (milestone)
                {
                    if (face != null) face.color = AccentSoft;
                    label.color = Accent;
                }
                else
                {
                    if (face != null) face.color = Surface;
                    label.color = Ink;
                }

                // Where you are now, over the top of everything else.
                if (level == current)
                {
                    if (face != null) face.color = Accent;
                    label.color = Color.white;
                }
            }
        }

        private void ScrollTo(IReadOnlyList<int> levels, int current)
        {
            int index = -1;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] == current) { index = i; break; }
            }
            if (index < 0 || _height <= 0f) return;

            // Roughly centre the current stop. Exact is not worth it: the list
            // is 200 long and being a stop or two off reads as fine.
            float offset = 40f + index * StopPitch + (index / StopsPerChapter) * BandHeight;
            float t = Mathf.Clamp01(1f - offset / _height);
            _scroll.verticalNormalizedPosition = t;
        }

        private void Close()
        {
            Hide();
            if (_onClose != null) _onClose();
        }
    }
}
