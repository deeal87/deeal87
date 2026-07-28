using System.Collections.Generic;
using SunnyStop.Core;
using SunnyStop.Game;
using UnityEditor;
using UnityEngine;

namespace SunnyStop.EditorTools
{
    /// <summary>
    /// Project configuration and in-editor content checks.
    ///
    /// The repo ships only ProjectVersion.txt and the package manifest, so Unity
    /// generates the rest of ProjectSettings with defaults on first open. That keeps
    /// the repository free of enormous machine-written YAML that nobody can review,
    /// at the cost of one menu click to apply the settings that actually matter.
    ///
    /// Everything here is idempotent - run it as often as you like.
    /// </summary>
    public static class SunnyStopSetup
    {
        private const string Menu = "Sunny Stop/";

        [MenuItem(Menu + "Apply Project Settings", priority = 0)]
        public static void ApplyProjectSettings()
        {
            PlayerSettings.companyName = "Sunny Stop";
            PlayerSettings.productName = "Sunny Stop";
            PlayerSettings.applicationIdentifier = "com.sunnystop.game";

            // Portrait only: the board is laid out for a phone held upright.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Linear colour keeps the warm daylight look consistent across devices.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Android 8.0 and iOS 15, per CONCEPT.md.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.iOS.targetOSVersionString = "15.0";

            AssetDatabase.SaveAssets();
            Debug.Log("[Sunny Stop] Project settings applied: portrait, linear colour, " +
                      "Android API 26+, iOS 15+.");
        }

        [MenuItem(Menu + "Check Content", priority = 20)]
        public static void CheckContent()
        {
            List<int> levels = ContentLoader.AvailableLevels();
            if (levels.Count == 0)
            {
                Debug.LogError("[Sunny Stop] No levels found. Expected them in " +
                               "Assets/Resources/Levels/.");
                return;
            }

            int unsolvable = 0;
            int malformed = 0;
            foreach (int number in levels)
            {
                LevelDefinition level = ContentLoader.Load(number);
                if (level == null)
                {
                    malformed++;
                    continue;
                }
                if (level.Validate().Count > 0)
                {
                    Debug.LogError($"[Sunny Stop] Level {number} is malformed.");
                    malformed++;
                    continue;
                }
                if (!Solver.IsSolvable(level, GameState.Initial(level)))
                {
                    Debug.LogError($"[Sunny Stop] Level {number} is NOT solvable.");
                    unsolvable++;
                }
            }

            MessageBook book = ContentLoader.Messages();
            int cards = book == null ? 0 : book.All.Count;

            if (unsolvable == 0 && malformed == 0)
            {
                Debug.Log($"[Sunny Stop] {levels.Count} levels all solvable, " +
                          $"{cards} postcards loaded.");
            }
            else
            {
                Debug.LogError($"[Sunny Stop] {malformed} malformed, {unsolvable} unsolvable. " +
                               "Re-run tools/check.sh.");
            }
        }

        [MenuItem(Menu + "Reset Progress", priority = 21)]
        public static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog(
                    "Reset progress?",
                    "Clears the saved level, the postcard album and all settings on this " +
                    "machine. Useful for testing the opening chapter.",
                    "Reset", "Cancel"))
            {
                return;
            }
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Sunny Stop] Progress reset.");
        }
    }
}
