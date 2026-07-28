using System;
using System.Collections.Generic;
using System.Linq;

namespace SunnyStop.Core
{
    public sealed class Postcard
    {
        public string Id { get; }
        public string Category { get; }
        public string Tone { get; }
        public string Text { get; }
        /// <summary>Milestone cards are pinned to one level; 0 for everything else.</summary>
        public int Level { get; }

        public Postcard(string id, string category, string tone, string text, int level)
        {
            Id = id;
            Category = category;
            Tone = tone;
            Text = text;
            Level = level;
        }
    }

    /// <summary>Player tone preference. <see cref="Off"/> means show no card at all.</summary>
    public enum ToneSetting
    {
        Warm,
        Playful,
        Quiet,
        Off
    }

    /// <summary>What the player just did. Decides which register the card speaks in.</summary>
    public readonly struct WinContext
    {
        public readonly int Level;
        public readonly int Attempts;
        public readonly bool Skipped;
        public readonly int HourOfDay;
        public readonly int DaysSinceLastPlay;

        public WinContext(int level, int attempts, bool skipped, int hourOfDay,
                          int daysSinceLastPlay)
        {
            Level = level;
            Attempts = attempts;
            Skipped = skipped;
            HourOfDay = hourOfDay;
            DaysSinceLastPlay = daysSinceLastPlay;
        }
    }

    /// <summary>
    /// The postcards shown instead of an interstitial ad (CONCEPT.md §6).
    ///
    /// Two rules matter more than the selection cleverness:
    ///   1. never repeat a card the player has seen recently, and
    ///   2. never show one the player asked not to see.
    /// A kind word that arrives twice, or after being switched off, stops being kind.
    /// </summary>
    public sealed class MessageBook
    {
        public const int NoRepeatWindow = 60;

        public string Locale { get; }
        public IReadOnlyList<Postcard> All { get; }

        private readonly Random _random;

        public MessageBook(string locale, IReadOnlyList<Postcard> cards, int seed = 0)
        {
            Locale = locale;
            All = cards;
            _random = seed == 0 ? new Random() : new Random(seed);
        }

        public static MessageBook FromJson(string json, int seed = 0)
        {
            JsonValue root = JsonValue.Parse(json);
            var cards = new List<Postcard>();
            foreach (JsonValue m in root["messages"].Items())
            {
                cards.Add(new Postcard(
                    m["id"].AsString(),
                    m["category"].AsString(),
                    m.StringOr("tone", "warm"),
                    m["text"].AsString(),
                    m.IntOr("level", 0)));
            }
            if (cards.Count == 0) throw new FormatException("message book is empty");
            return new MessageBook(root.StringOr("locale", "de"), cards, seed);
        }

        /// <summary>Which register fits what just happened.</summary>
        public static string CategoryFor(WinContext ctx)
        {
            if (ctx.Level % 25 == 0) return "milestone";
            if (ctx.DaysSinceLastPlay >= 3) return "return";
            if (ctx.Skipped) return "gentle";
            if (ctx.Attempts >= 3) return "recognition";
            if (ctx.HourOfDay >= 22 || ctx.HourOfDay < 4) return "grounding";
            return "playful";
        }

        /// <summary>
        /// Pick a card. Returns null when the player has the tone switched off.
        /// <paramref name="recentIds"/> is the ring buffer of the last cards shown,
        /// most recent last.
        /// </summary>
        public Postcard Pick(WinContext ctx, ToneSetting tone, IReadOnlyList<string> recentIds)
        {
            if (tone == ToneSetting.Off) return null;

            string category = CategoryFor(ctx);

            // Milestone cards are pinned to their level and always win.
            if (category == "milestone")
            {
                Postcard pinned = All.FirstOrDefault(c => c.Level == ctx.Level);
                if (pinned != null) return pinned;
                category = "recognition";
            }

            var recent = new HashSet<string>(
                recentIds?.Skip(Math.Max(0, (recentIds?.Count ?? 0) - NoRepeatWindow))
                ?? Enumerable.Empty<string>());

            string wanted = tone.ToString().ToLowerInvariant();

            // Widen the net only as far as needed: exact category+tone, then the
            // category in any tone, then anything at all.
            Postcard choice =
                Choose(All.Where(c => c.Level == 0 && c.Category == category && c.Tone == wanted), recent)
                ?? Choose(All.Where(c => c.Level == 0 && c.Category == category), recent)
                ?? Choose(All.Where(c => c.Level == 0 && c.Tone == wanted), recent)
                ?? Choose(All.Where(c => c.Level == 0), recent);

            if (choice != null) return choice;

            // Everything has been seen recently: fall back to the least recently
            // shown card rather than repeating the one from a moment ago.
            var pool = All.Where(c => c.Level == 0).ToList();
            if (pool.Count == 0) return All[0];
            return pool
                .OrderBy(c => IndexOfLastShow(recentIds, c.Id))
                .First();
        }

        private Postcard Choose(IEnumerable<Postcard> candidates, HashSet<string> recent)
        {
            var pool = candidates.Where(c => !recent.Contains(c.Id)).ToList();
            if (pool.Count == 0) return null;
            return pool[_random.Next(pool.Count)];
        }

        private static int IndexOfLastShow(IReadOnlyList<string> recentIds, string id)
        {
            if (recentIds == null) return -1;
            for (int i = recentIds.Count - 1; i >= 0; i--)
            {
                if (recentIds[i] == id) return i;
            }
            return -1;
        }
    }
}
