using System;
using System.Collections;
using System.Collections.Generic;
using SunnyStop.Core;
using UnityEngine;

namespace SunnyStop.Game
{
    /// <summary>
    /// Drives one level: taps in, animation out, and the anti-frustration behaviour
    /// from CONCEPT.md §5.4.
    ///
    /// The important design decision lives in <see cref="AfterMove"/>: the game checks
    /// solvability after every dispatch, so a player who has just made the level
    /// unwinnable is told immediately and offered a free rewind - instead of playing
    /// on for two minutes towards a loss that was already decided. No lives are spent
    /// and nothing is sold at that moment.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        private const int HintOfferedAfterFails = 3;
        private const int SkipOfferedAfterFails = 10;

        private BoardView _board;
        private Hud _hud;
        private PostcardView _postcard;

        private LevelDefinition _level;
        private GameState _state;
        private readonly Stack<GameState> _history = new Stack<GameState>();
        private List<int> _available = new List<int>();
        private int _levelNumber;
        private bool _busy;
        private bool _levelComplete;

        /// <summary>Raised when the player asks for the start menu.</summary>
        public event Action MenuRequested;

        /// <summary>Level numbers that actually shipped, ascending.</summary>
        public IReadOnlyList<int> AvailableLevels => _available;

        public int CurrentLevel => _levelNumber;

        public void Initialise(BoardView board, Hud hud, PostcardView postcard)
        {
            _board = board;
            _hud = hud;
            _postcard = postcard;

            _hud.HintRequested += OnHintRequested;
            _hud.UndoRequested += OnUndoRequested;
            _hud.RestartRequested += () => LoadLevel(_levelNumber);
            _hud.MenuRequested += () => MenuRequested?.Invoke();
            _board.BusTapped += OnBusTapped;

            _available = ContentLoader.AvailableLevels();
            Palette.ColorMode = SaveGame.ColorMode;
        }

        public void StartFromSave()
        {
            int target = SaveGame.HighestLevelReached;
            if (!_available.Contains(target))
            {
                target = _available.Count > 0 ? _available[0] : 1;
            }
            LoadLevel(target);
        }

        public void LoadLevel(int levelNumber)
        {
            StopAllCoroutines();
            _busy = false;
            _levelComplete = false;
            _history.Clear();
            _hud.HideBanner();

            _levelNumber = levelNumber;
            _level = ContentLoader.Load(levelNumber);
            if (_level == null) return;

            _state = GameState.Initial(_level);
            SaveGame.HighestLevelReached = levelNumber;
            SaveGame.RecordAttempt(levelNumber);
            SaveGame.Flush();

            _board.Build(_level);
            _board.ApplyState(_state);

            if (Camera.main != null)
                Camera.main.backgroundColor = Palette.SkyForChapter(_level.Chapter);
            _hud.SetLevel(levelNumber, _level.Chapter);
            RefreshHud();

            int fails = SaveGame.AttemptsOn(levelNumber) - 1;
            if (fails >= SkipOfferedAfterFails)
            {
                OfferSkip();
            }
            else if (fails >= HintOfferedAfterFails)
            {
                OfferHelp();
            }
        }

        // ----- input ----------------------------------------------------------- //

        private void OnBusTapped(int busId)
        {
            if (_busy || _levelComplete || _postcard.IsShowing) return;

            GameState.Refusal refusal = _state.CanDispatch(_level, busId);
            if (refusal != GameState.Refusal.None)
            {
                StartCoroutine(RefuseMove(busId, refusal));
                return;
            }
            StartCoroutine(PerformMove(busId));
        }

        private IEnumerator RefuseMove(int busId, GameState.Refusal refusal)
        {
            _busy = true;
            AudioDirector.Play(Sound.Refused);
            yield return _board.PlayRefusal(busId);
            _busy = false;

            if (refusal == GameState.Refusal.NoFreeBay && !_hud.IsBannerVisible)
            {
                _hud.ShowBanner("Alle Buchten sind belegt.", "Verstanden", null);
            }
        }

        private IEnumerator PerformMove(int busId)
        {
            _busy = true;
            _history.Push(_state);
            // Each move starts a fresh run up the scale, so a big cascade sounds
            // like one - which is exactly what it is.
            AudioDirector.ResetChain();

            List<TransitionEvent> events =
                Transition.Compute(_level, _state, busId, out GameState next);

            foreach (TransitionEvent evt in events)
            {
                switch (evt.Kind)
                {
                    case TransitionEventKind.BusDispatched:
                        AudioDirector.Play(Sound.Dispatch);
                        yield return _board.PlayDispatch(_level.BusById(evt.BusId), evt.BayIndex);
                        AudioDirector.Play(Sound.Dock);
                        break;

                    case TransitionEventKind.PassengerBoarded:
                    {
                        // The queue view is rebuilt from the level index, so the slot
                        // of the boarding passenger is always 0 relative to the head.
                        Passenger boarding = _level.Queue[evt.QueueIndex];
                        AudioDirector.PlayBoarding();
                        yield return _board.PlayBoarding(
                            0, evt.BayIndex, evt.SeatsLeft,
                            evt.BusId, boarding.Seats, boarding.Color);
                        _board.RefreshQueue(evt.QueueIndex + 1);
                        break;
                    }

                    case TransitionEventKind.BusDeparted:
                        AudioDirector.Play(Sound.Depart);
                        yield return _board.PlayDeparture(evt.BusId);
                        break;
                }
            }

            _state = next;
            _board.RefreshQueue(_state.QueueIndex);
            RefreshHud();
            _busy = false;

            AfterMove();
        }

