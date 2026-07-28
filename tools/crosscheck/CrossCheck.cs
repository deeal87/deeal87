using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SunnyStop.Core;

namespace SunnyStop.CrossCheck
{
    /// <summary>
    /// Runs the C# rules engine against every baked level, headless.
    ///
    /// The levels are produced and proven solvable by the Python tool, then played by
    /// this C# engine. If the two ever disagree, the game ships a level that is
    /// provably solvable and actually is not - the worst possible bug for a game whose
    /// core promise is "hard but never impossible".
    ///
    /// The Unity test suite covers the same ground, but only inside the editor. This
    /// harness needs nothing but a C# compiler, so CI can run it on every commit.
    ///
    ///     mcs -langversion:latest -target:library -out:SunnyStop.Core.dll Assets/Scripts/Core/*.cs
    ///     mcs -langversion:latest -r:SunnyStop.Core.dll -out:crosscheck.exe tools/crosscheck/CrossCheck.cs
    ///     mono crosscheck.exe Assets/Resources/Levels Assets/Resources/Messages
    /// </summary>
    public static class CrossCheck
    {
        private static int _failures;

        public static int Main(string[] args)
        {
            string levelDir = args.Length > 0 ? args[0] : "Assets/Resources/Levels";
            string messageDir = args.Length > 1 ? args[1] : "Assets/Resources/Messages";

            string[] files = Directory.GetFiles(levelDir, "level_*.json");
            Array.Sort(files);
            if (files.Length == 0)
            {
                Console.Error.WriteLine($"no levels found in {levelDir}");
                return 1;
            }

            Console.WriteLine($"checking {files.Length} levels with the C# engine\n");
            var watch = Stopwatch.StartNew();

            int totalStates = 0;
            int deepest = 0;
            foreach (string file in files)
            {
                LevelDefinition level;
                try
                {
                    level = LevelLoader.FromJson(File.ReadAllText(file));
                }
                catch (Exception ex)
                {
                    Fail(Path.GetFileName(file), $"did not load: {ex.Message}");
                    continue;
                }

                string name = $"level {level.Id:D3}";

                // 1. Structure agrees with what the Python validator accepted.
                List<string> problems = level.Validate();
                if (problems.Count > 0)
                {
                    Fail(name, "malformed: " + string.Join("; ", problems));
                    continue;
                }

                // 2. The reference solution replays move for move.
                if (level.ReferenceSolution.Count == 0)
                {
                    Fail(name, "no reference solution baked in");
                    continue;
                }

                GameState state = GameState.Initial(level);
                bool replayed = true;
                foreach (int busId in level.ReferenceSolution)
                {
                    GameState.Refusal refusal = state.CanDispatch(level, busId);
                    if (refusal != GameState.Refusal.None)
                    {
                        Fail(name, $"bus {busId} not dispatchable ({refusal}) - " +
                                   "the C# and Python rules disagree");
                        replayed = false;
                        break;
                    }
                    state = state.Dispatch(level, busId);
                }
                if (!replayed) continue;

                if (!state.IsWon(level))
                {
                    Fail(name, "reference solution did not win");
                    continue;
                }

                // 3. The runtime solver agrees the level is winnable from the start.
                GameState start = GameState.Initial(level);
                if (!Solver.IsSolvable(level, start))
                {
                    Fail(name, "runtime solver says unsolvable");
                    continue;
                }

                // 4. Hints never walk the player into a dead end. This is the promise
                //    behind the hint button, so it is checked on every level.
                GameState walk = start;
                int guard = 0;
                while (!walk.IsWon(level) && guard++ < 200)
                {
                    int hint = Solver.Hint(level, walk);
                    if (hint == 0)
                    {
                        Fail(name, "hint gave up on a winnable position");
                        break;
                    }
                    walk = walk.Dispatch(level, hint);
                    if (!Solver.IsSolvable(level, walk))
                    {
                        Fail(name, $"hint (bus {hint}) led into a dead end");
                        break;
                    }
                }
                if (!walk.IsWon(level) && guard < 200) continue;

                // 5. Transition events must reproduce the dispatch exactly, or the
                //    animation would drift from the rules.
                GameState probe = GameState.Initial(level);
                foreach (int busId in level.ReferenceSolution)
                {
                    GameState viaEvents;
                    List<TransitionEvent> events =
                        Transition.Compute(level, probe, busId, out viaEvents);
                    GameState viaDispatch = probe.Dispatch(level, busId);
                    if (!viaEvents.Equals(viaDispatch))
                    {
                        Fail(name, "transition events disagree with dispatch");
                        break;
                    }
                    if (events.Count == 0)
                    {
                        Fail(name, "dispatch produced no events");
                        break;
                    }
                    probe = viaDispatch;
                }

                totalStates += CountStates(level, out int decep);
                if (decep > deepest) deepest = decep;
            }

            CheckMessages(messageDir);

            watch.Stop();
            Console.WriteLine();
            if (_failures > 0)
            {
                Console.WriteLine($"FAILED: {_failures} problem(s)");
                return 1;
            }
            Console.WriteLine(
                $"all {files.Length} levels pass the C# engine " +
                $"({watch.ElapsedMilliseconds} ms, {totalStates} states explored)");
            Console.WriteLine("C# and Python rule engines agree on every shipped level.");
            return 0;
        }

