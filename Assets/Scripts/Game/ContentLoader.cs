using System.Collections.Generic;
using SunnyStop.Core;
using UnityEngine;

namespace SunnyStop.Game
{
    /// <summary>
    /// Loads baked levels and postcards from Resources.
    ///
    /// Levels ship as static JSON so every player worldwide plays the identical,
    /// verified board (CONCEPT.md §5.2). The shipping game swaps Resources for a
    /// versioned Addressables bundle so a re-tuned level can go out without a store
    /// review - the loading interface stays the same.
    /// </summary>
    public static class ContentLoader
    {
        public const string LevelPath = "Levels/level_";
        public const string MessagePath = "Messages/messages.";

        private static readonly Dictionary<int, LevelDefinition> LevelCache = new();
        private static MessageBook _messageBook;

        /// <summary>Level numbers present in the build, ascending.</summary>
        public static List<int> AvailableLevels()
        {
            var found = new List<int>();
            TextAsset[] assets = Resources.LoadAll<TextAsset>("Levels");
            foreach (TextAsset asset in assets)
            {
                // "level_007" -> 7
                int underscore = asset.name.LastIndexOf('_');
                if (underscore < 0) continue;
                if (int.TryParse(asset.name.Substring(underscore + 1), out int number))
                    found.Add(number);
            }
            found.Sort();
            return found;
        }

        public static LevelDefinition Load(int levelNumber)
        {
            if (LevelCache.TryGetValue(levelNumber, out LevelDefinition cached)) return cached;

            string path = LevelPath + levelNumber.ToString("D3");
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogError($"[SunnyStop] level not found: Resources/{path}.json");
                return null;
            }

            LevelDefinition level = LevelLoader.FromJson(asset.text);
            LevelCache[levelNumber] = level;
            return level;
        }

        public static MessageBook Messages(string locale = "de")
        {
            if (_messageBook != null) return _messageBook;

            var asset = Resources.Load<TextAsset>(MessagePath + locale);
            if (asset == null && locale != "de") asset = Resources.Load<TextAsset>(MessagePath + "de");
            if (asset == null)
            {
                Debug.LogError("[SunnyStop] no message book found in Resources/Messages");
                return null;
            }

            _messageBook = MessageBook.FromJson(asset.text);
            return _messageBook;
        }
    }
}
