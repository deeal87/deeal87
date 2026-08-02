using System;
using System.Collections.Generic;
using SunnyStop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyStop.Game
{
    /// <summary>
    /// The start screen: the terminal at night, seen from outside.
    ///
    /// It deliberately does NOT share the board's warm daylight palette. The
    /// board is the dispatcher's desk in the morning; this is the place before
    /// you get there, so it is dark, and stepping into a level reads as a
    /// change of place rather than a change of screen.
    ///
    /// Structure mirrors the browser preview exactly (tools/webpreview/game.js,
    /// showMenu) so the two builds cannot drift:
    ///   * an LED destination blind carrying the title,
    ///   * two facts read off the player's own save,
    ///   * one departure call - the single obvious thing to do,
    ///   * three secondary rows: the route map, the album, the message tone,
    ///   * a progress strip.
    ///
    /// Every row here goes somewhere real. Nothing on this screen is a button
    /// for a feature that does not exist.
    /// </summary>
    public sealed class MenuView : MonoBehaviour
    {
        // Night terminal palette. Named, not inherited: a plain mid-grey would
        // read as unconsidered, and these are pulled toward the amber/cyan pair
        // that carries the whole screen.
        private static readonly Color Void = new Color(0.027f, 0.043f, 0.071f);   // #070B12
        private static readonly Color Panel = new Color(0.055f, 0.086f, 0.133f);  // #0E1622
        private static readonly Color Led = new Color(1f, 0.702f, 0f);            // #FFB300
        private static readonly Color Beam = new Color(0.208f, 0.878f, 0.816f);   // #35E0D0
        private static readonly Color Rail = new Color(0.114f, 0.169f, 0.239f);   // #1D2B3D
        private static readonly Color Dim = new Color(0.478f, 0.553f, 0.643f);    // #7A8DA4
        private static readonly Color Ink = new Color(0.910f, 0.933f, 0.965f);    // #E8EEF6
        private static readonly Color LedInk = new Color(0.102f, 0.071f, 0.024f); // #1A1206

        private Canvas _canvas;
        private Text _goLabel;
        private Text _goKicker;
        private Text _toneValue;
        private Text _albumNote;
        private Text _albumNo;
        private Text _mapNote;
        private Text _legendLeft;
        private Text _legendRight;
        private Text _briefChapter;
        private Text _briefMilestone;
        private RectTransform _meterFill;

        private Action<int> _onPlay;
        private Action _onMap;
        private Action _onAlbum;

        public bool IsShowing => _canvas != null && _canvas.gameObject.activeSelf;

        /// <param name="onPlay">Start or resume at this level number.</param>
        /// <param name="onMap">Open the route map. Null hides the row.</param>
        /// <param name="onAlbum">Open the postcard album.</param>
        public void Initialise(Action<int> onPlay, Action onMap, Action onAlbum)
        {
            _onPlay = onPlay;
            _onMap = onMap;
            _onAlbum = onAlbum;
            BuildUi();
        }

        private void BuildUi()
        {
            _canvas = UiBuilder.CreateCanvas("MenuCanvas", sortOrder: 120);
            _canvas.transform.SetParent(transform, false);

            RectTransform back = UiBuilder.Panel(_canvas.transform, "Night", Void);
            UiBuilder.Stretch(back);

            // ---- the destination blind ----
            RectTransform bezel = UiBuilder.Panel(back, "Bezel", Panel);
            UiBuilder.Place(bezel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -120f), new Vector2(940f, 250f));

            RectTransform screen = UiBuilder.Panel(bezel, "Screen",
                                                   new Color(0.02f, 0.031f, 0.051f));
            UiBuilder.Stretch(screen, 18f);

            // A real dot matrix needs a texture; the prototype settles for wide
            // amber capitals, and the art pass replaces this with the lamp grid
            // the browser build draws on canvas.
            Text title = UiBuilder.Label(screen, "Title", "S U N N Y   S T O P", 96, Led);
            UiBuilder.Stretch(title.rectTransform);
            title.fontStyle = FontStyle.Bold;

            Text lineNo = UiBuilder.Label(back, "LineNo", "LINIE 200", 26, Dim,
                                          TextAnchor.MiddleLeft);
            UiBuilder.Place(lineNo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(-250f, -388f), new Vector2(440f, 34f));

            Text status = UiBuilder.Label(back, "Status", "TERMINAL · BETRIEB", 26, Beam,
                                          TextAnchor.MiddleRight);
            UiBuilder.Place(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(250f, -388f), new Vector2(440f, 34f));

            // ---- two facts from the save ----
            _briefChapter = BuildBriefCell(back, "BriefChapter", "ABSCHNITT", -240f);
            _briefMilestone = BuildBriefCell(back, "BriefMilestone",
                                             "NÄCHSTER MEILENSTEIN", 240f);

            // ---- the departure call ----
            RectTransform go = UiBuilder.Panel(back, "Go", Led);
            UiBuilder.Place(go, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -600f), new Vector2(940f, 150f));
            var goButton = go.gameObject.AddComponent<Button>();
            goButton.targetGraphic = go.GetComponent<Image>();
            goButton.onClick.AddListener(Play);

            _goKicker = UiBuilder.Label(go, "Kicker", "ABFAHRT", 24,
                                        new Color(0.102f, 0.071f, 0.024f, 0.72f),
                                        TextAnchor.LowerLeft);
            UiBuilder.Place(_goKicker.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(48f, 22f), new Vector2(600f, 30f));

            _goLabel = UiBuilder.Label(go, "Label", "Losfahren", 52, LedInk,
                                       TextAnchor.UpperLeft);
            UiBuilder.Place(_goLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(48f, -14f), new Vector2(760f, 60f));
            _goLabel.fontStyle = FontStyle.Bold;

            // ---- secondary destinations ----
            //
            // Rows are laid out by index over the rows that actually exist, so a
            // destination this build has not got yet is simply absent. A row
            // that does nothing when tapped is worse than no row: it promises a
            // screen that is not there.
            float y = -770f;
            const float pitch = 130f;

            if (_onMap != null)
            {
                _mapNote = BuildRow(back, "RowMap", "200", "Alle Haltestellen", "", y,
                                    _onMap, out Text mapNo);
                y -= pitch;
            }
            if (_onAlbum != null)
            {
                _albumNote = BuildRow(back, "RowAlbum", "000", "Postkarten-Album", "", y,
                                      _onAlbum, out _albumNo);
                y -= pitch;
            }
            _toneValue = BuildRow(back, "RowTone", "TON", "Worte nach dem Sieg", "", y,
                                  CycleTone, out Text toneNo);

            // ---- progress ----
            RectTransform track = UiBuilder.Panel(back, "MeterTrack",
                                                  new Color(1f, 1f, 1f, 0.09f));
            UiBuilder.Place(track, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 118f), new Vector2(940f, 8f));
            _meterFill = UiBuilder.Panel(track, "MeterFill", Led);
            _meterFill.anchorMin = new Vector2(0f, 0f);
            _meterFill.anchorMax = new Vector2(0f, 1f);
            _meterFill.pivot = new Vector2(0f, 0.5f);
            _meterFill.anchoredPosition = Vector2.zero;
            _meterFill.sizeDelta = new Vector2(0f, 0f);

            _legendLeft = UiBuilder.Label(back, "LegendLeft", "", 24, Dim,
                                          TextAnchor.MiddleLeft);
            UiBuilder.Place(_legendLeft.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(-250f, 82f), new Vector2(440f, 32f));
            _legendRight = UiBuilder.Label(back, "LegendRight", "", 24, Dim,
                                           TextAnchor.MiddleRight);
            UiBuilder.Place(_legendRight.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(250f, 82f), new Vector2(440f, 32f));

            _canvas.gameObject.SetActive(false);
        }

        private Text BuildBriefCell(Transform parent, string name, string label, float x)
        {
            RectTransform cell = UiBuilder.Panel(parent, name, Rail);
            UiBuilder.Place(cell, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(x, -440f), new Vector2(460f, 110f));

            Text caption = UiBuilder.Label(cell, "Caption", label, 22, Dim,
                                           TextAnchor.UpperLeft);
            UiBuilder.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(24f, -16f), new Vector2(420f, 28f));

            Text value = UiBuilder.Label(cell, "Value", "", 34, Ink, TextAnchor.LowerLeft);
            UiBuilder.Place(value.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(24f, 18f), new Vector2(420f, 40f));
            value.fontStyle = FontStyle.Bold;
            return value;
        }

        /// <summary>One board row. Returns its right-hand note label.</summary>
        private Text BuildRow(Transform parent, string name, string no, string title,
                              string note, float y, Action onClick, out Text noLabel)
        {
            RectTransform row = UiBuilder.Panel(parent, name, Rail);
            UiBuilder.Place(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, y), new Vector2(940f, 116f));
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row.GetComponent<Image>();
            if (onClick != null) button.onClick.AddListener(() => onClick());

            noLabel = UiBuilder.Label(row, "No", no, 34, Led, TextAnchor.MiddleLeft);
            UiBuilder.Place(noLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(30f, 0f), new Vector2(150f, 44f));
            noLabel.fontStyle = FontStyle.Bold;

            Text name2 = UiBuilder.Label(row, "Name", title, 34, Ink, TextAnchor.MiddleLeft);
            UiBuilder.Place(name2.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(190f, 0f), new Vector2(520f, 44f));
            name2.fontStyle = FontStyle.Bold;

            Text noteLabel = UiBuilder.Label(row, "Note", note, 24, Dim,
                                             TextAnchor.MiddleRight);
            UiBuilder.Place(noteLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(-30f, 0f), new Vector2(320f, 40f));
            return noteLabel;
        }

        // ----- data ------------------------------------------------------------ //

        public void Show()
        {
            Refresh();
            _canvas.gameObject.SetActive(true);
        }

        public void Hide() => _canvas.gameObject.SetActive(false);

        private void Refresh()
        {
            int resume = Mathf.Clamp(SaveGame.HighestLevelReached, 1, 200);
            int cleared = SaveGame.ClearedCount();
            List<string> album = SaveGame.Album();
            bool isNew = cleared == 0 && resume == 1;

            _goKicker.text = isNew ? "ERSTE FAHRT" : "ABFAHRT";
            _goLabel.text = isNew ? "Losfahren" : $"Weiter · Level {resume}";

            int chapter = Mathf.Clamp((resume - 1) / 25 + 1, 1, 8);
            _briefChapter.text = $"{chapter} · {ChapterNames[chapter - 1]}";
            _briefMilestone.text = $"Level {Mathf.Min(200, Mathf.CeilToInt(resume / 25f) * 25)}";

            if (_mapNote != null) _mapNote.text = $"{cleared} GELÖST";
            if (_albumNo != null) _albumNo.text = album.Count.ToString("000");
            if (_albumNote != null) _albumNote.text = album.Count > 0 ? "ÖFFNEN" : "NOCH LEER";
            _toneValue.text = ToneLabel(SaveGame.Tone);

            _legendLeft.text = $"{cleared} / 200 GELÖST";
            _legendRight.text = $"{album.Count} POSTKARTEN";

            // sizeDelta.x stays 0; the anchors do the work, so the bar scales
            // with the canvas rather than with a hard-coded pixel width.
            _meterFill.anchorMax = new Vector2(Mathf.Clamp01(cleared / 200f), 1f);
        }

        private void Play()
        {
            int resume = Mathf.Clamp(SaveGame.HighestLevelReached, 1, 200);
            Hide();
            if (_onPlay != null) _onPlay(resume);
        }

        private void CycleTone()
        {
            ToneSetting[] order =
            {
                ToneSetting.Warm, ToneSetting.Playful, ToneSetting.Quiet, ToneSetting.Off
            };
            int i = Array.IndexOf(order, SaveGame.Tone);
            SaveGame.Tone = order[(i + 1) % order.Length];
            SaveGame.Flush();
            _toneValue.text = ToneLabel(SaveGame.Tone);
        }

        private static string ToneLabel(ToneSetting tone)
        {
            switch (tone)
            {
                case ToneSetting.Playful: return "VERSPIELT";
                case ToneSetting.Quiet: return "STILL";
                case ToneSetting.Off: return "AUS";
                default: return "WARM";
            }
        }

        private static readonly string[] ChapterNames =
        {
            "Morgen", "Mittag", "Abend", "Nacht",
            "Regen", "Schnee", "Fest", "Morgengrauen"
        };
    }
}