        // ----- state checks ---------------------------------------------------- //

        private void AfterMove()
        {
            if (_state.IsWon(_level))
            {
                CompleteLevel();
                return;
            }

            if (_state.IsDead(_level))
            {
                OfferRewind("Hier geht es nicht mehr weiter.");
                return;
            }

            // The valuable check: still legal moves, but none of them can win any more.
            if (!Solver.IsSolvable(_level, _state))
            {
                OfferRewind("Mit diesem Zug ist das Level nicht mehr zu schaffen.");
            }
        }

        private void OfferRewind(string reason)
        {
            // A hand on the shoulder, not an alarm: the player has just made a
            // losing move and the game is about to offer to take it back.
            AudioDirector.Play(Sound.Rewind);
            _hud.ShowBanner(
                reason,
                "Zug zurücknehmen", OnUndoRequested,
                "Neu starten", () => LoadLevel(_levelNumber));
        }

        private void OfferHelp()
        {
            _hud.ShowBanner(
                "Der hier ist zäh. Soll ich dir einen Bus zeigen?",
                "Ja, gern", OnHintRequested,
                "Nein danke", null);
        }

        private void OfferSkip()
        {
            _hud.ShowBanner(
                "Dieses Level darf auch mal liegen bleiben.",
                "Überspringen", SkipLevel,
                "Weiter versuchen", null);
        }

        private void CompleteLevel()
        {
            _levelComplete = true;
            int attempts = SaveGame.AttemptsOn(_levelNumber);
            bool skipped = SaveGame.WasSkipped(_levelNumber);

            var context = new WinContext(
                level: _levelNumber,
                attempts: attempts,
                skipped: skipped,
                hourOfDay: System.DateTime.Now.Hour,
                daysSinceLastPlay: SaveGame.DaysSinceLastPlay());

            SaveGame.ClearAttempts(_levelNumber);
            SaveGame.MarkCleared(_levelNumber);
            SaveGame.TouchLastPlayed();
            SaveGame.Flush();

            StartCoroutine(CelebrateThenReward(context));
        }

        private IEnumerator CelebrateThenReward(WinContext context)
        {
            AudioDirector.Play(Sound.Win);
            // Let the last bus finish leaving before the screen changes.
            yield return new WaitForSeconds(1.0f);

            ToneSetting tone = SaveGame.Tone;
            MessageBook book = ContentLoader.Messages();
            Postcard card = book?.Pick(context, tone, SaveGame.RecentCards());

            if (card == null)
            {
                // Tone is off, or no content: go straight on. Never a fallback ad.
                AdvanceToNextLevel();
                yield break;
            }

            SaveGame.PushRecentCard(card.Id);
            SaveGame.Flush();

            // From here until the player moves on, the board is quiet and the
            // card gets one soft bell. See AudioDirector for why this matters
            // more than it looks.
            AudioDirector.SetPostcardShowing(true);
            AudioDirector.Play(Sound.Postcard);
            _postcard.Show(card, context.Level, () =>
            {
                AudioDirector.SetPostcardShowing(false);
                AdvanceToNextLevel();
            });
        }

        private void AdvanceToNextLevel()
        {
            int index = _available.IndexOf(_levelNumber);
            if (index >= 0 && index + 1 < _available.Count)
            {
                LoadLevel(_available[index + 1]);
                return;
            }

            _hud.ShowBanner(
                "Das war das letzte Level im Prototyp. Danke fürs Mitfahren.",
                "Von vorn", () => LoadLevel(_available.Count > 0 ? _available[0] : 1));
        }

        private void SkipLevel()
        {
            SaveGame.MarkSkipped(_levelNumber);
            SaveGame.Flush();
            AdvanceToNextLevel();
        }

        // ----- assistance ------------------------------------------------------ //

        private void OnHintRequested()
        {
            if (_busy || _levelComplete) return;

            int busId = Solver.Hint(_level, _state);
            if (busId == 0)
            {
                OfferRewind("Von hier aus geht es leider nicht mehr.");
                return;
            }
            _board.HighlightBus(busId);
        }

        private void OnUndoRequested()
        {
            if (_busy || _levelComplete || _history.Count == 0) return;

            _state = _history.Pop();
            _hud.HideBanner();

            // Rebuilding is cheap at these board sizes and keeps the view honest:
            // ApplyState is the only code that turns state into visuals.
            _board.Build(_level);
            _board.ApplyState(_state);
            RefreshHud();
        }

        private void RefreshHud()
        {
            _hud.SetQueueInfo(_level.Queue.Count - _state.QueueIndex, _level.Queue.Count);
            _hud.SetUndoAvailable(_history.Count > 0);
        }
    }
}
