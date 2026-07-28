using System.Collections.Generic;
using UnityEngine;

namespace SunnyStop.Game
{
    /// <summary>
    /// Colour is the game's core signal, so it can never be the *only* signal
    /// (CONCEPT.md §7.4). Every colour carries a distinct glyph that appears on both
    /// the bus and the passenger, and the palettes are swappable for the two most
    /// common forms of colour blindness.
    /// </summary>
    public static class Palette
    {
        public enum Mode
        {
            Default,
            Deuteranopia,
            Tritanopia
        }

        public static Mode ColorMode = Mode.Default;

        private static readonly Dictionary<string, Color> DefaultColors = new()
        {
            { "red", new Color(0.91f, 0.35f, 0.33f) },
            { "blue", new Color(0.31f, 0.55f, 0.85f) },
            { "green", new Color(0.42f, 0.72f, 0.42f) },
            { "yellow", new Color(0.95f, 0.78f, 0.30f) },
            { "purple", new Color(0.63f, 0.47f, 0.80f) },
            { "orange", new Color(0.93f, 0.58f, 0.31f) },
            { "pink", new Color(0.93f, 0.60f, 0.72f) },
            { "teal", new Color(0.31f, 0.74f, 0.72f) },
        };

        // Deuteranopia: red/green separated by lightness and hue distance instead.
        private static readonly Dictionary<string, Color> DeuteranopiaColors = new()
        {
            { "red", new Color(0.85f, 0.33f, 0.42f) },
            { "blue", new Color(0.20f, 0.44f, 0.82f) },
            { "green", new Color(0.55f, 0.80f, 0.95f) },
            { "yellow", new Color(0.97f, 0.85f, 0.30f) },
            { "purple", new Color(0.44f, 0.25f, 0.62f) },
            { "orange", new Color(0.98f, 0.65f, 0.20f) },
            { "pink", new Color(0.96f, 0.72f, 0.82f) },
            { "teal", new Color(0.10f, 0.28f, 0.36f) },
        };

        private static readonly Dictionary<string, Color> TritanopiaColors = new()
        {
            { "red", new Color(0.88f, 0.28f, 0.30f) },
            { "blue", new Color(0.35f, 0.68f, 0.78f) },
            { "green", new Color(0.30f, 0.55f, 0.36f) },
            { "yellow", new Color(0.94f, 0.62f, 0.60f) },
            { "purple", new Color(0.55f, 0.30f, 0.52f) },
            { "orange", new Color(0.80f, 0.45f, 0.22f) },
            { "pink", new Color(0.98f, 0.80f, 0.86f) },
            { "teal", new Color(0.18f, 0.38f, 0.42f) },
        };

        /// <summary>Redundant, non-colour identity for each bus colour.</summary>
        private static readonly Dictionary<string, string> Glyphs = new()
        {
            { "red", "★" },
            { "blue", "●" },
            { "green", "▲" },
            { "yellow", "◆" },
            { "purple", "♥" },
            { "orange", "■" },
            { "pink", "✿" },
            { "teal", "▼" },
        };

        public static Color Of(string colorName)
        {
            Dictionary<string, Color> table = ColorMode switch
            {
                Mode.Deuteranopia => DeuteranopiaColors,
                Mode.Tritanopia => TritanopiaColors,
                _ => DefaultColors
            };
            return table.TryGetValue(colorName, out Color c) ? c : Color.gray;
        }

        public static string GlyphOf(string colorName) =>
            Glyphs.TryGetValue(colorName, out string g) ? g : "?";

        // Chapter backdrops: morning yellow through to dawn (CONCEPT.md §10).
        private static readonly Color[] ChapterSky =
        {
            new Color(0.99f, 0.93f, 0.80f),
            new Color(0.85f, 0.93f, 0.99f),
            new Color(0.99f, 0.86f, 0.75f),
            new Color(0.24f, 0.26f, 0.42f),
            new Color(0.72f, 0.78f, 0.82f),
            new Color(0.93f, 0.96f, 0.99f),
            new Color(0.98f, 0.88f, 0.93f),
            new Color(0.96f, 0.85f, 0.72f),
        };

        public static Color SkyForChapter(int chapter) =>
            ChapterSky[Mathf.Clamp(chapter - 1, 0, ChapterSky.Length - 1)];
    }
}
