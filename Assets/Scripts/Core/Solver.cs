using System.Collections.Generic;

namespace SunnyStop.Core
{
    /// <summary>
    /// Runtime solver. The boards are small enough (a few thousand reachable states
    /// even at level 120) to answer "can this still be won?" exactly, on device, in
    /// under a millisecond.
    ///
    /// That single question powers both anti-frustration features from CONCEPT.md §5.4:
    /// the hint button, and detecting the moment a move made the level unwinnable so
    /// the game can offer "Rewind that one?" instead of letting the player grind on
    /// towards a loss they cannot see coming.
    ///
    /// Note this deliberately does NOT use the reference solution baked into the level
    /// file: that path is only valid from the starting position, and a hint is worth
    /// nothing unless it works from wherever the player actually is.
    /// </summary>
    public static class Solver
    {
        private readonly struct Key
        {
            private readonly GameState _state;
            private readonly int _moves;

            public Key(GameState state, bool countMoves)
            {
                _state = state;
                _moves = countMoves ? state.Moves : 0;
            }

            public override int GetHashCode() => (_state.GetHashCode() * 397) ^ _moves;

            public override bool Equals(object obj) =>
                obj is Key other && _moves == other._moves && _state.Equals(other._state);
        }

        /// <summary>Can the level still be won from this state?</summary>
        public static bool IsSolvable(LevelDefinition level, GameState state)
        {
            var memo = new Dictionary<Key, bool>();
            return Search(level, state, memo);
        }

        /// <summary>Every legal move that keeps the level winnable.</summary>
        public static List<int> SafeMoves(LevelDefinition level, GameState state)
        {
            var memo = new Dictionary<Key, bool>();
            var safe = new List<int>();
            foreach (int busId in state.LegalMoves(level))
            {
                if (Search(level, state.Dispatch(level, busId), memo)) safe.Add(busId);
            }
            return safe;
        }

        /// <summary>
        /// One good next move, or 0 if the level can no longer be won.
        /// Prefers a move that gets somebody aboard right away - a hint the player can
        /// see the point of is worth more than a technically optimal one.
        /// </summary>
        public static int Hint(LevelDefinition level, GameState state)
        {
            List<int> safe = SafeMoves(level, state);
            if (safe.Count == 0) return 0;

            int best = safe[0];
            int bestProgress = -1;
            foreach (int busId in safe)
            {
                GameState next = state.Dispatch(level, busId);
                int progress = next.QueueIndex - state.QueueIndex;
                if (progress > bestProgress)
                {
                    bestProgress = progress;
                    best = busId;
                }
            }
            return best;
        }

        /// <summary>Shortest remaining winning sequence, or null if there is none.</summary>
        public static List<int> Solve(LevelDefinition level, GameState state)
        {
            var came = new Dictionary<GameState, (GameState prev, int move)>();
            var seen = new HashSet<GameState> { state };
            var frontier = new Queue<GameState>();
            frontier.Enqueue(state);

            while (frontier.Count > 0)
            {
                GameState current = frontier.Dequeue();
                if (current.IsWon(level))
                {
                    var path = new List<int>();
                    while (!current.Equals(state))
                    {
                        var step = came[current];
                        path.Add(step.move);
                        current = step.prev;
                    }
                    path.Reverse();
                    return path;
                }
                foreach (int busId in current.LegalMoves(level))
                {
                    GameState next = current.Dispatch(level, busId);
                    if (!seen.Add(next)) continue;
                    came[next] = (current, busId);
                    frontier.Enqueue(next);
                }
            }
            return null;
        }

        private static bool Search(LevelDefinition level, GameState state,
                                   Dictionary<Key, bool> memo)
        {
            if (state.IsWon(level)) return true;
            bool countMoves = level.MoveLimit.HasValue;
            var key = new Key(state, countMoves);
            if (memo.TryGetValue(key, out bool cached)) return cached;

            // Guard against revisiting the same state inside the current branch.
            memo[key] = false;
            foreach (int busId in state.LegalMoves(level))
            {
                if (Search(level, state.Dispatch(level, busId), memo))
                {
                    memo[key] = true;
                    return true;
                }
            }
            return false;
        }
    }
}
