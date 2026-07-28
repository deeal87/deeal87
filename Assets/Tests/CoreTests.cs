using System.Collections.Generic;
using NUnit.Framework;
using SunnyStop.Core;
using SunnyStop.Game;
using UnityEngine;

namespace SunnyStop.Tests
{
    /// <summary>
    /// Mirrors tools/leveltool/test_rules.py. The Python rules engine and this C# one
    /// must agree, because levels are verified by the former and played by the latter -
    /// a divergence between them means shipping a level that is provably solvable and
    /// actually is not.
    /// </summary>
    public class RulesTests
    {
        private static LevelDefinition TinyLevel(int bays = 2, IReadOnlyList<Bus> buses = null,
                                                 IReadOnlyList<Passenger> queue = null,
                                                 IReadOnlyList<Cell> blocked = null)
        {
            const int w = 4, h = 4;
            buses ??= new[]
            {
                new Bus(1, "red", new[] { new Cell(1, 0), new Cell(1, 1) }, Facing.Up, 3, w, h),
                new Bus(2, "blue", new[] { new Cell(1, 2), new Cell(1, 3) }, Facing.Up, 3, w, h)
            };
            queue ??= new[]
            {
                new Passenger("red"), new Passenger("red"), new Passenger("red"),
                new Passenger("blue"), new Passenger("blue"), new Passenger("blue")
            };
            return new LevelDefinition(999, 1, w, h, bays, buses, queue,
                                       blocked ?? new Cell[0], null, new int[0]);
        }

        [Test]
        public void ExitPathRunsToTheBorder()
        {
            LevelDefinition level = TinyLevel();
            Assert.AreEqual(0, level.BusById(1).ExitPath.Count);
            Assert.AreEqual(2, level.BusById(2).ExitPath.Count);
        }

        [Test]
        public void ValidateAcceptsAGoodLevel()
        {
            Assert.IsEmpty(TinyLevel().Validate());
        }

        [Test]
        public void ValidateRejectsOverlappingBuses()
        {
            const int w = 4, h = 4;
            LevelDefinition level = TinyLevel(buses: new[]
            {
                new Bus(1, "red", new[] { new Cell(1, 0), new Cell(1, 1) }, Facing.Up, 3, w, h),
                new Bus(2, "blue", new[] { new Cell(1, 1), new Cell(1, 2) }, Facing.Up, 3, w, h)
            });
            Assert.IsTrue(level.Validate().Exists(p => p.Contains("overlaps")));
        }

        [Test]
        public void ValidateRejectsAnUnfillableQueue()
        {
            LevelDefinition level = TinyLevel(queue: new[]
            {
                new Passenger("red"), new Passenger("red"), new Passenger("red"),
                new Passenger("red"), new Passenger("red"),
                new Passenger("blue"), new Passenger("blue"), new Passenger("blue")
            });
            Assert.IsTrue(level.Validate().Exists(p => p.Contains("not enough red seats")));
        }

        [Test]
        public void BlockedBusCannotBeDispatched()
        {
            LevelDefinition level = TinyLevel();
            GameState state = GameState.Initial(level);
            CollectionAssert.AreEqual(new[] { 1 }, state.LegalMoves(level));
            Assert.AreEqual(GameState.Refusal.Blocked, state.CanDispatch(level, 2));
        }

        [Test]
        public void DispatchUnblocksTheBusBehind()
        {
            LevelDefinition level = TinyLevel();
            GameState state = GameState.Initial(level).Dispatch(level, 1);
            CollectionAssert.AreEqual(new[] { 2 }, state.LegalMoves(level));
        }

        [Test]
        public void OnlyTheHeadOfTheQueueBoardsAndBoardingCascades()
        {
            const int w = 4, h = 4;
            LevelDefinition level = TinyLevel(buses: new[]
            {
                new Bus(1, "blue", new[] { new Cell(0, 0), new Cell(0, 1) }, Facing.Up, 3, w, h),
                new Bus(2, "red", new[] { new Cell(2, 0), new Cell(2, 1) }, Facing.Up, 3, w, h)
            });

            // Blue docks first, but the queue starts with red - nobody boards.
            GameState state = GameState.Initial(level).Dispatch(level, 1);
            Assert.AreEqual(0, state.QueueIndex);
            Assert.AreEqual("blue", state.Bays[0].Color);

            // Red arrives: reds board and fill it, red leaves, blue is now the head and
            // is already docked, so the whole queue clears in one chain.
            state = state.Dispatch(level, 2);
            Assert.AreEqual(6, state.QueueIndex);
            Assert.IsTrue(state.IsWon(level));
        }

