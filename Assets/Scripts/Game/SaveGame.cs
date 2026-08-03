using System;
using System.Collections.Generic;
using SunnyStop.Core;
using UnityEngine;

namespace SunnyStop.Game
{
    /// <summary>
    /// Player progress. PlayerPrefs is fine for the prototype; the shipping game moves
    /// this to a JSON file plus cloud save so nobody loses 200 levels of postcards
    /// with a lost phone (CONCEPT.md §9.3).
    ///
    /// Note what is deliberately NOT stored: lives, energy, timers, streaks. There is
    /// nothing here that can be spent, lost, or used to pressure the player.
    /// </summary>
    public static class SaveGame
    {
        private const string KeyHighest = "ss.highestLevel";
        private const string KeyAttempts = "ss.attempts.";
        private const string KeySkipped = "ss.skipped.";
        private const string KeyRecentCards = "ss.recentCards";
        private const string KeyAlbum = "ss.album";
        private const string KeyCleared = "ss.cleared";
        private const string KeyTone = "ss.tone";
        private const string KeyColorMode = "ss.colorMode";
        private const string KeyLastPlayed = "ss.lastPlayed";

        public static int HighestLevelReached
        {
            get => PlayerPrefs.GetInt(KeyHighest, 1);
            set
            {
                if (value > HighestLevelReached) PlayerPrefs.SetInt(KeyHighest, value);
            }
        }

        public static int AttemptsOn(int level) => PlayerPrefs.GetInt(KeyAttempts + level, 0);

        public static void RecordAttempt(int level) =>
            PlayerPrefs.SetInt(KeyAttempts + level, AttemptsOn(level) + 1);

        public static void ClearAttempts(int level) =>
            PlayerPrefs.DeleteKey(KeyAttempts + level);

        public static bool WasSkipped(int level) =>
            PlayerPrefs.GetInt(KeySkipped + level, 0) == 1;

        public static void MarkSkipped(int level) =>
            PlayerPrefs.SetInt(KeySkipped + level, 1);

        public static ToneSetting Tone
        {
            get => (ToneSetting)PlayerPrefs.GetInt(KeyTone, (int)ToneSetting.Warm);
            set => PlayerPrefs.SetInt(KeyTone, (int)value);
        }

        public static Palette.Mode ColorMode
        {
            get => (Palette.Mode)PlayerPrefs.GetInt(KeyColorMode, (int)Palette.Mode.Default);
            set
            {
                PlayerPrefs.SetInt(KeyColorMode, (int)value);
                Palette.ColorMode = value;
            }
        }

        public static List<string> RecentCards() => Split(PlayerPrefs.GetString(KeyRecentCards, ""));

        public static void PushRecentCard(string id)
        {
            List<string> recent = RecentCards();
            recent.Add(id);
            // Keep a little more than the no-repeat window so the fallback ordering
            // still has something to work with.
            int overflow = recent.Count - (MessageBook.NoRepeatWindow * 2);
            if (overflow > 0) recent.RemoveRange(0, overflow);
            PlayerPrefs.SetString(KeyRecentCards, string.Join(",", recent));
        }

        /// <summary>
        /// One kept postcard: which card, and where and when it was earned.
        ///
        /// The level matters because <see cref="Postcard.Level"/> is the card's
        /// own milestone binding (0 for most cards), not the level the player
        /// was on. Captioning the album from that would label almost every card
        /// with nothing.
        /// </summary>
        public readonly struct KeptCard
        {
            public readonly string Id;
            public readonly int Level;
            public readonly DateTime When;

            public KeptCard(string id, int level, DateTime when)
            {
                Id = id;
                Level = level;
                When = when;
            }

            public bool HasWhen => When != default(DateTime);
        }

        /// <summary>Raw entries. Prefer <see cref="KeptCards"/>.</summary>
        public static List<string> Album() => Split(PlayerPrefs.GetString(KeyAlbum, ""));

        /// <summary>
        /// Kept cards, oldest first. Entries are "id|level|ticks"; a bare "id"
        /// is an older save and still loads, because a returning player must
        /// not lose their album to a storage change.
        /// </summary>
        public static List<KeptCard> KeptCards() => ParseKept(Album());

        /// <summary>
        /// Pure parse, separated from PlayerPrefs so it can be tested. This is
        /// the code path that silently eats a returning player's album if it
        /// ever regresses, which is worth a test rather than a hope.
        /// </summary>
        public static List<KeptCard> ParseKept(IEnumerable<string> rawEntries)
        {
            var cards = new List<KeptCard>();
            if (rawEntries == null) return cards;

            foreach (string raw in rawEntries)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string[] parts = raw.Split('|');
                int level = 0;
                DateTime when = default(DateTime);
                if (parts.Length > 1) int.TryParse(parts[1], out level);
                if (parts.Length > 2)
                {
                    long ticks;
                    if (long.TryParse(parts[2], out ticks)
                        && ticks >= DateTime.MinValue.Ticks
                        && ticks <= DateTime.MaxValue.Ticks)
                    {
                        when = new DateTime(ticks);
                    }
                }
                cards.Add(new KeptCard(parts[0], level, when));
            }
            return cards;
        }

        public static void KeepCard(string id, int level)
        {
            List<string> album = Album();
            foreach (string raw in album)
            {
                if (raw.Split('|')[0] == id) return;
            }
            album.Add($"{id}|{level}|{DateTime.Now.Ticks}");
            PlayerPrefs.SetString(KeyAlbum, string.Join(",", album));
        }

        /// <summary>Whole days since the last session, for the "welcome back" card.</summary>
        public static int DaysSinceLastPlay()
        {
            string stored = PlayerPrefs.GetString(KeyLastPlayed, "");
            if (string.IsNullOrEmpty(stored)) return 0;
            if (!DateTime.TryParse(stored, null,
                                   System.Globalization.DateTimeStyles.RoundtripKind,
                                   out DateTime last))
                return 0;
            return Mathf.Max(0, (int)(DateTime.UtcNow.Date - last.Date).TotalDays);
        }

        public static void TouchLastPlayed() =>
            PlayerPrefs.SetString(KeyLastPlayed, DateTime.UtcNow.ToString("o"));

        public static void Flush() => PlayerPrefs.Save();

        /// <summary>
        /// Levels actually won, as a set of ids.
        ///
        /// Deliberately not derived from <see cref="HighestLevelReached"/>: a
        /// skipped level advances the furthest-reached marker without ever being
        /// solved, so counting from it would tell the player on the menu that
        /// they had cleared levels they had not.
        /// </summary>
        public static List<string> ClearedLevels() => Split(PlayerPrefs.GetString(KeyCleared, ""));

        public static int ClearedCount() => ClearedLevels().Count;

        public static bool IsCleared(int level) =>
            ClearedLevels().Contains(level.ToString());

        public static void MarkCleared(int level)
        {
            string id = level.ToString();
            List<string> cleared = ClearedLevels();
            if (cleared.Contains(id)) return;
            cleared.Add(id);
            PlayerPrefs.SetString(KeyCleared, string.Join(",", cleared));
        }

        private static List<string> Split(string raw) =>
            string.IsNullOrEmpty(raw)
                ? new List<string>()
                : new List<string>(raw.Split(','));
    }
}