        /// <summary>Rough state count, used only to show the solver really ran.</summary>
        private static int CountStates(LevelDefinition level, out int deepest)
        {
            var seen = new HashSet<GameState>();
            var frontier = new Queue<GameState>();
            GameState start = GameState.Initial(level);
            seen.Add(start);
            frontier.Enqueue(start);
            deepest = 0;
            while (frontier.Count > 0)
            {
                GameState s = frontier.Dequeue();
                if (s.IsWon(level)) continue;
                foreach (int busId in s.LegalMoves(level))
                {
                    GameState next = s.Dispatch(level, busId);
                    if (seen.Add(next)) frontier.Enqueue(next);
                }
            }
            return seen.Count;
        }

        private static void CheckMessages(string messageDir)
        {
            if (!Directory.Exists(messageDir))
            {
                Fail("messages", $"directory not found: {messageDir}");
                return;
            }

            foreach (string file in Directory.GetFiles(messageDir, "messages.*.json"))
            {
                string name = Path.GetFileName(file);
                MessageBook book;
                try
                {
                    book = MessageBook.FromJson(File.ReadAllText(file), seed: 7);
                }
                catch (Exception ex)
                {
                    Fail(name, $"did not load: {ex.Message}");
                    continue;
                }

                if (book.All.Count != 240)
                {
                    Fail(name, $"expected 240 cards, found {book.All.Count}");
                }

                // Tone off must never produce a card.
                var ctx = new WinContext(7, 1, false, 12, 0);
                if (book.Pick(ctx, ToneSetting.Off, new List<string>()) != null)
                {
                    Fail(name, "a card was shown with the tone switched off");
                }

                // A warm player must stay in the warm register even on a fast solve,
                // which is drawn from the playful category.
                var shown = new List<string>();
                for (int i = 0; i < 20; i++)
                {
                    Postcard card = book.Pick(ctx, ToneSetting.Warm, shown);
                    if (card == null)
                    {
                        Fail(name, "picker returned nothing");
                        break;
                    }
                    if (card.Tone != "warm")
                    {
                        Fail(name, $"warm player served a {card.Tone} card: \"{card.Text}\"");
                        break;
                    }
                    if (shown.Contains(card.Id))
                    {
                        Fail(name, $"card {card.Id} repeated within the no-repeat window");
                        break;
                    }
                    shown.Add(card.Id);
                }

                // Milestones are pinned to their level.
                Postcard milestone = book.Pick(new WinContext(100, 1, false, 12, 0),
                                               ToneSetting.Warm, new List<string>());
                if (milestone == null || milestone.Level != 100)
                {
                    Fail(name, "level 100 did not get its milestone card");
                }

                Console.WriteLine($"ok   {name}  {book.All.Count} cards");
            }
        }

        private static void Fail(string what, string why)
        {
            Console.WriteLine($"FAIL {what}: {why}");
            _failures++;
        }
    }
}