        [Test]
        public void LeftmostFreeBayIsUsed()
        {
            const int w = 4, h = 4;
            LevelDefinition level = TinyLevel(
                bays: 3,
                buses: new[]
                {
                    new Bus(1, "blue", new[] { new Cell(0, 0), new Cell(0, 1) }, Facing.Up, 3, w, h),
                    new Bus(2, "green", new[] { new Cell(2, 0), new Cell(2, 1) }, Facing.Up, 3, w, h),
                    new Bus(3, "red", new[] { new Cell(3, 0), new Cell(3, 1) }, Facing.Up, 3, w, h)
                },
                queue: new[]
                {
                    new Passenger("red"), new Passenger("red"), new Passenger("red"),
                    new Passenger("blue"), new Passenger("blue"), new Passenger("blue"),
                    new Passenger("green"), new Passenger("green"), new Passenger("green")
                });

            GameState state = GameState.Initial(level).Dispatch(level, 1).Dispatch(level, 2);
            Assert.AreEqual("blue", state.Bays[0].Color);
            Assert.AreEqual("green", state.Bays[1].Color);
            Assert.IsTrue(state.Bays[2].IsFree);
        }

        [Test]
        public void DispatchWithoutAFreeBayIsRefused()
        {
            const int w = 4, h = 4;
            LevelDefinition level = TinyLevel(
                bays: 1,
                buses: new[]
                {
                    new Bus(1, "blue", new[] { new Cell(0, 0), new Cell(0, 1) }, Facing.Up, 3, w, h),
                    new Bus(2, "green", new[] { new Cell(2, 0), new Cell(2, 1) }, Facing.Up, 3, w, h),
                    new Bus(3, "red", new[] { new Cell(3, 0), new Cell(3, 1) }, Facing.Up, 3, w, h)
                },
                queue: new[]
                {
                    new Passenger("red"), new Passenger("red"), new Passenger("red"),
                    new Passenger("blue"), new Passenger("blue"), new Passenger("blue"),
                    new Passenger("green"), new Passenger("green"), new Passenger("green")
                });

            GameState state = GameState.Initial(level).Dispatch(level, 1);
            Assert.AreEqual(GameState.Refusal.NoFreeBay, state.CanDispatch(level, 2));
            Assert.IsEmpty(state.LegalMoves(level));
        }

        [Test]
        public void CommittingTheLastBayToTheWrongColourIsDeath()
        {
            const int w = 4, h = 4;
            LevelDefinition level = TinyLevel(
                bays: 1,
                buses: new[]
                {
                    new Bus(1, "green", new[] { new Cell(0, 0), new Cell(0, 1) }, Facing.Up, 3, w, h),
                    new Bus(2, "red", new[] { new Cell(2, 0), new Cell(2, 1) }, Facing.Up, 3, w, h)
                },
                queue: new[]
                {
                    new Passenger("red"), new Passenger("red"), new Passenger("red"),
                    new Passenger("green"), new Passenger("green"), new Passenger("green")
                });

            GameState state = GameState.Initial(level).Dispatch(level, 1);
            Assert.IsTrue(state.IsDead(level));
            Assert.IsFalse(state.IsWon(level));

            // ...and the solver knows the level itself was fine; the move was not.
            Assert.IsTrue(Solver.IsSolvable(level, GameState.Initial(level)));
            Assert.IsFalse(Solver.IsSolvable(level, state));
        }

        [Test]
        public void TransitionEventsMatchTheRealDispatch()
        {
            // Guards the one place where presentation could drift from the rules.
            LevelDefinition level = TinyLevel();
            GameState start = GameState.Initial(level);

            List<TransitionEvent> events =
                Transition.Compute(level, start, 1, out GameState afterEvents);
            GameState afterDispatch = start.Dispatch(level, 1);

            Assert.AreEqual(afterDispatch, afterEvents);
            Assert.AreEqual(TransitionEventKind.BusDispatched, events[0].Kind);
            Assert.AreEqual(3, events.FindAll(
                e => e.Kind == TransitionEventKind.PassengerBoarded).Count);
            Assert.AreEqual(1, events.FindAll(
                e => e.Kind == TransitionEventKind.BusDeparted).Count);
        }

        [Test]
        public void StateEqualityIgnoresHowYouGotThere()
        {
            LevelDefinition level = TinyLevel();
            GameState a = GameState.Initial(level).Dispatch(level, 1);
            GameState b = GameState.Initial(level).Dispatch(level, 1);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }

