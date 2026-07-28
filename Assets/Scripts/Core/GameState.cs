using System;
using System.Collections.Generic;
using System.Text;

namespace SunnyStop.Core
{
    /// <summary>A docking bay: either free, or holding a bus with seats left.</summary>
    public readonly struct Bay : IEquatable<Bay>
    {
        public static readonly Bay Free = default;

        public readonly int BusId;
        public readonly string Color;
        public readonly int SeatsLeft;

        public bool IsFree => BusId == 0;

        public Bay(int busId, string color, int seatsLeft)
        {
            BusId = busId;
            Color = color;
            SeatsLeft = seatsLeft;
        }

        public Bay WithSeats(int seatsLeft) =>
            seatsLeft <= 0 ? Free : new Bay(BusId, Color, seatsLeft);

        public bool Equals(Bay other) =>
            BusId == other.BusId && SeatsLeft == other.SeatsLeft;

        public override bool Equals(object obj) => obj is Bay other && Equals(other);
        public override int GetHashCode() => (BusId * 397) ^ SeatsLeft;
    }

    /// <summary>
    /// Immutable game state. Every transition returns a new instance, which is what
    /// makes undo a one-liner and lets the solver hash states directly.
    /// Mirrors tools/leveltool/rules.py.
    /// </summary>
    public sealed class GameState : IEquatable<GameState>
    {
        /// <summary>Bus ids still parked in the lot.</summary>
        public IReadOnlyCollection<int> InLot => _inLot;
        public IReadOnlyList<Bay> Bays => _bays;
        public int QueueIndex { get; }
        public int Moves { get; }

        private readonly HashSet<int> _inLot;
        private readonly Bay[] _bays;
        private readonly int _hash;

        private GameState(HashSet<int> inLot, Bay[] bays, int queueIndex, int moves)
        {
            _inLot = inLot;
            _bays = bays;
            QueueIndex = queueIndex;
            Moves = moves;
            _hash = ComputeHash();
        }

        public static GameState Initial(LevelDefinition level)
        {
            var inLot = new HashSet<int>();
            foreach (Bus b in level.Buses) inLot.Add(b.Id);
            var bays = new Bay[level.Bays];
            var state = new GameState(inLot, bays, 0, 0);
            return state.ResolveBoarding(level);
        }

        public bool IsBusInLot(int busId) => _inLot.Contains(busId);

        /// <summary>Board the head of the queue until nothing more can board.</summary>
        public GameState ResolveBoarding(LevelDefinition level)
        {
            var bays = (Bay[])_bays.Clone();
            int index = QueueIndex;
            bool changed = true;
            while (changed && index < level.Queue.Count)
            {
                changed = false;
                Passenger head = level.Queue[index];
                for (int i = 0; i < bays.Length; i++)
                {
                    Bay bay = bays[i];
                    if (bay.IsFree) continue;
                    if (bay.Color != head.Color || bay.SeatsLeft < head.Seats) continue;
                    bays[i] = bay.WithSeats(bay.SeatsLeft - head.Seats);
                    index++;
                    changed = true;
                    break;
                }
            }
            return new GameState(_inLot, bays, index, Moves);
        }

        /// <summary>True if the bus is parked and its exit path is clear.</summary>
        public bool IsPathClear(LevelDefinition level, int busId)
        {
            if (!_inLot.Contains(busId)) return false;
            Bus bus = level.BusById(busId);
            foreach (Cell cell in bus.ExitPath)
            {
                if (level.IsBlocked(cell)) return false;
                foreach (int otherId in _inLot)
                {
                    if (otherId == busId) continue;
                    foreach (Cell occupied in level.BusById(otherId).Cells)
                    {
                        if (occupied.Equals(cell)) return false;
                    }
                }
            }
            return true;
        }

        public bool HasFreeBay()
        {
            foreach (Bay bay in _bays)
            {
                if (bay.IsFree) return true;
            }
            return false;
        }

        /// <summary>Why a tap was refused - drives the feedback the player sees.</summary>
        public enum Refusal
        {
            None,
            NotInLot,
            Blocked,
            NoFreeBay,
            AlreadyWon
        }

        public Refusal CanDispatch(LevelDefinition level, int busId)
        {
            if (IsWon(level)) return Refusal.AlreadyWon;
            if (!_inLot.Contains(busId)) return Refusal.NotInLot;
            if (!IsPathClear(level, busId)) return Refusal.Blocked;
            if (!HasFreeBay()) return Refusal.NoFreeBay;
            return Refusal.None;
        }

        public List<int> LegalMoves(LevelDefinition level)
        {
            var moves = new List<int>();
            if (IsWon(level) || !HasFreeBay()) return moves;
            foreach (int busId in _inLot)
            {
                if (IsPathClear(level, busId)) moves.Add(busId);
            }
            moves.Sort();
            return moves;
        }

        /// <summary>Dispatch a bus into the leftmost free bay, then resolve boarding.</summary>
        public GameState Dispatch(LevelDefinition level, int busId)
        {
            Refusal refusal = CanDispatch(level, busId);
            if (refusal != Refusal.None)
                throw new InvalidOperationException($"cannot dispatch bus {busId}: {refusal}");

            Bus bus = level.BusById(busId);
            var bays = (Bay[])_bays.Clone();
            for (int i = 0; i < bays.Length; i++)
            {
                if (!bays[i].IsFree) continue;
                bays[i] = new Bay(bus.Id, bus.Color, bus.Capacity);
                break;
            }

            var inLot = new HashSet<int>(_inLot);
            inLot.Remove(busId);
            var next = new GameState(inLot, bays, QueueIndex, Moves + 1);
            return next.ResolveBoarding(level);
        }

        public bool IsWon(LevelDefinition level) => QueueIndex >= level.Queue.Count;

        /// <summary>No win, and nothing left to do.</summary>
        public bool IsDead(LevelDefinition level)
        {
            if (IsWon(level)) return false;
            if (level.MoveLimit.HasValue && Moves >= level.MoveLimit.Value) return true;
            return LegalMoves(level).Count == 0;
        }

        /// <summary>Head of the queue, or null when the queue is empty.</summary>
        public Passenger Head(LevelDefinition level) =>
            QueueIndex < level.Queue.Count ? level.Queue[QueueIndex] : null;

        private int ComputeHash()
        {
            unchecked
            {
                int h = 17;
                int lotHash = 0;
                foreach (int id in _inLot) lotHash ^= 1 << (id % 31);
                h = h * 31 + lotHash;
                foreach (Bay bay in _bays) h = h * 31 + bay.GetHashCode();
                h = h * 31 + QueueIndex;
                return h;
            }
        }

        public bool Equals(GameState other)
        {
            if (other is null) return false;
            if (QueueIndex != other.QueueIndex) return false;
            if (_bays.Length != other._bays.Length) return false;
            if (_inLot.Count != other._inLot.Count) return false;
            for (int i = 0; i < _bays.Length; i++)
            {
                if (!_bays[i].Equals(other._bays[i])) return false;
            }
            return _inLot.SetEquals(other._inLot);
        }

        public override bool Equals(object obj) => Equals(obj as GameState);
        public override int GetHashCode() => _hash;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("lot[");
            foreach (int id in _inLot) sb.Append(id).Append(' ');
            sb.Append("] bays[");
            foreach (Bay bay in _bays)
                sb.Append(bay.IsFree ? "-" : $"{bay.Color}:{bay.SeatsLeft}").Append(' ');
            sb.Append($"] q={QueueIndex}");
            return sb.ToString();
        }
    }
}