    public class JsonTests
    {
        [Test]
        public void ParsesTheShapesTheLevelFormatUses()
        {
            JsonValue v = JsonValue.Parse(
                "{\"a\":1,\"b\":[[0,2],[3,4]],\"c\":\"x\",\"d\":true,\"e\":null,\"f\":-1.5}");
            Assert.AreEqual(1, v["a"].AsInt());
            Assert.AreEqual(2, v["b"][0][1].AsInt());
            Assert.AreEqual("x", v["c"].AsString());
            Assert.IsTrue(v["d"].AsBool());
            Assert.IsTrue(v["e"].IsNull);
            Assert.AreEqual(-2, v["f"].AsInt()); // -1.5 rounds away from zero
            Assert.AreEqual(7, v.IntOr("missing", 7));
        }

        [Test]
        public void HandlesEscapesAndUmlauts()
        {
            JsonValue v = JsonValue.Parse("{\"t\":\"Sch\\u00f6n \\\"gut\\\"\\ngemacht\"}");
            Assert.AreEqual("Schön \"gut\"\ngemacht", v["t"].AsString());
        }

        [Test]
        public void RejectsMalformedInput()
        {
            Assert.Throws<System.FormatException>(() => JsonValue.Parse("{\"a\":}"));
            Assert.Throws<System.FormatException>(() => JsonValue.Parse("[1,2"));
        }
    }

    public class BakedLevelTests
    {
        [Test]
        public void EveryShippedLevelLoadsAndIsSolvable()
        {
            List<int> levels = ContentLoader.AvailableLevels();
            Assert.IsNotEmpty(levels, "no levels found in Resources/Levels");

            foreach (int number in levels)
            {
                LevelDefinition level = ContentLoader.Load(number);
                Assert.IsNotNull(level, $"level {number} failed to load");
                Assert.IsEmpty(level.Validate(), $"level {number} is malformed");

                GameState start = GameState.Initial(level);
                Assert.IsTrue(Solver.IsSolvable(level, start),
                              $"level {number} is not solvable");
            }
        }

        [Test]
        public void ReferenceSolutionFromThePythonToolStillWins()
        {
            // This is the cross-language check: a path proven correct by the Python
            // solver must replay move-for-move in the C# engine.
            foreach (int number in ContentLoader.AvailableLevels())
            {
                LevelDefinition level = ContentLoader.Load(number);
                Assert.IsNotEmpty(level.ReferenceSolution, $"level {number} has no solution");

                GameState state = GameState.Initial(level);
                foreach (int busId in level.ReferenceSolution)
                {
                    Assert.AreEqual(GameState.Refusal.None, state.CanDispatch(level, busId),
                                    $"level {number}: bus {busId} was not dispatchable");
                    state = state.Dispatch(level, busId);
                }
                Assert.IsTrue(state.IsWon(level), $"level {number}: solution did not win");
            }
        }

        [Test]
        public void HintAlwaysReturnsAMoveThatKeepsTheLevelWinnable()
        {
            foreach (int number in ContentLoader.AvailableLevels())
            {
                LevelDefinition level = ContentLoader.Load(number);
                GameState state = GameState.Initial(level);

                int guard = 0;
                while (!state.IsWon(level) && guard++ < 100)
                {
                    int busId = Solver.Hint(level, state);
                    Assert.AreNotEqual(0, busId, $"level {number}: hint gave up");
                    state = state.Dispatch(level, busId);
                    Assert.IsTrue(Solver.IsSolvable(level, state),
                                  $"level {number}: hint led into a dead end");
                }
                Assert.IsTrue(state.IsWon(level), $"level {number}: hints did not finish");
            }
        }
    }

    public class MessageBookTests
    {
        private static MessageBook Book() => ContentLoader.Messages("de");

        [Test]
        public void TheFullSetOfCardsIsPresent()
        {
            MessageBook book = Book();
            Assert.IsNotNull(book);
            // One per level plus headroom, so finishing all 200 never repeats.
            Assert.AreEqual(240, book.All.Count);
        }

        [Test]
        public void EverySituationalCategoryCoversEveryTone()
        {
            // Otherwise a player who chose "quiet" gets served the playful
            // register whenever the picker has to widen its search.
            MessageBook book = Book();
            string[] categories = { "recognition", "playful", "gentle", "grounding", "return" };
            string[] tones = { "warm", "playful", "quiet" };

            foreach (string category in categories)
            {
                foreach (string tone in tones)
                {
                    int count = 0;
                    foreach (Postcard c in book.All)
                    {
                        if (c.Category == category && c.Tone == tone) count++;
                    }
                    Assert.GreaterOrEqual(count, 8,
                        $"{category}/{tone} has only {count} cards");
                }
            }
        }

        [Test]
        public void WarmIsTheDefaultAndStaysWarm()
        {
            // A fast clean solve normally draws from the "playful" category. A
            // player on the warm setting must still get a warm-toned card.
            MessageBook book = Book();
            var ctx = new WinContext(7, 1, false, 12, 0);
            Assert.AreEqual("playful", MessageBook.CategoryFor(ctx));

            var seen = new List<string>();
            for (int i = 0; i < 12; i++)
            {
                Postcard card = book.Pick(ctx, ToneSetting.Warm, seen);
                Assert.AreEqual("warm", card.Tone,
                    $"warm player was served a {card.Tone} card: \"{card.Text}\"");
                seen.Add(card.Id);
            }
        }

        [Test]
        public void ToneOffShowsNothing()
        {
            var ctx = new WinContext(3, 1, false, 12, 0);
            Assert.IsNull(Book().Pick(ctx, ToneSetting.Off, new List<string>()));
        }

        [Test]
        public void ContextPicksTheRegister()
        {
            Assert.AreEqual("milestone",
                MessageBook.CategoryFor(new WinContext(25, 1, false, 12, 0)));
            Assert.AreEqual("return",
                MessageBook.CategoryFor(new WinContext(7, 1, false, 12, 5)));
            Assert.AreEqual("gentle",
                MessageBook.CategoryFor(new WinContext(7, 1, true, 12, 0)));
            Assert.AreEqual("recognition",
                MessageBook.CategoryFor(new WinContext(7, 4, false, 12, 0)));
            Assert.AreEqual("grounding",
                MessageBook.CategoryFor(new WinContext(7, 1, false, 23, 0)));
            Assert.AreEqual("playful",
                MessageBook.CategoryFor(new WinContext(7, 1, false, 12, 0)));
        }

        [Test]
        public void MilestoneCardIsPinnedToItsLevel()
        {
            var ctx = new WinContext(25, 1, false, 12, 0);
            Postcard card = Book().Pick(ctx, ToneSetting.Warm, new List<string>());
            Assert.AreEqual(25, card.Level);
        }

        [Test]
        public void NeverRepeatsWhileFreshCardsRemain()
        {
            MessageBook book = Book();
            var seen = new List<string>();
            var ctx = new WinContext(7, 1, false, 12, 0);

            // Draw as many as the smallest category can supply without repeating.
            for (int i = 0; i < 8; i++)
            {
                Postcard card = book.Pick(ctx, ToneSetting.Warm, seen);
                Assert.IsNotNull(card);
                CollectionAssert.DoesNotContain(seen, card.Id);
                seen.Add(card.Id);
            }
        }

        [Test]
        public void FallsBackRatherThanReturningNothing()
        {
            MessageBook book = Book();
            var everything = new List<string>();
            foreach (Postcard c in book.All) everything.Add(c.Id);

            Postcard card = book.Pick(new WinContext(7, 1, false, 12, 0),
                                      ToneSetting.Warm, everything);
            Assert.IsNotNull(card, "must still show something once all cards are recent");
        }
    }

    public class PaletteTests
    {
        [Test]
        public void EveryColourHasADistinctNonColourGlyph()
        {
            string[] colors = { "red", "blue", "green", "yellow",
                                "purple", "orange", "pink", "teal" };
            var glyphs = new HashSet<string>();
            foreach (string c in colors)
            {
                string glyph = Palette.GlyphOf(c);
                Assert.AreNotEqual("?", glyph, $"{c} has no glyph");
                Assert.IsTrue(glyphs.Add(glyph), $"{c} reuses glyph {glyph}");
            }
        }

        [Test]
        public void EveryPaletteDefinesEveryColour()
        {
            string[] colors = { "red", "blue", "green", "yellow",
                                "purple", "orange", "pink", "teal" };
            foreach (Palette.Mode mode in System.Enum.GetValues(typeof(Palette.Mode)))
            {
                Palette.ColorMode = mode;
                foreach (string c in colors)
                {
                    Assert.AreNotEqual(Color.gray, Palette.Of(c),
                                       $"{mode} palette is missing {c}");
                }
            }
            Palette.ColorMode = Palette.Mode.Default;
        }
    }
}
